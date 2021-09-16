using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Malfoy
{
    public static class Export
    {
        public static bool Ida { get; set; } 

        public static void Process(string currentDirectory, string[] args)
        {
            var foundMode = Common.GetCommandLineArgument(args, -1, "f") != null;
            var nosalt = Common.GetCommandLineArgument(args, -1, "-nosalt") != null;
            var ignoresalt = Common.GetCommandLineArgument(args, -1, "-ignoresalt") != null;
            var base64 = Common.GetCommandLineArgument(args, -1, "-base64") != null;

            var lookupFileEntries = Directory.GetFiles(currentDirectory, args[1]);

            if (lookupFileEntries.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Lookup file {args[1]} was not found.");
                Console.ResetColor();
                return;
            }

            //Get user hashes / json input path
            var hashFileEntries = Directory.GetFiles(currentDirectory, args[0]);

            if (hashFileEntries.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;

                var message = $"User hashes file {args[0]} was not found.";

                Console.WriteLine(message);
                Console.ResetColor();
                return;
            }

            //Load lookups into memory
            Console.WriteLine("Loading lookups into memory.");

            var lookups = new Dictionary<string, string>();
            var lineCount = 0;

            var size = Common.GetFileEntriesSize(lookupFileEntries);
            var progressTotal = 0L;

            using (var progress = new ProgressBar(false))
            {
                foreach (var lookupPath in lookupFileEntries)
                {
                    using (var reader = new StreamReader(lookupPath))
                    {
                        while (!reader.EndOfStream)
                        {
                            lineCount++;

                            var line = reader.ReadLine();
                            var splits = line.Split(':');
                            progressTotal += line.Length;

                            if (splits.Length == 2 || splits.Length == 3)
                            {
                                var hash = splits[0].ToLower();
                                var plain = "";

                                //Throw away any hash identifier eg MD5 ABCXXX
                                if (hash.Contains(' '))
                                {
                                    var newSplits = hash.Split(' ');
                                    hash = newSplits[1];
                                }

                                if (splits.Length == 3)
                                {
                                    if (!ignoresalt) hash = $"{hash}:{splits[1]}";
                                    plain = splits[2];
                                }
                                else
                                {
                                    plain = splits[1];
                                }

                                //Only add if not already in file
                                lookups[hash] = plain;
                            }

                            //Update the percentage
                            progress.Report((double)progressTotal / size);
                        }
                    }
                }
            }

            Console.WriteLine($"Loaded {lookups.Keys.Count} lookups from {lineCount} lines in {lookupFileEntries.Count()} files.");

            var output = new List<string>();
            var found = new List<string>();
            var left = new List<string>();
            var lefthash = new List<string>();

            //Load up user  hashes
            Console.WriteLine("Processing hashes.");

            size = Common.GetFileEntriesSize(hashFileEntries);

            var userHashCounts = 0;
            var fileUserHashCounts = 0;
            var founds = 0;
            var lefts = 0;

            progressTotal = 0L;

            using (var progress = new ProgressBar(false))
            {
                foreach (var hashesPath in hashFileEntries)
                {
                    var fileName = Path.GetFileNameWithoutExtension(hashesPath);
                    var filePathName = $"{currentDirectory}\\{fileName}";

                    var plainsPath = $"{filePathName}-plains.txt";
                    var foundPath = $"{filePathName}-found.txt";
                    var leftPath = $"{filePathName}-left.txt";
                    var hashPath = $"{filePathName}-hash.txt";

                    progress.UpdateText("");

                    //Check that there are no output files
                    if (!Common.CheckForFiles(new string[] { plainsPath, foundPath, leftPath, hashPath }))
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine($"Skipping {hashesPath}.");
                        Console.ResetColor();

                        continue;
                    }

                    output.Clear();
                    found.Clear();
                    left.Clear();
                    lefthash.Clear();

                    var counter = 0;

                    using (var reader = new StreamReader(hashesPath))
                    {
                        fileUserHashCounts = 0;

                        while (!reader.EndOfStream)
                        {
                            fileUserHashCounts++;
                            userHashCounts++;
                            counter++;

                            var line = reader.ReadLine();
                            var splits = line.Split(':');

                            if (line.Contains("C1OZ53GEyrvZ5HLB6H92wyIwJist")) progressTotal += 0;

                            progressTotal += line.Length;

                            //Remove second @ in email
                            if (Ida)
                            {
                                var atSplits = splits[0].Split('@');
                                if (atSplits.Length > 1) splits[0] = $"{atSplits[0]}@{atSplits[1]}";
                            }

                            //Found mode
                            if (foundMode)
                            {
                                if (splits.Length == 1 || splits.Length == 2)
                                {
                                    var originalHash = splits[0];
                                    var hash = splits[0].ToLower();

                                    //Check for salt
                                    if (splits.Length == 2 && !nosalt)
                                    {
                                        hash = $"{hash}:{splits[1]}";
                                        originalHash = $"{originalHash}:{splits[1]}";
                                    }

                                    if (lookups.TryGetValue(hash, out string plain))
                                    {
                                        founds++;

                                        //Write out the founds on a per file basis
                                        found.Add($"{originalHash}:{plain}");
                                    }
                                    else
                                    {
                                        lefts++;
                                        lefthash.Add(originalHash);
                                    }

                                }
                            }

                            //Username + hash ( + salt)
                            else if (splits.Length == 2 || splits.Length == 3)
                            {
                                var user = splits[0];
                                var originalHash = splits[1];
                                var hash = splits[1].ToLower();


                                //Check for salt
                                if (splits.Length == 3 && !nosalt)
                                {
                                    hash = $"{hash}:{splits[2]}";
                                    originalHash = $"{originalHash}:{splits[2]}";
                                }

                                //Check hash for base64 encoding
                                if (base64)
                                {
                                    if (hash.EndsWith("="))
                                    {
                                        var bytes = Convert.FromBase64String(originalHash);
                                        hash = BitConverter.ToString(bytes).Replace("-", "").ToLower();
                                        originalHash = hash;
                                    }
                                }

                                if (lookups.TryGetValue(hash, out string plain))
                                {
                                    founds++;
                                    output.Add($"{user}:{plain}");

                                    //Write out the founds on a per file basis
                                    found.Add($"{originalHash}:{plain}");
                                }
                                else
                                {
                                    lefts++;
                                    left.Add(line);
                                    lefthash.Add(originalHash);
                                }
                            }

                            //Update the percentage
                            progress.Report((double)progressTotal / size);

                            //Check if we need to write out 
                            if (counter > 1000000 && found.Count > 0)
                            {
                                counter = 0;
                                File.AppendAllLines(plainsPath, output);
                                File.AppendAllLines(foundPath, found);
                                File.AppendAllLines(leftPath, left);
                                File.AppendAllLines(hashPath, lefthash);

                                output.Clear();
                                found.Clear();
                                left.Clear();
                                lefthash.Clear();
                            }
                        }
                    }

                    var perc = ((double)founds / fileUserHashCounts * 100).ToString("#.##");

                    progress.WriteLine($"Found {founds} out of {fileUserHashCounts} ({perc}%) with {left.Count()} hashes left.");

                    //Write out file
                    if (found.Count > 0)
                    {
                        File.AppendAllLines(plainsPath, output);
                        File.AppendAllLines(foundPath, found);
                        File.AppendAllLines(leftPath, left);
                        File.AppendAllLines(hashPath, lefthash);
                    }
                }
            }

            if (hashFileEntries.Count() > 1) Console.WriteLine($"Got {founds} founds from {userHashCounts} hashes in {hashFileEntries.Count()} files.");
        }

    }
}
