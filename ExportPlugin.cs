using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;

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

            var outputs = new Dictionary<string, List<string>>();
            var sources = new Dictionary<string, string>();
            var hasSources = false;

            //Add default output
            outputs.Add("", new List<string>());

            //Load source information
            if (options.SourcePath.Length > 0)
            {
                WriteMessage($"Reading source information for {options.SourcePath}.");

                using (var reader = new StreamReader(options.SourcePath))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        var splits = line.Split(new[] {':'}, 2);
                        var source = splits[0];

                        if (!outputs.ContainsKey(source))
                        {
                            outputs[source] = new List<string>();
                            hasSources = true;
                        }

                        sources[splits[1]] = source;
                    }
                }

                WriteMessage($"Got {outputs.Keys.Count() - 1} sources for {options.SourcePath}.");
            }

            //Load lookups into memory
            var lookups = new Dictionary<string, string>();
            var lineCount = 0;
            var errorCount = 0;

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

                        var line = reader.ReadLineAsHashPlain(options.IgnoreSalt);

                        progressTotal += line.Length;

                        //if (line.Hash == "$2y$10$II42yolK86Cva9ywSeDtyOKyqgDk9FDQdjOsrJT8Yxy/OFQLvbZKC") progressTotal = progressTotal + 0;

                        //Hash is not null if line was read correctly
                        if (line.Hash != null)
                        {
                            //Translate shucked result to plain or just add
                            //Only add if not already in file
                            if (useShucks && shucks.ContainsKey(line.Plain))
                            {
                                lookups[line.Hash] = shucks[line.Plain];
                            }
                            else
                            {
                                lookups[line.Hash] = line.Plain;
                            }
                        }
                        else
                        {
                            errorCount++;
                        }

                        //Update the percentage
                        if (lineCount % 1000 == 0) WriteProgress("Loading founds", progressTotal, size);
                    }
                }
            }
             
            WriteMessage($"Loaded {lookups.Keys.Count} lookups from {lineCount} lines ({errorCount} errors) in {lookupFileEntries.Count()} files.");

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
                var leftPath = $"{currentDirectory}\\{IncrementFilename(fileName, "left")}.txt"; //hash

                //Check that there are no output files
                if (!CheckForFiles(new string[] { plainsPath, foundPath, leftPath}))
                {
                    WriteHighlight($"One or more output files exist. Skipping {hashesPath}.");
                    continue;
                }

                //Check outputs from sources paths
                if (hasSources)
                {
                    var paths = new List<string>();
                    foreach (var key in outputs.Keys)
                    {
                        if (key == "") continue;
                        paths.Add($"{currentDirectory}\\{key}.{fileName}.plains.txt");
                    }

                    if (!CheckForFiles(paths.ToArray()))
                    {
                        WriteHighlight($"One or more source output files exist. Skipping {hashesPath}.");
                        continue;
                    }
                }

                //Clear
                foreach (var output in outputs.Values) output.Clear();
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

                        var line = reader.ReadLineAsEmailHash();

                        progressTotal += line.Text.Length;

                        //Username + hash ( + salt)
                        if (line.FullHash != null)
                        {
                            //if (line.Text.Contains("$2y$10$II42yolK86Cva9ywSeDtyOKyqgDk9FDQdjOsrJT8Yxy/OFQLvbZKC")) progressTotal = progressTotal + 0;

                            if (lookups.TryGetValue(line.FullHash, out string plain))
                            {
                                founds++;

                                //Look to see if we need to get a source
                                if (hasSources && sources.TryGetValue($"{line.Email}:{line.FullHash}", out var source))
                                {
                                    outputs[source].Add($"{line.Email}:{plain}");
                                }
                                else
                                {
                                    outputs[""].Add($"{line.Email}:{plain}");
                                }

                                //Write out the founds on a per file basis
                                found.Add($"{line.FullHash}:{plain}");

                                //Sort out any removals
                                if (removeHashes.Count > 0)
                                {
                                    //The index in the hash file should match the index in the word file
                                    var index = removeHashes.IndexOf(line.FullHash);
                                    
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
                                left.Add(line.Text);
                            }
                        }

                        //Update the percentage
                        if (counter % 1000 == 0) WriteProgress("Processing hashes", progressTotal, size);

                        //Check if we need to write out 
                        if (counter > 1000000)
                        {
                            counter = 0;
                            if (outputs[""].Count() > 0) File.AppendAllLines(plainsPath, outputs[""]);
                            if (found.Count() > 0) File.AppendAllLines(foundPath, found);
                            if (left.Count() > 0) File.AppendAllLines(leftPath, left);

                            if (hasSources)
                            {
                                foreach (var de in outputs)
                                {
                                    if (de.Key == "") continue;
                                    if (de.Value.Count() == 0) continue;
                                    
                                    var sourcePlainsPath = $"{currentDirectory}\\{de.Key}.{fileName}.plains.txt";
                                    File.AppendAllLines(sourcePlainsPath, de.Value);
                                }
                            }

                            foreach (var output in outputs.Values) output.Clear();
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
                    if (outputs[""].Count() > 0) File.AppendAllLines(plainsPath, outputs[""]);
                    if (found.Count() > 0) File.AppendAllLines(foundPath, found);
                    if (left.Count() > 0) File.AppendAllLines(leftPath, left);

                    if (hasSources)
                    {
                        foreach (var de in outputs)
                        {
                            if (de.Key == "") continue;
                            if (de.Value.Count() == 0) continue;

                            var sourcePlainsPath = $"{currentDirectory}\\{de.Key}.{fileName}.plains.txt";
                            File.AppendAllLines(sourcePlainsPath, de.Value);
                        }
                    }

                    //Write out the removed hashes and words as a new file
                    if (removeHashes.Count > 0)
                    {
                        var removeHashesFileName = Path.GetFileNameWithoutExtension(options.RemoveHashesPath);
                        var removeHashesNewPath = $"{currentDirectory}\\{IncrementFilename(removeHashesFileName, "left")}.hash"; //hash

                        File.AppendAllLines(removeHashesNewPath, removeHashes);

                        if (removeWords.Count > 0)
                        {
                            var removeWordsFileName = Path.GetFileNameWithoutExtension(options.RemoveWordsPath);
                            var removeWordsNewPath = $"{currentDirectory}\\{IncrementFilename(removeWordsFileName, "left")}.word"; //word

                            File.AppendAllLines(removeWordsNewPath, removeWords);
                        }
                    }
                }
            }

            if (hashFileEntries.Count() > 1) WriteMessage($"Got {founds} founds from {userHashCounts} hashes in {hashFileEntries.Count()} files.");
        }
    }
}
