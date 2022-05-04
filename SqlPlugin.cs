using Microsoft.SqlServer.TransactSql.ScriptDom;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Metacrack
{
    [Plugin("sql")]
    public class SqlPlugin : PluginBase
    {
        private static List<string> _keyword = new List<string> { "CREATE", "DROP", "INSERT", "SELECT", "UPDATE", "ALTER", "BEGIN", "DELETE", "TRUNCATE"};       
        private static TSql150Parser _parser = new TSql150Parser(true);

        public static void Process(SqlOptions options)
        {
            //Validate and display arguments
            var sqlFileEntries = Directory.GetFiles(Directory.GetCurrentDirectory(), options.InputPath);

            if (sqlFileEntries.Length == 0)
            {
                WriteError($"No .sql files found for {options.InputPath}.");
                return;
            }

            Console.WriteLine($"Started at {DateTime.Now.ToShortTimeString()}.");

            var columns = Array.ConvertAll(options.Columns.ToArray(), int.Parse);
            var metas = (options.MetaColumns.Count() == 0) ? new int[0] : Array.ConvertAll(options.MetaColumns.ToArray(), int.Parse);

            var size = GetFileEntriesSize(sqlFileEntries);
            var progressTotal = 0L;
            var lineCount = 0L;
            var statementLineCount = 0L;

            var fileOutput = new List<string>();
            var metaOutput = new List<string>();

            var currentDirectory = Directory.GetCurrentDirectory();

            foreach (var sqlPath in sqlFileEntries)
            {
                WriteMessage($"Processing {sqlPath}.");

                var fileName = Path.GetFileNameWithoutExtension(sqlPath);
                var filePathName = $"{currentDirectory}\\{fileName}";
                var outputPath = $"{filePathName}.parsed.txt";
                var metapath = $"{filePathName}.meta.txt";

                //Check that there are no output files
                if (!CheckOverwrite(new string[] { outputPath, metapath }))
                {
                    WriteHighlight($"Skipping {filePathName}.");
                    var fileInfo = new FileInfo(sqlPath);
                    progressTotal += fileInfo.Length;

                    continue;
                }

                fileOutput.Clear();
                metaOutput.Clear();
                lineCount = 0;               

                using (var reader = new StreamReader(sqlPath))
                {
                    var buffer = new StringBuilder();
                    var inInsert = false;

                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine().TrimStart();

                        lineCount++;
                        progressTotal += line.Length;

                        if (options.Start > 0 && lineCount < options.Start) continue;
                        if (options.End > 0 && lineCount > options.End) continue;

                        var isNewStatement = false;
                        

                        //Detect a new statement
                        foreach (var statement in _keyword)
                        {
                            if (line.StartsWith(statement, StringComparison.InvariantCultureIgnoreCase))
                            {
                                isNewStatement = true;
                                break;                                
                            }
                        }

                        //We collect each line and put them into a string buffer.
                        if (isNewStatement && buffer.Length > 0)
                        {
                            var parsed = ProcessBuffer(options, buffer, statementLineCount, columns, metas, fileOutput, metaOutput);

                            if (options.Debug)
                            {
                                if (parsed != null)
                                {
                                    WriteMessage("Debug complete. Exiting.");
                                    return;
                                }
                            }

                            buffer.Clear();

                            //Check if we need to flush the file output
                            if (fileOutput.Count >= 100000)
                            {
                                if (fileOutput.Count > 0) File.AppendAllLines(outputPath, fileOutput);
                                if (metaOutput.Count > 0) File.AppendAllLines(metapath, metaOutput);

                                fileOutput.Clear();
                                metaOutput.Clear();
                            }
                        }

                        //Insert statements must start on a new line
                        if (isNewStatement)
                        {
                            inInsert = false;
                            if (line.StartsWith("INSERT", StringComparison.InvariantCultureIgnoreCase))
                            {
                                inInsert = true;
                                statementLineCount = lineCount;
                            }
                        }

                        if (inInsert)
                        {
                            //Change backticks to " and use sql in quoted identifier mode (use true on constructor of parser)
                            //https://stackoverflow.com/questions/19657101/what-is-the-difference-between-square-brackets-and-single-quotes-for-aliasing-in
                            line = line.Replace("`", "\"");

                            //_binary is handled differently in tsql. For now we just remove the keyword
                            line = line.Replace("_binary", "");

                            //Remove any mysql escape characters
                            line = line.Replace(@"\\", @"");//Using a single \ seems to cause issues

                            line = line.Replace(@"\'", "''");
                            line = line.Replace("\\\"", "\"");
                            line = line.Replace("\\t", "");
                            line = line.Replace("\\%", "%");
                            line = line.Replace("\\_", "_");

                            


                            buffer.AppendLine(line);
                        }

                        //Update the percentage. Sometimes a line can be very long, so we use a low number
                        if (lineCount % 100 == 0) WriteProgress($"Parsing {fileName}", progressTotal, size);
                    }
                }

                //Write out file
                WriteMessage($"Finished writing to {fileName}.parsed.txt at {DateTime.Now.ToShortTimeString()}.");
                if (fileOutput.Count > 0) File.AppendAllLines(outputPath, fileOutput);
                if (metaOutput.Count > 0) File.AppendAllLines(metapath, metaOutput);
            }

            WriteMessage($"Completed at {DateTime.Now.ToShortTimeString()}.");
        } 
        
        private static StatementList ProcessBuffer(SqlOptions options, StringBuilder buffer, long lineCount, int[] columns, int[] metas, List<string> fileOutput, List<string> metaOutput)
        {
            var statements = _parser.ParseStatementList(new StringReader(buffer.ToString()), out var errors);

            if (errors.Count > 0)
            {
                if (options.Debug) WriteMessage($"Line {lineCount},{errors[0].Offset}:{errors[0].Message}");
                return null;
            }

            if (statements != null)
            {
                //We dont want to loose progress over what could be a long time processing
                try
                {
                    foreach (var statement in statements.Statements)
                    {
                        if (statement is InsertStatement)
                        {
                            var insertStatement = statement as InsertStatement;
                            var target = insertStatement.InsertSpecification.Target as NamedTableReference;

                            //Check the name and quit if its incorrect
                            if (!string.Equals(target.SchemaObject.BaseIdentifier.Value, options.Table, StringComparison.InvariantCultureIgnoreCase)) return null;

                            if (options.Debug) WriteMessage($"Parsed insert statement for target table without errors.");

                            var spec = insertStatement.InsertSpecification;

                            var source = spec.InsertSource;

                            if (source is ValuesInsertSource)
                            {
                                var valuesSource = source as ValuesInsertSource;

                                foreach (var rowValues in valuesSource.RowValues)
                                {
                                    var outputs = new List<string>();
                                    var metaoutputs = new List<string>();

                                    foreach (var column in columns)
                                    {
                                        var literal = rowValues.ColumnValues[column] as Literal;

                                        //Convert NULL to empty string
                                        outputs.Add((literal.Value == "NULL") ? "" : literal.Value);
                                    }

                                    foreach (var meta in metas)
                                    {
                                        var literal = rowValues.ColumnValues[meta] as Literal;

                                        //Convert NULL to empty string
                                        metaoutputs.Add((literal.Value == "NULL") ? "" : literal.Value);
                                    }

                                    //Ignore empty outputs and empty passwords
                                    if (outputs.Count > 0 && !string.IsNullOrEmpty(outputs[1]))
                                    {
                                        //Force to lowercase only the first output (email)
                                        outputs[0] = outputs[0].ToLower();

                                        var result = string.Join(":", outputs);
                                        var metaresult = "";

                                        fileOutput.Add(result);

                                        if (metas.Count() > 0)
                                        {
                                            metaresult = string.Join(":", metaoutputs);

                                            //Put the email in front of the metas
                                            metaOutput.Add($"{outputs[0]}:{metaresult}");
                                        }

                                        //Write debug information
                                        if (options.Debug)
                                        {
                                            if (metas.Count() > 0)
                                            {
                                                WriteMessage($"{result} ({metaresult})");
                                            }
                                            else
                                            {
                                                WriteMessage(result);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteMessage($"Error line {lineCount}: {ex.Message}");
                    //WriteMessage(line);
                }
            }

            return statements;
        }
    }
}
