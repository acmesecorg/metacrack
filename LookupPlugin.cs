using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Malfoy
{
    public class LookupPlugin : PluginBase
    {
        private static int LookupValueLengthMax = 70;
        private static int IdentifierCountMax = 40;

        private struct FileLookup
        {
            public FileLookup(string filename, string version)
            {
                Buckets = new Dictionary<string, Dictionary<string, string>>(256);
                Filename = filename;
                Version = version;
                Hashes = new List<string>();
                Words = new List<string>();
            }

            public Dictionary<string, Dictionary<string, string>> Buckets;
            public string Filename;
            public string Version;
            public List<string> Hashes;
            public List<string> Words;
        }

        public static void Process(LookupOptions options)
        {
            //Validate and display arguments
            var currentDirectory = Directory.GetCurrentDirectory();
            var fileEntries = Directory.GetFiles(currentDirectory, options.InputPath);

            if (fileEntries.Length == 0)
            {
                WriteError($"No files found for {options.InputPath} in {currentDirectory}.");
                return;
            }

            WriteMessage($"Using prefix {options.Prefix} .");

            var sourceFiles = Directory.GetFiles(options.SourceFolder, $"{options.Prefix}-*");

            if (sourceFiles.Length != 256)
            {
                if (sourceFiles.Count() == 0) WriteError($"No lookup files found for {options.SourceFolder}.");
                WriteError($"Expected 256 lookup files for {options.SourceFolder}.");
                return;
            }

            if (options.Tokenize) WriteMessage("Tokenize enabled.");

            Console.WriteLine($"Started at {DateTime.Now.ToShortTimeString()}.");

            var size = Common.GetFileEntriesSize(fileEntries);
            var progressTotal = 0L;

            //We only want to iterate through a file once, so we have lists of files and lists of their contents in buckets by hex key
            var lookups = new List<FileLookup>();

            using (var sha1 = SHA1.Create())
            {
                foreach (var filePath in fileEntries)
                {
                    //Create a version based on the file size, so that the hash and dict are bound together
                    var fileInfo = new FileInfo(filePath);
                    var version = Common.GetSerial(fileInfo);

                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var filePathName = $"{currentDirectory}\\{fileName}";
                    var outputHashPath = $"{filePathName}.{version}.hash";
                    var outputWordPath = $"{filePathName}.{version}.word";

                    //Check that there are no output files
                    if (!CheckForFiles(new string[] { outputHashPath, outputWordPath }))
                    {
                        WriteHighlight($"Skipping {filePathName}.");

                        progressTotal += fileInfo.Length;
                        continue;
                    }

                    //Add the files to the list of filenames
                    var lookup = new FileLookup(fileName, version);
                    
                    long lineCount = 0;

                    lookup.Buckets = new Dictionary<string, Dictionary<string, string>>(256);

                    foreach (var hex1 in Hex)
                    {
                        foreach (var hex2 in Hex)
                        {
                            lookup.Buckets.Add($"{hex1}{hex2}", new Dictionary<string, string>());
                        }
                    }

                    //Loop through the file and place each hashed email in a bucket
                    using (var reader = new StreamReader(filePath))
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();

                            lineCount++;
                            progressTotal += line.Length;

                            var splits = line.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);

                            if (splits.Length == 2) //|| splits.Length == 3 || splits.Length == 5 - override with a value otherwise corrupt data gets in
                            {
                                var email = splits[0].ToLower();
                                var inputHash = splits[1];

                                if (!ValidateHash(inputHash, options.Hash)) continue;

                                //if (email.StartsWith("mail.adikukreja@gmail.com")) email = email.ToLower();

                                //Validate the email is valid
                                if (ValidateEmail(email, out var emailStem))
                                {
                                    var emailHash = sha1.ComputeHash(Encoding.UTF8.GetBytes(emailStem));
                                    var key = emailHash[0].ToString("x2");
                                    var identifier = GetIdentifier(emailHash).Substring(2);

                                    //We will jsut add the hash(+salt?) into the output now
                                    if (!lookup.Buckets[key].ContainsKey(identifier)) lookup.Buckets[key].Add(identifier, inputHash);
                                }
                            }

                            //Update the percentage
                            WriteProgress($"Processing {fileInfo.Name}", progressTotal, size);
                        }
                    }

                    lookups.Add(lookup);
                }

                WriteMessage("Starting lookups in source");

                //2. Loop through each bucket, load the source file into memory, and write out any matches
                //For each match, write out a line to both the hash file, and the dictionary file
                var bucketCount = 0;

                foreach (var hex1 in Hex)
                {
                    foreach (var hex2 in Hex)
                    {
                        var key = $"{hex1}{hex2}";
                        var sourcePath = $"{options.SourceFolder}\\{options.Prefix}-{key}.txt";
                        DoLookup(key, currentDirectory, sourcePath, lookups, options.Tokenize);

                        bucketCount++;
                        WriteProgress($"Looking up key {key}", bucketCount, 256);
                    }
                }

                WriteMessage($"Completed at {DateTime.Now.ToShortTimeString()}.");
            }
        }

        private static void DoLookup(string key, string currentDirectory, string sourcePath, List<FileLookup> lookups, bool tokenize)
        {
            //Clear each lookup outputs
            foreach (var lookup in lookups)
            {
                lookup.Hashes.Clear();
                lookup.Words.Clear();
            }

            //Load the file
            using (var reader = new StreamReader(sourcePath))
            {
                var lastIdentifier = "";
                var lastIdentifierCount = 0;

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var splits = line.Split(new char[] { ':' }, 2);
                    var identifier = splits[0].ToLower();

                    //if (email.StartsWith("mail.adikukreja@gmail.com")) lastEmail = email;

                    //We dont want to inject loads of combolists and bad data. This also seems to break attack mode 9
                    //So track the previous email record
                    if (lastIdentifier == identifier)
                    {
                        lastIdentifierCount++;
                    }
                    else
                    {
                        lastIdentifierCount = 0;
                    }
                    lastIdentifier = identifier;

                    if (lastIdentifierCount < IdentifierCountMax && splits.Length == 2 && !string.IsNullOrEmpty(splits[0]) && !string.IsNullOrEmpty(splits[1]) && splits[1].Length < LookupValueLengthMax)
                    {
                        foreach (var lookup in lookups)
                        {
                            var entries = lookup.Buckets[key];

                            if (entries.ContainsKey(identifier))
                            {
                                //Check hash is fine
                                var hash = entries[identifier];

                                if (!string.IsNullOrEmpty(hash))
                                {
                                    if (tokenize)
                                    {
                                        var tokens = GetTokens(splits[1]);
                                        foreach (var token in tokens)
                                        {
                                            lookup.Hashes.Add(hash);
                                            lookup.Words.Add(token);
                                        }
                                    }
                                    else
                                    {
                                        lookup.Hashes.Add(hash);
                                        lookup.Words.Add(splits[1]);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            //Dump into the output files
            foreach (var lookup in lookups)
            {
                if (lookup.Hashes.Count != lookup.Words.Count) throw new ApplicationException("Hashes count does not match wordlist count.");

                var filePathName = $"{currentDirectory}\\{lookup.Filename}";

                File.AppendAllLines($"{filePathName}.{lookup.Version}.hash", lookup.Hashes);
                File.AppendAllLines($"{filePathName}.{lookup.Version}.word", lookup.Words);
            }
        }
    }
}
