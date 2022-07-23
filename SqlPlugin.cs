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

            //Parse out any special modes
            var modes = options.Modes.Select(s => s.ToLowerInvariant());
            var canForceNew = options.Modes.Contains("force-insert");

            if (modes.Count() > 0)
            {
                WriteMessage($"Detected {modes.Count()} mode(s).");
                if (canForceNew) WriteMessage($"Force new inserts enabled.");
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
                var debugPath = $"{filePathName}.debug.txt";

                //Check that there are no output files
                if (!CheckForFiles(new string[] { outputPath, metapath }))
                {
                    WriteHighlight($"Skipping {filePathName}.");
                    var fileInfo = new FileInfo(sqlPath);
                    progressTotal += fileInfo.Length;

                    continue;
                }

                fileOutput.Clear();
                metaOutput.Clear();
                lineCount = 0;

                var buffer = new StringBuilder();

                using (var reader = new StreamReader(sqlPath))
                {                   
                    var inInsert = false;
                    var line = "";
                    var forceNew = false;

                    while (!reader.EndOfStream)
                    {
                        line = reader.ReadLine().TrimStart();

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
                        if ((isNewStatement || forceNew) && buffer.Length > 0)
                        {
                            var parsed = ProcessBuffer(options, buffer, statementLineCount, columns, metas, fileOutput, metaOutput, debugPath);

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

                            if (forceNew)
                            {
                                buffer.AppendLine($"INSERT INTO {options.Table} VALUES");
                                forceNew = false;
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

                        //Force a new statement
                        if (canForceNew && buffer.Length > 10485760 && line.EndsWith(",")) //10MB
                        {
                            line = line.Substring(0, line.Length - 1);
                            forceNew = true;
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
                            line = line.Replace("\\.", ".");
                            line = line.Replace("\\N", "''");

                            buffer.AppendLine(line);
                        }

                        //Update the percentage. Sometimes a line can be very long, so we use a low number
                        if (lineCount % 100 == 0) WriteProgress($"Parsing {fileName}", progressTotal, size);
                    }
                }

                //We need to process the last buffer
                if (buffer.Length > 0) ProcessBuffer(options, buffer, statementLineCount, columns, metas, fileOutput, metaOutput, debugPath);

                //Write out file
                WriteMessage($"Finished writing to {fileName}.parsed.txt at {DateTime.Now.ToShortTimeString()}.");
                if (fileOutput.Count > 0) File.AppendAllLines(outputPath, fileOutput);
                if (metaOutput.Count > 0) File.AppendAllLines(metapath, metaOutput);
            }

            WriteMessage($"Completed at {DateTime.Now.ToShortTimeString()}.");
        } 
        
        private static StatementList ProcessBuffer(SqlOptions options, StringBuilder buffer, long lineCount, int[] columns, int[] metas, List<string> fileOutput, List<string> metaOutput, string debugPath)
        {
            var bufferString = buffer.ToString();
            var statements = _parser.ParseStatementList(new StringReader(bufferString), out var errors);

            if (errors.Count > 0)
            {
                if (options.Debug)
                {
                    WriteHighlight($"Line {lineCount},{errors[0].Offset}:{errors[0].Message}");

                    var lines = new List<string>();

                    lines.Add($"Line {lineCount},{errors[0].Offset}:{errors[0].Message}");
                    lines.Add(bufferString);

                    File.AppendAllLines(debugPath, lines);

                    WriteMessage($"Wrote sql debug output to {debugPath}");

                    return null;
                }
            }

            var parseColumns = columns.Length == 0 && options.ColumnNames.Count() > 0;
            var parseMetas = metas.Length == 0 && options.MetaNames.Count() > 0;

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
                                //Check if we need to parse out column values
                                if (parseColumns) columns = ParseColumns(insertStatement, spec, options.ColumnNames);
                                if (parseMetas) metas = ParseColumns(insertStatement, spec, options.MetaNames);

                                var valuesSource = source as ValuesInsertSource;

                                foreach (var rowValues in valuesSource.RowValues)
                                {
                                    var outputs = new List<string>();
                                    var metaoutputs = new List<string>();

                                    foreach (var column in columns)
                                    {
                                        if (column <= rowValues.ColumnValues.Count)
                                        {
                                            var literal = rowValues.ColumnValues[column] as Literal;

                                            //Convert NULL to empty string
                                            outputs.Add((literal.Value == "NULL") ? "" : literal.Value);
                                        }
                                    }

                                    foreach (var meta in metas)
                                    {
                                        var literal = rowValues.ColumnValues[meta] as Literal;

                                        //Convert NULL to empty string
                                        metaoutputs.Add((literal.Value == "NULL") ? "" : literal.Value);
                                    }

                                    //Ignore empty outputs and empty passwords
                                    if (outputs.Count > 0 && !string.IsNullOrEmpty(outputs[0]))
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

        private static int[] ParseColumns(InsertStatement insertStatement, InsertSpecification spec, IEnumerable<string> names)
        {
            var indexes = new List<int>();

            //Loop through the columns, and compare tokens
            foreach (var columnName in names)
            {
                var i = 0;
                foreach (var column in spec.Columns)
                {
                    var token = insertStatement.ScriptTokenStream[column.FirstTokenIndex];
                    var name = token.Text.Trim('"').Trim('[').Trim(']');

                    if (name.Contains(columnName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        indexes.Add(i);
                        break;
                    }
                    i++;
                }
            }

            return indexes.ToArray();
        }
    }
}
