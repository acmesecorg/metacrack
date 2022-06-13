using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Metacrack
{
    public class ExportPlugin : PluginBase
    {
        public static void Process(ExportOptions options)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var lookupFileEntries = Directory.GetFiles(currentDirectory, options.LookupPath);

            if (lookupFileEntries.Length == 0)
            {
                WriteMessage($"Lookup file(s) for {options.LookupPath} not found.");
                return;
            }

            //Get user hashes input
            var hashFileEntries = Directory.GetFiles(currentDirectory, options.HashesPath);

            if (hashFileEntries.Length == 0)
            {
                WriteMessage($"Hashes {options.HashesPath} was not found.");
                return;
            }

            var removeHashes = new List<string>();
            var removeWords = new List<string>();

            if (options.RemoveHashesPath.Length > 0)
            {
                WriteMessage($"Removing hashes for {options.RemoveHashesPath}.");
                removeHashes.AddRange(File.ReadAllLines(options.RemoveHashesPath));
            }

            if (options.RemoveWordsPath.Length > 0)
            {
                WriteMessage($"Removing associated words for {options.RemoveWordsPath}.");
                removeWords.AddRange(File.ReadAllLines(options.RemoveWordsPath));

                //Ensure that the row counts are the same, otherwise cancel both remove operations
                if (removeHashes.Count() != removeWords.Count())
                {
                    WriteError($"Hash list count does not match associated word list count. Removal is disabled.");
                    removeHashes.Clear();
                    removeWords.Clear();
                }
            }

            var shucks = new Dictionary<string, string>();

            //Load and calculate shucking pairs
            if (options.ShuckPath.Length > 0)
            {
                WriteMessage($"Creating shuck pairs for {options.ShuckPath}.");

                using (var reader = new StreamReader(options.ShuckPath))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        shucks[HashMd5(line)] = line;
                    }
                }

                WriteMessage($"Created {shucks.Count()} shuck pairs for {options.ShuckPath}.");
            }

            //Load lookups into memory
            var lookups = new Dictionary<string, string>();
            var lineCount = 0;

            var size = GetFileEntriesSize(lookupFileEntries);
            var progressTotal = 0L;
            var useShucks = shucks.Count() > 0;

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

                            //Translate shucked result to plain or just add
                            //Only add if not already in file
                            if (useShucks && shucks.ContainsKey(plain))
                            {
                                lookups[hash] = shucks[plain];
                            }
                            else
                            {
                                lookups[hash] = plain;
                            }
                        }

                        //Update the percentage
                        if (lineCount % 1000 == 0) WriteProgress("Loading founds", progressTotal, size);
                    }
                }
            }
            
            WriteMessage($"Loaded {lookups.Keys.Count} lookups from {lineCount} lines in {lookupFileEntries.Count()} files.");

            var output = new List<string>();
            var found = new List<string>();
            var left = new List<string>();

            size = GetFileEntriesSize(hashFileEntries);

            var userHashCounts = 0;
            var fileUserHashCounts = 0;
            var founds = 0;
            var lefts = 0;

            progressTotal = 0L;

            foreach (var hashesPath in hashFileEntries)
            {
                var fileName = Path.GetFileNameWithoutExtension(hashesPath);
                var plainsPath = $"{currentDirectory}\\{fileName}.plains.txt"; //email:plain
                var foundPath = $"{currentDirectory}\\{fileName}.found.txt"; //hash:plain

                //Increment filename needs optimisation
                var leftPath = $"{currentDirectory}\\{IncrementFilename(fileName, "left")}.txt"; //hash

                //Check that there are no output files
                if (!CheckOverwrite(new string[] { plainsPath, foundPath, leftPath}))
                {
                    WriteHighlight($"Skipping {hashesPath}.");
                    continue;
                }

                output.Clear();
                found.Clear();
                left.Clear();

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


                        //Username + hash ( + salt)
                        if (splits.Length == 2 || splits.Length == 3)
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

                            if (lookups.TryGetValue(hash, out string plain))
                            {
                                founds++;
                                output.Add($"{user}:{plain}");

                                //Write out the founds on a per file basis
                                found.Add($"{originalHash}:{plain}");

                                //Sort out any removals
                                if (removeHashes.Count > 0)
                                {
                                    var index = removeHashes.IndexOf(originalHash);
                                    
                                    if (index > -1)
                                    {
                                        removeHashes.RemoveAt(index);
                                        if (removeWords.Count > 0) removeWords.RemoveAt(index);
                                    }
                                }
                            }
                            else
                            {
                                lefts++;
                                left.Add(line);
                            }
                        }

                        //Update the percentage
                        if (counter % 1000 == 0) WriteProgress("Processing hashes", progressTotal, size);

                        //Check if we need to write out 
                        if (counter > 1000000)
                        {
                            counter = 0;
                            File.AppendAllLines(plainsPath, output);
                            File.AppendAllLines(foundPath, found);
                            File.AppendAllLines(leftPath, left);

                            output.Clear();
                            found.Clear();
                            left.Clear();
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

                    //Write out the removed hashes and words
                    if (removeHashes.Count > 0)
                    {
                        File.Delete(options.RemoveHashesPath);
                        File.AppendAllLines(options.RemoveHashesPath, removeHashes);

                        if (removeWords.Count > 0)
                        {
                            File.Delete(options.RemoveWordsPath);
                            File.AppendAllLines(options.RemoveWordsPath, removeWords);
                        }
                    }
                }
            }

            if (hashFileEntries.Count() > 1) WriteMessage($"Got {founds} founds from {userHashCounts} hashes in {hashFileEntries.Count()} files.");
        }
    }
}
