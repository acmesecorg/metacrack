using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Malfoy
{
    public static class Lookup
    {
        private static string[] Hex = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "a", "b", "c", "d", "e", "f" };
        private static readonly object _filelock = new object();

        public static void Process(string currentDirectory, string[] args)
        {
            var arg = args[0];
            var source = args[1];
            var prefix = "Passwords";

            //Get user hashes / json input path
            var fileEntries = Directory.GetFiles(currentDirectory, arg);

            if (fileEntries.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No input file(s) found.");
                Console.ResetColor();
                return;
            }

            var sourceFiles = Directory.GetFiles(source, $"{prefix}-*");

            if (sourceFiles.Count() != 4096)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                if (sourceFiles.Count() == 0) Console.WriteLine("No source files found.");
                if (sourceFiles.Count() != 4096) Console.WriteLine($"Found {sourceFiles.Count()} file(s) instead of 4096 files.");

                Console.ResetColor();
                return;
            }

            Console.WriteLine($"Started at {DateTime.Now.ToShortTimeString()}.");

            var size = Common.GetFileEntriesSize(fileEntries);
            var progressTotal = 0L;

            using (var progress = new ProgressBar(false))
            using (var sha1 = new SHA1Managed())
            {
                foreach (var filePath in fileEntries)
                {
                    progress.WriteLine($"Processing {filePath}.");

                    //Create a version based on the file size, so that the hash and dict are bound together
                    var fileInfo = new FileInfo(filePath);
                    var version = Common.GetSerial(fileInfo);

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

                        progressTotal += fileInfo.Length;

                        progress.Resume();
                        progress.Report((double)progressTotal / size);

                        continue;
                    }

                    long lineCount = 0;

                    var buckets = new Dictionary<string, Dictionary<string, string>>(4096);

                    foreach (var hex1 in Hex)
                    {
                        foreach (var hex2 in Hex)
                        {
                            foreach (var hex3 in Hex) buckets.Add($"{hex1}{hex2}{hex3}", new Dictionary<string, string>());
                        }
                    }

                    //1. Loop through the file and place each hashed email in a bucket
                    using (var reader = new StreamReader(filePath))
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();

                            lineCount++;
                            progressTotal += line.Length;

                            var splits = line.Split(':');

                            if (splits.Length == 2 || splits.Length == 3 || splits.Length == 5)
                            {
                                var email = splits[0].ToLower();

                                //Validate the email is valid
                                var subsplits = email.Split('@');

                                if (subsplits.Length == 2)
                                {
                                    var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(email));
                                    var key = hash[0].ToString("x2") + hash[1].ToString("x2").Substring(0, 1);

                                    if (!buckets[key].ContainsKey(email)) buckets[key].Add(email, line);
                                }
                            }

                            //Update the percentage
                            progress.Report((double)progressTotal / size);
                        }
                    }

                    progress.WriteLine("Starting lookups in source");

                    //2. Loop through each bucket, load the source file into memory, and write out any matches
                    //For each match, write out a line to both the hash file, and the dictionary file
                    var bucketCount = 0;

                    foreach (var hex1 in Hex)
                    {
                        foreach (var hex2 in Hex)
                        {
                            //Process up to 16 tasks at once
                            //var tasks = new List<Task>();

                            foreach (var hex3 in Hex)
                            {
                                var key = $"{hex1}{hex2}{hex3}";

                                if (buckets[key].Count > 0)
                                {
                                    var sourcePath = $"{source}\\{prefix}-{key}.txt";
                                    //tasks.Add(Task.Run(() => DoLookup(sourcePath, buckets[key], outputHashPath, outputDictPath)));
                                    DoLookup(sourcePath, buckets[key], outputHashPath, outputDictPath);
                                }

                                bucketCount++;
                                progress.Report((double)bucketCount / 4096);

                            }

                            //Wait for tasks to complate
                            //while (tasks.Count > 0)
                            //{
                            //    var task = await Task.WhenAny(tasks);
                            //    tasks.Remove(task);

                            //    bucketCount++;
                            //    progress.Report((double)bucketCount / 4096);
                            //}
                        }
                    }
                }

                progress.WriteLine($"Completed at {DateTime.Now.ToShortTimeString()}.");
            }
        }

        private static void DoLookup(string sourcePath, Dictionary<string, string> entries, string outputHashPath, string outputDictPath)
        {
            var hashes = new List<string>();
            var dicts = new List<string>();

            //Load the file
            using (var reader = new StreamReader(sourcePath))
            {
                var lastEmail = "";
                var lastEmailCount = 0;

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var splits = line.Split(new char[] { ':' }, 2);
                    var email = splits[0].ToLower();

                    //We dont want to inject loads of combolists and bad data. This also seems to break attack mode 9
                    //So track the previous email record
                    if (lastEmail == email)
                    {
                        lastEmailCount++;
                    }
                    else
                    {
                        lastEmailCount = 0;
                    }
                    lastEmail = email;

                    if (lastEmailCount < 20 && splits.Length == 2 && !string.IsNullOrEmpty(splits[0]) && !string.IsNullOrEmpty(splits[1]) && splits[1].Length < 70)
                    {
                        if (entries.ContainsKey(email))
                        {
                            hashes.Add(entries[email]);
                            dicts.Add(splits[1]);
                        }

                    }
                }
            }

            //Dump into the output files
            lock (_filelock)
            {
                File.AppendAllLines(outputHashPath, hashes);
                File.AppendAllLines(outputDictPath, dicts);
            }
        }
    }
}
