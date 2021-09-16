using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Malfoy
{
    public static class Map
    {
        public static void Process(string currentDirectory, string[] args)
        {
            var arg = args[0];
            var source = args[1];

            //Get user hashes / json input path
            var fileEntries = Directory.GetFiles(currentDirectory, arg);

            if (fileEntries.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No input file(s) found.");
                Console.ResetColor();
                return;
            }

            var sourceFiles = Directory.GetFiles(currentDirectory, source);

            if (sourceFiles.Count() == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                if (sourceFiles.Count() == 0) Console.WriteLine("No source files found.");

                Console.ResetColor();
                return;
            }

            Console.WriteLine($"Started at {DateTime.Now.ToShortTimeString()}.");

            //Load the firstnames or other items used for stemming into a hashset
            var lookups = new HashSet<string>();
            var lineCount = 0;

            var size = Common.GetFileEntriesSize(sourceFiles);
            var progressTotal = 0L;
            var version = "";

            using (var progress = new ProgressBar(false))
            {
                foreach (var lookupPath in sourceFiles)
                {
                    if (version =="")
                    {
                        version = Path.GetFileNameWithoutExtension(lookupPath);
                        if (sourceFiles.Count() > 1) version += $"[{sourceFiles.Count()}]";
                    }

                    using (var reader = new StreamReader(lookupPath))
                    {
                        while (!reader.EndOfStream)
                        {
                            lineCount++;

                            var line = reader.ReadLine();

                            if (line.Length < 70) lookups.Add(line);

                            //Update the percentage
                            progress.Report((double)progressTotal / size);
                        }
                    }
                }
            }

            size = Common.GetFileEntriesSize(fileEntries);
            progressTotal = 0L;
            lineCount = 0;

            using (var progress = new ProgressBar(false))
            { 
                foreach (var filePath in fileEntries)
                {
                    progress.WriteLine($"Processing {filePath}.");

                    //Create a version based on the file size, so that the hash and dict are bound together
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var filePathName = $"{currentDirectory}\\{fileName}";
                    var outputHashPath = $"{filePathName}.{version}.hash";
                    var outputDictPath = $"{filePathName}.{version}.dict";

                    //Check that there are no output files
                    if (!Common.CheckForFiles(new string[] { outputHashPath,outputDictPath }))
                    {
                        progress.Pause();

                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine($"Skipping {filePathName}.");
                        Console.ResetColor();

                        var fileInfo = new FileInfo(filePath);
                        progressTotal += fileInfo.Length;

                        progress.Resume();
                        progress.Report((double)progressTotal / size);

                        continue;
                    }

                    var hashes = new List<string>();
                    var dicts = new List<string>();

                    //Loop through and check if each email contains items from the lookup, if so add them
                    using (var reader = new StreamReader(filePath))
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();

                            lineCount++;
                            progressTotal += line.Length;

                            var splits = line.Split(':');

                            //Just very basic validation
                            if (splits.Length == 2 || splits.Length == 3)
                            {
                                //Just add every item in the lookups
                                foreach (var final in lookups)
                                {
                                    hashes.Add(line);
                                    dicts.Add(final);
                                }
                            }

                            //Update the percentage
                            progress.Report((double)progressTotal / size);
                        }
                    }

                    if (hashes.Count > 0)
                    {
                        File.AppendAllLines(outputHashPath, hashes);
                        File.AppendAllLines(outputDictPath, dicts);
                    }
                }

                progress.WriteLine($"Completed at {DateTime.Now.ToShortTimeString()}.");
            }
        }
    }
}
