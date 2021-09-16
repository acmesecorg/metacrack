using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Malfoy
{
    public static class Hash
    {
        public static bool Ida { get; set; }

        public static void Process(string currentDirectory, string[] args)
        {
            var arg = args[0];
            var mode = args[2];

            //Get user hashes / json input path
            var modeFileEntries = Directory.GetFiles(currentDirectory, arg);

            if (modeFileEntries.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No input file(s) found.");
                Console.ResetColor();
                return;
            }

            Console.WriteLine($"Started at {DateTime.Now.ToShortTimeString()}.");

            var size = Common.GetFileEntriesSize(modeFileEntries);
            var progressTotal = 0L;
            int hashLength;

            //Determine values based on mode
            if (mode == "0")
            {
                hashLength = 32;
            }
            else if (mode == "100")
            {
                hashLength = 40;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Mode {mode} no supported.");
                Console.ResetColor();
                return;
            }

            var foundOutput = new List<string>();
            var notFoundOutput = new List<string>();

            using (var progress = new ProgressBar(false))
            {
                foreach (var modePath in modeFileEntries)
                {
                    progress.WriteLine($"Processing {modePath}.");

                    var fileName = Path.GetFileNameWithoutExtension(modePath);
                    var filePathName = $"{currentDirectory}\\{fileName}";
                    var outputFoundPath = $"{filePathName}-mode{mode}.txt";
                    var outputNotFoundPath = $"{filePathName}-nomode.txt";

                    //Check that there are no output files
                    if (!Common.CheckForFiles(new string[] { outputFoundPath, outputNotFoundPath }))
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

                    foundOutput.Clear();
                    notFoundOutput.Clear();
                    long lineCount = 0;

                    using (var reader = new StreamReader(modePath))
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();

                            lineCount++;
                            progressTotal += line.Length;

                            var splits = line.Split(':');

                            if (splits.Length == 2 || splits.Length == 3)
                            {
                                var hash = splits[1];

                                //Check for salt
                                //if (splits.Length == 3 && !nosalt) hash = $"{hash}:{splits[2]}";

                                //Remove second @ in email
                                if (Ida)
                                {
                                    var atSplits = splits[0].Split('@');
                                    if (atSplits.Length > 2) splits[0] = $"{atSplits[0]}@{atSplits[1]}";
                                }

                                if (Common.IsHash(hash, hashLength))
                                {
                                    foundOutput.Add(string.Join(":", splits));
                                }
                                else
                                {
                                    notFoundOutput.Add(string.Join(":", splits));
                                }
                            }

                            if (lineCount == 1000000)
                            {
                                lineCount = 0;
                                File.AppendAllLines(outputFoundPath, foundOutput);
                                File.AppendAllLines(outputNotFoundPath, notFoundOutput);

                                foundOutput.Clear();
                                notFoundOutput.Clear();
                            }

                            //Update the percentage
                            progress.Report((double)progressTotal / size);
                        }
                    }

                    progress.Pause();

                    //Write out file
                    progress.WriteLine($"Finished writing files at {DateTime.Now.ToShortTimeString()}.");
                    File.AppendAllLines(outputFoundPath, foundOutput);
                    File.AppendAllLines(outputNotFoundPath, notFoundOutput);
                }

                progress.WriteLine($"Completed at {DateTime.Now.ToShortTimeString()}.");
            }
        }
    }
}
