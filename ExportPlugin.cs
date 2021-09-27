using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Malfoy
{
    public class ExportPlugin : PluginBase
    {
        public static void Process(ExportOptions options)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var lookupFileEntries = Directory.GetFiles(currentDirectory, options.LookupPath);

            if (lookupFileEntries.Length == 0)
            {
                WriteMessage($"Lookup file(s) {options.LookupPath} was not found.");
                return;
            }

            //Get user hashes input
            var hashFileEntries = Directory.GetFiles(currentDirectory, options.HashesPath);

            if (hashFileEntries.Length == 0)
            {
                WriteMessage($"Hashes {options.HashesPath} was not found.");
                return;
            }

            //Load lookups into memory
            var lookups = new Dictionary<string, string>();
            var lineCount = 0;

            var size = GetSizeOfEntries(lookupFileEntries);
            var progressTotal = 0L;

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
                                if (!options.IgnoreSalt) hash = $"{hash}:{splits[1]}";
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
                        WriteProgress("Loading lookups", progressTotal, size);
                    }
                }
            }
            
            WriteMessage($"Loaded {lookups.Keys.Count} lookups from {lineCount} lines in {lookupFileEntries.Count()} files.");

            var output = new List<string>();
            var found = new List<string>();
            var left = new List<string>();
            var lefthash = new List<string>();

            size = GetSizeOfEntries(hashFileEntries);

            var userHashCounts = 0;
            var fileUserHashCounts = 0;
            var founds = 0;
            var lefts = 0;

            progressTotal = 0L;

            foreach (var hashesPath in hashFileEntries)
            {
                var fileName = Path.GetFileNameWithoutExtension(hashesPath);
                var filePathName = $"{currentDirectory}\\{fileName}";

                var plainsPath = $"{filePathName}-plains.txt";
                var foundPath = $"{filePathName}-found.txt";
                var leftPath = $"{filePathName}-left.txt";
                var hashPath = $"{filePathName}-hash.txt";

                //Check that there are no output files
                if (!CheckForFiles(new string[] { plainsPath, foundPath, leftPath, hashPath }))
                {
                    WriteHighlight($"Skipping {hashesPath}.");
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

                        progressTotal += line.Length;

                        //Found mode
                        if (options.FoundMode)
                        {
                            if (splits.Length == 1 || splits.Length == 2)
                            {
                                var originalHash = splits[0];
                                var hash = splits[0].ToLower();

                                //Check for salt
                                if (splits.Length == 2 && !options.NoSalt)
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
                            if (splits.Length == 3 && !options.NoSalt)
                            {
                                hash = $"{hash}:{splits[2]}";
                                originalHash = $"{originalHash}:{splits[2]}";
                            }

                            //Check hash for base64 encoding
                            if (options.Base64)
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
                        WriteProgress("Processing hashes.", progressTotal, size);

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

                WriteMessage($"Found {founds} out of {fileUserHashCounts} ({perc}%) with {lefts} hashes left.");

                //Write out file
                if (found.Count > 0)
                {
                    File.AppendAllLines(plainsPath, output);
                    File.AppendAllLines(foundPath, found);
                    File.AppendAllLines(leftPath, left);
                    File.AppendAllLines(hashPath, lefthash);
                }
            }

            if (hashFileEntries.Count() > 1) WriteMessage($"Got {founds} founds from {userHashCounts} hashes in {hashFileEntries.Count()} files.");
        }
    }
}
