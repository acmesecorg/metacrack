using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Malfoy
{
    public static class Lookup
    {
        private static string[] Hex = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "a", "b", "c", "d", "e", "f" };
        private static readonly object _filelock = new object();

        private static int PasswordLengthMax = 70;
        private static int EmailPasswordCountMax = 40;


        public static void Process(string currentDirectory, string[] args)
        {
            var arg = args[0];
            var source = args[1];
            var prefix = "Passwords";

            if (args.Length > 3) prefix = args[3];

            Console.WriteLine($"Using prefix {prefix}.");

            //Split lookups into lowercase, remove special characters etc
            var tokenize = Common.GetCommandLineArgument(args, -1, "-tokens") != null;
            if (tokenize) Console.WriteLine($"Using tokenize.");

            var hashtype = Common.GetCommandLineArgument(args, -1, "-hash");
            var mode = "999";

            if (!string.IsNullOrEmpty(hashtype))
            {
                mode = hashtype;
                Console.WriteLine($"Detected mode 3200 (bcrypt).");
            }


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

                            var splits = line.Split(new char[] {':'}, StringSplitOptions.RemoveEmptyEntries);

                            if (splits.Length == 2) //|| splits.Length == 3 || splits.Length == 5 - override with a value otherwise corrupt data gets in
                            {
                                var email = splits[0].ToLower();
                                var inputHash = splits[1];

                                if (mode == "3200" && inputHash.Length != 60) continue;                             

                                if (email.StartsWith("mail.adikukreja@gmail.com")) email = email.ToLower();

                                //Validate the email is valid
                                if (Common.ValidateEmail(email))
                                {
                                    var emailHash = sha1.ComputeHash(Encoding.UTF8.GetBytes(email));
                                    var key = emailHash[0].ToString("x2") + emailHash[1].ToString("x2").Substring(0, 1);

                                    //Rejoin the splits incase line contained ::
                                    if (!buckets[key].ContainsKey(email)) buckets[key].Add(email, String.Join(":", splits));
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
                            var tasks = new List<Task>();

                            foreach (var hex3 in Hex)
                            {
                                var key = $"{hex1}{hex2}{hex3}";

                                if (buckets[key].Count > 0)
                                {
                                    var sourcePath = $"{source}\\{prefix}-{key}.txt";
                                    //tasks.Add(DoLookup(sourcePath, buckets[key], outputHashPath, outputDictPath, tokenize));
                                    DoLookup(sourcePath, buckets[key], outputHashPath, outputDictPath, tokenize);
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

        private static void DoLookup(string sourcePath, Dictionary<string, string> entries, string outputHashPath, string outputDictPath, bool tokenize)
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

                    if (email.StartsWith("mail.adikukreja@gmail.com")) lastEmail = email;

                    //Validate the email
                    if (!Common.ValidateEmail(email)) continue;

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

                    if (lastEmailCount < EmailPasswordCountMax && splits.Length == 2 && !string.IsNullOrEmpty(splits[0]) && !string.IsNullOrEmpty(splits[1]) && splits[1].Length < PasswordLengthMax)
                    {
                        if (entries.ContainsKey(email))
                        {
                            //Check hash is fine
                            var hash = entries[email];

                            if (!string.IsNullOrEmpty(hash))
                            {
                                if (tokenize)
                                {
                                    var tokens = Common.GetTokens(splits[1]);
                                    foreach (var token in tokens)
                                    {
                                        hashes.Add(hash);
                                        dicts.Add(token);
                                    }
                                }
                                else
                                {
                                    hashes.Add(hash);
                                    dicts.Add(splits[1]);
                                }
                            }
                        }
                    }
                }
            }

            //Dump into the output files
            lock (_filelock)
            {
                if (hashes.Count != dicts.Count) throw new ApplicationException("This should not happen");

                File.AppendAllLines(outputHashPath, hashes);
                File.AppendAllLines(outputDictPath, dicts);
            }
        }
    }
}
