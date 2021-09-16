using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Malfoy
{
    public static class Catalog
    {
        private static string[] Hex = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "a", "b", "c", "d", "e", "f" };

        public static string Prefix { get; set; }

        public static void Process(string currentDirectory, string[] args)
        {
            
            if (!Directory.Exists(args[0]))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Input folder {args[0]} was not found.");
                Console.ResetColor();
                return;
            }

            var outputPath = args[1];

            if (!Directory.Exists(outputPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Output folder {outputPath} was not found.");
                Console.ResetColor();
                return;
            }

            if (string.IsNullOrEmpty(Prefix)) Prefix = "Passwords";

            Console.WriteLine($"Using prefix {Prefix}.");

            //Get list of text file entries
            var fileEntries = Directory.GetFiles(args[0],"*.txt", SearchOption.AllDirectories);
            var fileEntriesSize = Common.GetFileEntriesSize(fileEntries);

            Console.WriteLine($"Found {fileEntries.Count()} text file entries ({Common.FormatFileSize(fileEntriesSize)}) in all folders.");

            var progressTotal = 0L;
            var lineCount = 0L;
            var validCount = 0L;

            Console.WriteLine($"Started adding values at {DateTime.Now.ToShortTimeString()}.");

            var buckets = new Dictionary<string, List<string>>(4096);

            foreach (var hex1 in Hex)
            {
                foreach (var hex2 in Hex)
                {
                    foreach (var hex3 in Hex) buckets.Add($"{hex1}{hex2}{hex3}", new List<string>());
                }
            }

            //We use sha1 manged for now for potential cross platform benefits
            using (var progress = new ProgressBar(false))
            using (var sha1 = new SHA1Managed())
            {
                foreach (var lookupPath in fileEntries)
                {
                    using (var reader = new StreamReader(lookupPath))
                    {
                        while (!reader.EndOfStream)
                        {
                            lineCount++;

                            var line = reader.ReadLine();
                            var splits = line.Split(':');
                            progressTotal += line.Length;

                            if (splits.Length >= 2)
                            {
                                validCount++;

                                //We hash the email address to put it in the correct bucket
                                //We create 256 * 16 = 4096 buckets
                                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(splits[0]));
                                var key = hash[0].ToString("x2") + hash[1].ToString("x2").Substring(0, 1);

                                buckets[key].Add($"{splits[0]}:{splits[1]}");                                
                            }

                            //Update the percentage
                            progress.Report((double)progressTotal / fileEntriesSize);
                        }
                    }

                    //For now we just write out after every file, although that may need to change in future
                    foreach (var hex1 in Hex)
                    {
                        foreach (var hex2 in Hex)
                        {
                            foreach (var hex3 in Hex)
                            {
                                var key = $"{hex1}{hex2}{hex3}";
                                
                                File.AppendAllLines($"{outputPath}\\{Prefix}-{key}.txt", buckets[key]);
                                buckets[key].Clear();
                            }
                        }
                    }
                }
            }

            Console.WriteLine($"Added {validCount} valid lines out of {lineCount}.");
            Console.WriteLine($"Finished adding values at {DateTime.Now.ToShortTimeString()}.");
        }
    }
}
