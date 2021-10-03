using Microsoft.SqlServer.TransactSql.ScriptDom;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


namespace Malfoy
{
    [Plugin("sql")]
    public class SqlPlugin: PluginBase
    {
        public static void Process(SqlOptions options)
        {
            //Validate and display arguments
            var sqlFileEntries = Directory.GetFiles(Directory.GetCurrentDirectory(), options.InputPath);

            if (sqlFileEntries.Length == 0)
            {
                WriteError($"No .sql files found for {options.InputPath}.");
                return;
            }

            if (options.S2Mode) WriteMessage("Using S2 mode.");
            if (options.S3Mode) WriteMessage("Using S3 mode.");

            Console.WriteLine($"Started at {DateTime.Now.ToShortTimeString()}.");

            var columns = Array.ConvertAll(options.Columns.ToArray(), int.Parse);
            var metas = (options.MetaColumns.Count() == 0) ? new int[0] : Array.ConvertAll(options.MetaColumns.ToArray(), int.Parse);

            var size = GetFileEntriesSize(sqlFileEntries);
            var progressTotal = 0L;
            var lineCount = 0L;

            var fileOutput = new List<string>();
            var metaOutput = new List<string>();
            var exampleCount = 0;
            var debugCount = 0;

            var currentDirectory = Directory.GetCurrentDirectory();

            foreach (var sqlPath in sqlFileEntries)
            {
                WriteMessage($"Processing {sqlPath}.");

                var fileName = Path.GetFileNameWithoutExtension(sqlPath);
                var filePathName = $"{currentDirectory}\\{fileName}";
                var outputPath = $"{filePathName}-output.txt";
                var metapath = $"{filePathName}-meta.txt";

                //Check that there are no output files
                if (!CheckForFiles(new string[] { outputPath, metapath }))
                {
                    WriteHighlight($"Skipping {filePathName}.");
                    var fileInfo = new FileInfo(filePathName);
                    progressTotal += fileInfo.Length;

                    WriteProgress("Parsing sql", progressTotal, size);
                    continue;
                }

                fileOutput.Clear();
                metaOutput.Clear();
                lineCount = 0;
                exampleCount = 0;

                var parser = new TSql150Parser(false);

                using (var reader = new StreamReader(sqlPath))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine().TrimStart();

                        lineCount++;
                        progressTotal += line.Length;

                        if (line.StartsWith("INSERT") || (options.S2Mode && line.StartsWith("(")))
                        {
                            //Try hack out the quoted identifier for table names
                            if (!options.S2Mode) line = line.Replace(options.Table, "users");

                            if (options.S2Mode && !line.StartsWith("INSERT"))
                            {
                                line = "INSERT INTO USERS VALUES " + line;
                                if (line.EndsWith("),") || line.EndsWith(");")) line = line.Substring(0, line.Length - 1);
                            }

                            //Remove any mysql escape characters
                            line = line.Replace(@"\\", @"");//Using a single \ seems to cause issues

                            line = line.Replace(@"\'", "''");
                            line = line.Replace("\\\"", "\"");
                            line = line.Replace("\\t", "");
                            line = line.Replace("\\%", "%");
                            line = line.Replace("\\_", "_");

                            var statements = parser.ParseStatementList(new StringReader(line), out var errors);
                            if (errors.Count > 0)
                            {
                                if (!options.S2Mode) WriteMessage($"Line {lineCount},{errors[0].Offset}:{errors[0].Message}");
                                continue;
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
                                            var spec = insertStatement.InsertSpecification;

                                            var source = spec.InsertSource;
                                            if (source is ValuesInsertSource)
                                            {
                                                var valuesSource = source as ValuesInsertSource;

                                                foreach (var rowValues in valuesSource.RowValues)
                                                {
                                                    var outputs = new List<string>();
                                                    var metaoutputs = new List<string>();

                                                    //Do a line of debug
                                                    if (debugCount < 9 && options.Debug)
                                                    {
                                                        var debugColumn = 0;

                                                        WriteMessage("-- Row --");

                                                        foreach (var columnValue in rowValues.ColumnValues)
                                                        {
                                                            var literal = columnValue as Literal;
                                                            var literalValue = (literal.Value == "NULL") ? "" : literal.Value;

                                                            WriteMessage($"column {debugColumn}:{literalValue}");

                                                            debugColumn++;
                                                        }

                                                        
                                                        debugCount++;

                                                        //Just quit
                                                        if (debugCount > 8) return;                                                        
                                                    }

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

                                                        if (exampleCount < 9)
                                                        {
                                                            exampleCount++;
                                                            if (metaoutputs.Count > 0)
                                                            {
                                                                WriteMessage($"Example output: {result} [{metaresult}]");
                                                            }
                                                            else
                                                            {
                                                                WriteMessage($"Example output: {result}");
                                                            }
                                                        }
                                                    }

                                                    //Check if we need to flush the file output
                                                    if (fileOutput.Count >= 100000)
                                                    {
                                                        File.AppendAllLines(outputPath, fileOutput);
                                                        File.AppendAllLines(metapath, metaOutput);

                                                        fileOutput.Clear();
                                                        metaOutput.Clear();
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    WriteMessage($"Error line {lineCount}: {ex.Message}");
                                }
                            }
                        }

                        //Update the percentage
                        if (lineCount % 1000 == 0) WriteProgress("Parsing Sql", progressTotal, size);
                    }
                }

                //Write out file
                WriteMessage($"Finished writing to {fileName}-output.txt at {DateTime.Now.ToShortTimeString()}.");
                File.AppendAllLines(outputPath, fileOutput);
                File.AppendAllLines(metapath, metaOutput);
            }

            WriteMessage($"Completed at {DateTime.Now.ToShortTimeString()}.");
        }            
    }
}
