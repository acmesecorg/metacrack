using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Malfoy
{
    public static class Count
    {
        public static void Process(string currentDirectory, string[] args)
        {
            var arg = args[0];

            //Get user hashes / json input path
            var countFileEntries = Directory.GetFiles(currentDirectory, arg);

            if (countFileEntries.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No input file(s) found.");
                Console.ResetColor();
                return;
            }

            Console.WriteLine($"Started at {DateTime.Now.ToShortTimeString()}.");

            var size = Common.GetFileEntriesSize(countFileEntries);
            var progressTotal = 0L;
            var founds = new Dictionary<string, long>();

            using (var progress = new ProgressBar(false))
            {
                foreach (var countPath in countFileEntries)
                {
                    progress.WriteLine($"Processing {countPath}.");

                    var fileName = Path.GetFileNameWithoutExtension(countPath);
                    var filePathName = $"{currentDirectory}\\{fileName}";
                    var outputFoundPath = $"{filePathName}-count.txt";

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

                    founds.Clear();
                    long lineCount = 0;

                    using (var reader = new StreamReader(countPath))
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();

                            lineCount++;
                            progressTotal += line.Length;

                            //Just use the last split
                            var splits = line.Split(':');
                            var item = splits.Last();

                            //Increment
                            if (founds.ContainsKey(item))
                            {
                                founds[item]++;
                            }
                            else
                            {
                                founds.Add(item, 1);
                            }

                            //Update the percentage
                            progress.Report((double)progressTotal / size);
                        }
                    }

                    progress.Pause();

                    progress.WriteLine($"Sorting output.");
                    var foundsSorted = founds.OrderByDescending(d => d.Value).ToList();
                    var finalOutput = new List<string>();
                    
                    foreach (var de in foundsSorted) finalOutput.Add($"{de.Key}:{de.Value}");

                    //Sort and write out file in descending order
                    progress.WriteLine($"Finished writing files at {DateTime.Now.ToShortTimeString()}.");
                    
                    File.AppendAllLines(outputFoundPath, finalOutput);
                }

                progress.WriteLine($"Completed at {DateTime.Now.ToShortTimeString()}.");
            }
        }
    }
}
