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
            var fileEntries = Directory.GetFiles(options.InputFolder, "*.txt", SearchOption.AllDirectories);

            if (fileEntries.Length == 0)
            {
                WriteError($"No .txt files found for {options.InputFolder}");
                return;
            }

            if (!Directory.Exists(options.OutputFolder))
            {
                WriteError($"Output folder {options.OutputFolder} was not found.");
                return;
            }

            WriteMessage($"Using prefix {options.Prefix}");

            if (!options.NoOptimize) WriteMessage("Optimize enabled");
            if (options.Tokenize) WriteMessage("Tokenize enabled");

            //Determine columns;
            int[] columns = (options.Columns.Count() == 0) ? new int[] {1} : Array.ConvertAll(options.Columns.ToArray(), s => int.Parse(s));

            WriteMessage($"Using columns {String.Join(',', columns)}");

            //Get files
            var fileEntriesSize = GetFileEntriesSize(fileEntries);

            WriteMessage($"Found {fileEntries.Count()} text file entries ({FormatSize(fileEntriesSize)}) in all folders.");

            var progressTotal = 0L;
            var lineCount = 0L;
            var validCount = 0L;
            var fileCount = 0;

            WriteMessage($"Started adding values at {DateTime.Now.ToShortTimeString()}.");

            //Create 256 buckets to contain information for each file
            var buckets = new Dictionary<string, List<string>>(4096);

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

                                    //Write out each split, so we need to choose columns here
                                    foreach (var i in columns)
                                    {
                                        if (splits.Length > i)
                                        {
                                            var split = splits[i];
                                            if (options.Tokenize)
                                            {
                                                var tokens = split.Split(' ');
                                                foreach (var token in tokens)
                                                {
                                                    var trimToken = token.Trim().ToLower();
                                                    if (trimToken.Length > 0) buckets[key].Add($"{identifier}:{trimToken}");
                                                }
                                            }
                                            else
                                            {
                                                buckets[key].Add($"{identifier}:{splits[i]}");
                                            }
                                        }
                                    }
                                }
                            }
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

                if (!options.NoOptimize)
                {
                    WriteMessage($"Optimising buckets.");

                    progressTotal = 0;
                    var count = buckets.Count();

                    var sourceFiles = Directory.GetFiles(options.OutputFolder, $"{options.Prefix}-*");

                    foreach (var sourceFile in sourceFiles)
                    {
                        var bucket = new List<string>();
                        using (var reader = new StreamReader(sourceFile))
                        {
                            while (!reader.EndOfStream)
                            {
                                bucket.Add(reader.ReadLine());
                            }
                        }

                        //Optimize this bucket by deduplicating and then sorting
                        bucket = bucket.Distinct().OrderBy(q => q).ToList();

                        File.Delete(sourceFile);
                        File.AppendAllLines(sourceFile, bucket);

                        progressTotal++;
                        WriteProgress("Optimizing files", progressTotal, count);
                    }
                }
            }

            WriteMessage($"Added {validCount} valid lines out of {lineCount}.");
            WriteMessage($"Finished adding values at {DateTime.Now.ToShortTimeString()}.");

        }
    }
}
