using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Malfoy
{
    public class CatalogPlugin : PluginBase
    {
        public static void Process(CatalogOptions options)
        {
            //Validate and display arguments
            var currentDirectory = Directory.GetCurrentDirectory();
            var fileEntries = Directory.GetFiles(currentDirectory, options.InputPath, SearchOption.AllDirectories);

            if (fileEntries.Length == 0)
            {
                WriteError($"No .txt files found for {options.InputPath}");
                return;
            }

            if (!Directory.Exists(options.OutputFolder))
            {
                WriteError($"Output folder {options.OutputFolder} was not found.");
                return;
            }

            if (options.Tokenize && options.StemEmailOnly)
            {
                WriteError("Cannot use --tokenize and --stem-email-only options together.");
                return;
            }

            if (options.StemEmail && options.StemEmailOnly)
            {
                WriteError("Cannot use --stem-email and --stem-email-only options together.");
                return;
            }

            WriteMessage($"Using prefix {options.Prefix}");

            if (!options.NoOptimize) WriteMessage("Optimize enabled");
            if (options.Tokenize) WriteMessage("Tokenize enabled");
            if (options.StemEmail) WriteMessage("Stem email enabled");
            if (options.StemEmailOnly) WriteMessage("Stem email only enabled");

            //Determine columns;
            int[] columns = (options.Columns.Count() == 0) ? new int[] {1} : Array.ConvertAll(options.Columns.ToArray(), s => int.Parse(s));

            WriteMessage($"Using columns {String.Join(',', columns)}");

            //Get names input (if any)
            var sourceFiles = new string[] { };

            if (!string.IsNullOrEmpty(options.NamesPath)) sourceFiles = Directory.GetFiles(currentDirectory, options.NamesPath);

            if (sourceFiles.Length > 0)
            {
                if (sourceFiles.Length == 1) WriteMessage($"Using names source file {sourceFiles[0]}");
                if (sourceFiles.Length > 1) WriteMessage($"Using {sourceFiles.Length} names source files");
            }

            //Load the firstnames or other items used for stemming into a hashset
            var lookups = new HashSet<string>();
            var lineCount = 0L;

            var size = GetFileEntriesSize(sourceFiles);
            var progressTotal = 0L;

            foreach (var lookupPath in sourceFiles)
            {
                using (var reader = new StreamReader(lookupPath))
                {
                    while (!reader.EndOfStream)
                    {
                        lineCount++;

                        var line = reader.ReadLine();
                        progressTotal += line.Length + 1;

                        //We add teh lower case version for comparison only
                        if (line.Length >= 3 && line.Length < 70) lookups.Add(line.ToLower());

                        //Update the percentage
                        if (lineCount % 1000 == 0) WriteProgress("Loading names", progressTotal, size);
                    }
                }
            }

            //Get files
            var fileEntriesSize = GetFileEntriesSize(fileEntries);

            WriteMessage($"Found {fileEntries.Count()} text file entries ({FormatSize(fileEntriesSize)}) in all folders.");

            progressTotal = 0L;
            lineCount = 0L;
            var validCount = 0L;
            var fileCount = 0;

            WriteMessage($"Started adding values at {DateTime.Now.ToShortTimeString()}.");

            //Create 256 buckets to contain information for each file
            var buckets = new Dictionary<string, List<string>>(256);

            foreach (var hex1 in Hex)
            {
                foreach (var hex2 in Hex)
                {
                    buckets.Add($"{hex1}{hex2}", new List<string>());
                }
            }

            #pragma warning disable SYSLIB0021
            //We keep using Sha1Managed for performance reasons
            using (var sha1 = new SHA1Managed())
            {
                //Process a file
                foreach (var lookupPath in fileEntries)
                {
                    fileCount++;

                    using (var reader = new StreamReader(lookupPath))
                    {
                        while (!reader.EndOfStream)
                        {
                            lineCount++;

                            var line = reader.ReadLine();
                            var splits = line.Split(':');
                            progressTotal += line.Length;

                            if (splits.Length > 1 && !string.IsNullOrEmpty(splits[1]))
                            {
                                //Get the email, stem it and validate it 
                                if (ValidateEmail(splits[0], out var emailStem))
                                {
                                    validCount++;

                                    //We hash the email address to put it in the correct bucket
                                    //We create 256 buckets based on the first byte of the hash
                                    var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(emailStem));
                                    var key = hash[0].ToString("x2");
                                    
                                    //Leave the first two chars (1 byte) as it is the same for the whole file
                                    var identifier = GetIdentifier(hash).Substring(2);
                                    var finals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                                    //Write out each split, so we need to choose columns here
                                    foreach (var i in columns)
                                    {
                                        if (splits.Length > i)
                                        {
                                            var split = splits[i];

                                            //if (split == "rhettlynch") split = split;

                                            if (split.Length > 0)
                                            {
                                                if (options.Tokenize || options.StemEmail || options.StemEmailOnly)
                                                {
                                                    if (options.Tokenize)
                                                    {
                                                        var tokens = split.Split(' ');
                                                        foreach (var token in tokens)
                                                        {
                                                            //We trim the token, but we dont change capitalisation. We leave that to the lookup
                                                            var trimToken = token.Trim();
                                                            if (trimToken.Length > 0) finals.Add(trimToken);
                                                        }
                                                    }

                                                    //Add the original value
                                                    if (!options.Tokenize && !options.StemEmailOnly) finals.Add(split);

                                                    //Stem email if required
                                                    if (options.StemEmail || options.StemEmailOnly)
                                                    {
                                                        var stems = StemEmail(emailStem, lookups);
                                                        finals.UnionWith(stems);
                                                    }
                                                }
                                                else
                                                {
                                                    finals.Add(split);
                                                }
                                            }
                                        }
                                    }

                                    //Add lines
                                    foreach (var final in finals)
                                    {
                                        if (final.Length > 0) buckets[key].Add($"{identifier}:{final}");
                                    }
                                }
                            }

                            if (lineCount % 1000 == 0) WriteProgress($"Adding values", progressTotal, fileEntriesSize);
                        }
                    }

                    //Update the percentage
                    WriteProgress($"Processing file {fileCount} of {fileEntries.Length}", progressTotal, fileEntriesSize);

                    //For now we just write out after every file, although that may need to change in future
                    foreach (var hex1 in Hex)
                    {
                        foreach (var hex2 in Hex)
                        {
                            var key = $"{hex1}{hex2}";

                            File.AppendAllLines($"{options.OutputFolder}\\{options.Prefix}-{key}.txt", buckets[key]);
                            buckets[key].Clear();
                        }
                    }
                }

                //Optimise the folder
                if (!options.NoOptimize) OptimizeFolder(options.OutputFolder, options.Prefix);
            }

            WriteMessage($"Added {validCount} valid lines out of {lineCount}.");
            WriteMessage($"Finished adding values at {DateTime.Now.ToShortTimeString()}.");

        }
    }
}
