using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Malfoy
{
    public static class Append
    {
        public static void Process(string currentDirectory, string[] args)
        {
            string arg = args[0];
            string suffix = args[2];

            //Get user hashes / json input path
            var appendFileEntries = Directory.GetFiles(currentDirectory, arg);

            if (appendFileEntries.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No input file(s) not found.");
                Console.ResetColor();
                return;
            }

            Console.WriteLine($"Started at {DateTime.Now.ToShortTimeString()}.");

            var size = Common.GetFileEntriesSize(appendFileEntries);
            var progressTotal = 0L;
            var appendOutput = new List<string>();

            using (var progress = new ProgressBar(false))
            {
                foreach (var appendPath in appendFileEntries)
                {
                    progress.WriteLine($"Processing {appendPath}.");

                    var fileName = Path.GetFileNameWithoutExtension(appendPath);
                    var filePathName = $"{currentDirectory}\\{fileName}";
                    var outputFoundPath = $"{filePathName}-append.txt";

                    //Check that there are no output files
                    if (!Common.CheckForFiles(new string[] { outputFoundPath }))
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

                    appendOutput.Clear();
                    long lineCount = 0;

                    using (var reader = new StreamReader(appendPath))
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();

                            lineCount++;
                            progressTotal += line.Length;

                            appendOutput.Add($"{line}{suffix}");

                            if (lineCount == 1000000)
                            {
                                lineCount = 0;
                                File.AppendAllLines(outputFoundPath, appendOutput);

                                appendOutput.Clear();
                            }

                            //Update the percentage
                            progress.Report((double)progressTotal / size);
                        }
                    }

                    progress.Pause();

                    //Write out file
                    progress.WriteLine($"Finished writing files at {DateTime.Now.ToShortTimeString()}.");
                    File.AppendAllLines(outputFoundPath, appendOutput);
                }

                progress.WriteLine($"Completed at {DateTime.Now.ToShortTimeString()}.");
            }
        }
    }
}
