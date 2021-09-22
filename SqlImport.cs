using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Malfoy
{
    public static class SqlImport
    {
        public static bool S2Mode { get; set; }
        public static bool S3Mode { get; set; }

        public static void Process(string currentDirectory, string[] args)
        {
            string arg = args[0];
            string columnsarg = args[2];
            string metasarg = "";
            
            if (args.Length > 3) metasarg = args[3];

            //Get user hashes / json input path
            var sqlFileEntries = Directory.GetFiles(currentDirectory, arg);

            if (sqlFileEntries.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No SQL input file(s) not found.");
                Console.ResetColor();
                return;
            }

            string table = "`users`";

            if (args.Length > 4)
            {
                table = args[4];
                Console.WriteLine($"Using table name {table}.");
            }

            Console.WriteLine($"Started at {DateTime.Now.ToShortTimeString()}.");

            var columnsplits = columnsarg.Split(',');
            var columns = Array.ConvertAll(columnsplits, int.Parse);

            var metasplits = metasarg.Split(',');
            var metas = Array.ConvertAll(metasplits, int.Parse);

            var size = Common.GetFileEntriesSize(sqlFileEntries);
            var progressTotal = 0L;
            var lineCount = 0L;

            var fileOutput = new List<string>();
            var metaOutput = new List<string>();

            using (var progress = new ProgressBar(false))
            {
                var exampleCount = 0;

                foreach (var sqlPath in sqlFileEntries)
                {
                    progress.WriteLine($"Processing {sqlPath}.");

                    var fileName = Path.GetFileNameWithoutExtension(sqlPath);
                    var filePathName = $"{currentDirectory}\\{fileName}";
                    var outputPath = $"{filePathName}-output.txt";
                    var metapath = $"{filePathName}-meta.txt";

                    //Check that there are no output files
                    if (!Common.CheckForFiles(new string[] { outputPath, metapath }))
                    {
                        progress.Pause();

                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine($"Skipping {filePathName}.");
                        Console.ResetColor();

                        var fileInfo = new FileInfo(filePathName);
                        progressTotal += fileInfo.Length;

                        progress.Resume();
                        progress.Report((double)progressTotal / size);

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

                            if (line.StartsWith("INSERT") || (S2Mode && line.StartsWith("(")))
                            {
                                //Try hack out the quoted identifier for table names
                                if (!S2Mode) line = line.Replace(table, "users");

                                if (S2Mode && !line.StartsWith("INSERT"))
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
                                    if (!S2Mode) progress.WriteLine($"Line {lineCount},{errors[0].Offset}:{errors[0].Message}");

                                    //var outputErrors = new List<string>();
                                    //var errorPath = $"{filePathName}-errors.txt";
                                    //outputErrors.Clear();
                                    //outputErrors.Add($"Line {lineCount},{errors[0].Offset}:{errors[0].Message}");
                                    //outputErrors.Add(line);
                                    //File.Delete(errorPath);
                                    //File.AppendAllLines(errorPath, outputErrors);

                                    continue;
                                }

                                //TODO: put in a try catch so we dont loose the whole thing
                                if (statements != null)
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
                                                        fileOutput.Add(result);

                                                        var metaresult = string.Join(":", metaoutputs);

                                                        //Put the email in front of the metas
                                                        metaOutput.Add($"{outputs[0]}:{metaresult}");


                                                        if (exampleCount < 9)
                                                        {
                                                            exampleCount++;
                                                            if (metaoutputs.Count > 0)
                                                            {
                                                                progress.WriteLine($"Example output: {result} [{metaresult}]");
                                                            }
                                                            else
                                                            {
                                                                progress.WriteLine($"Example output: {result}");
                                                            }                                                           
                                                        }
                                                    }

                                                    //Check if we need to flush the file output
                                                    if (fileOutput.Count >= 1000000)
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
                            }

                            //Update the percentage
                            progress.Report((double)progressTotal / size);
                        }
                    }

                    progress.Pause();

                    //Write out file
                    progress.WriteLine($"Finished writing to {fileName}-output.txt at {DateTime.Now.ToShortTimeString()}.");
                    File.AppendAllLines(outputPath, fileOutput);
                    File.AppendAllLines(metapath, metaOutput);
                }

                progress.WriteLine($"Completed at {DateTime.Now.ToShortTimeString()}.");
            }
        }

    }
}
