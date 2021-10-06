using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Malfoy
{
    public class LookupPlugin : PluginBase
    {
        private static int LookupValueLengthMax = 70;
        private static int IdentifierCountMax = 40;

        private struct FileLookup
        {
            public FileLookup(string filename)
            {
                Buckets = new Dictionary<string, Dictionary<string, string>>(256);
                Filename = filename;
                Hashes = new List<string>();
                Words = new List<string>();

                //This will only ever contain one hash at a time, but needs to be modified in a loop
                CurrentHash = new List<string>();

                Numerics = new List<string>();
                Alphas = new List<string>();
                Singles = new List<string>();   
            }

            public Dictionary<string, Dictionary<string, string>> Buckets;
            public string Filename;
            public List<string> Hashes;
            public List<string> Words;
            
            public List<string> CurrentHash;
            public List<string> Numerics;
            public List<string> Alphas;
            public List<string> Singles;
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
            if (options.Hash > 0) WriteMessage($"Validating hash mode {options.Hash}.");

            if (options.Stem && options.StemOnly)
            {
                WriteError("Options stem and stem-only cannot both be specified.");
                return;
            }

            if (options.Stem) WriteMessage($"Using stem option.");
            if (options.StemOnly) WriteMessage($"Using stem-only option.");

            Console.WriteLine($"Started at {DateTime.Now.ToShortTimeString()}.");

            var size = GetFileEntriesSize(fileEntries);
            var progressTotal = 0L;

            var updateMod = 1000;
            if (size > 100000000) updateMod = 10000;

            //We only want to iterate through a file once, so we have lists of files and lists of their contents in buckets by hex key
            var lookups = new List<FileLookup>();
            var variation = "";

            if (options.Stem) variation = "stem";
            if (options.StemOnly) variation = "stemonly";

            var hashInfo = GetHashInfo(options.Hash);

            using (var sha1 = SHA1.Create())
            {
                foreach (var filePath in fileEntries)
                {
                    //Create a version based on the file size, so that the hash and dict are bound together
                    var fileInfo = new FileInfo(filePath);
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var filePathName = $"{currentDirectory}\\{fileName}";
                    var outputHashPath = $"{filePathName}{variation}.hash";
                    var outputWordPath = $"{filePathName}{variation}.word";

                    //Check that there are no output files
                    if (!CheckForFiles(new string[] { outputHashPath, outputWordPath }))
                    {
                        WriteHighlight($"Skipping {filePathName}.");

                        progressTotal += fileInfo.Length;
                        continue;
                    }

                    //Add the files to the list of filenames
                    var lookup = new FileLookup(fileName);
                    
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

                            if (splits.Length == hashInfo.Columns + 1)
                            {
                                var email = splits[0].ToLower();
                                var inputHash = (hashInfo.Columns == 1) ? splits[1]: $"{splits[1]}:{splits[2]}";

                                if (!ValidateHash(splits[1], hashInfo)) continue;

                                //if (email.StartsWith("mail.adikukreja@gmail.com")) email = email.ToLower();

                                //Validate the email is valid
                                if (ValidateEmail(email, out var emailStem))
                                {
                                    var emailHash = sha1.ComputeHash(Encoding.UTF8.GetBytes(emailStem));
                                    var key = emailHash[0].ToString("x2");
                                    var identifier = GetIdentifier(emailHash).Substring(2);

                                    //We will just add the hash(+salt?) into the output now
                                    if (!lookup.Buckets[key].ContainsKey(identifier)) lookup.Buckets[key].Add(identifier, inputHash);
                                }
                            }

                            //Update the percentage
                            if (lineCount % updateMod == 0) WriteProgress($"Processing {fileInfo.Name}", progressTotal, size);
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
                        DoLookup(key, currentDirectory, sourcePath, lookups, options);

                        bucketCount++;
                        WriteProgress($"Looking up key {key}", bucketCount, 256);
                    }
                }

                WriteMessage($"Completed at {DateTime.Now.ToShortTimeString()}.");
            }
        }

        private static void DoLookup(string key, string currentDirectory, string sourcePath, List<FileLookup> fileLookups, LookupOptions options)
        {
            //Clear each lookup outputs
            foreach (var fileLookup in fileLookups)
            {
                fileLookup.Hashes.Clear();
                fileLookup.Words.Clear();
            }

            //Load the file
            using (var reader = new StreamReader(sourcePath))
            {
                var lastIdentifier = "";
                var lastIdentifierCount = 0;

                //Read through the database
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var splits = line.Split(new char[] { ':' }, 2);
                    var identifier = splits[0].ToLower();

                    //We dont want to inject loads of combolists and bad data. This also seems to break attack mode 9
                    //So track the previous email record
                    if (lastIdentifier == identifier)
                    {
                        lastIdentifierCount++;
                    }
                    else
                    {
                        lastIdentifierCount = 0;

                        //Add the alphas and singles, and multiply out the alphas by the numerics
                        foreach (var fileLookup in fileLookups)
                        {
                            if (fileLookup.CurrentHash.Count > 0)
                            {
                                var currentHash = fileLookup.CurrentHash[0];

                                foreach (var single in fileLookup.Singles)
                                {
                                    fileLookup.Hashes.Add(currentHash);
                                    fileLookup.Words.Add(single);
                                }

                                foreach (var alpha in fileLookup.Alphas)
                                {
                                    fileLookup.Hashes.Add(currentHash);
                                    fileLookup.Words.Add(alpha);

                                    foreach (var numeric in fileLookup.Numerics)
                                    {
                                        fileLookup.Hashes.Add(currentHash);
                                        fileLookup.Words.Add($"{alpha}{numeric}");
                                    }
                                }

                                fileLookup.CurrentHash.Clear();
                                fileLookup.Alphas.Clear();
                                fileLookup.Numerics.Clear();
                                fileLookup.Singles.Clear();
                            }
                        }
                    }

                    lastIdentifier = identifier;

                    if (lastIdentifierCount < IdentifierCountMax && splits.Length == 2 && !string.IsNullOrEmpty(splits[0]) && !string.IsNullOrEmpty(splits[1]) && splits[1].Length < LookupValueLengthMax)
                    {
                        foreach (var fileLookup in fileLookups)
                        {
                            var bucketEntries = fileLookup.Buckets[key];

                            if (bucketEntries.ContainsKey(identifier))
                            {
                                //Lookup the hash in the bucket entries for this identifier
                                //It wont change between identifiers
                                var hash = bucketEntries[identifier];

                                if (!string.IsNullOrEmpty(hash))
                                {
                                    //We cant modify a struct in a loop, so we just add into the collection once
                                    if (fileLookup.CurrentHash.Count == 0) fileLookup.CurrentHash.Add(hash);

                                    if (options.Tokenize)
                                    {
                                        var tokens = GetTokens(splits[1]);
                                        foreach (var token in tokens)
                                        {
                                            AddToLookup(hash, token, fileLookup, options);
                                        }
                                    }
                                    else
                                    {
                                        AddToLookup(hash, splits[1], fileLookup, options);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            //Dump into the output files
            foreach (var lookup in fileLookups)
            {
                if (lookup.Hashes.Count != lookup.Words.Count) throw new ApplicationException("Hashes count does not match wordlist count.");

                var filePathName = $"{currentDirectory}\\{lookup.Filename}";

                File.AppendAllLines($"{filePathName}.hash", lookup.Hashes);
                File.AppendAllLines($"{filePathName}.word", lookup.Words);
            }
        }

        private static void AddToLookup(string hash, string password, FileLookup lookup, LookupOptions options)
        {
            //Add a number or text
            if (int.TryParse(password, out var number))
            {
                if (!options.StemOnly)
                {
                    if (number > 9 && number < 100000)
                    {
                        lookup.Numerics.Add(password);

                        if (number >= 1930 && number < 2001) lookup.Numerics.Add((number - 1900).ToString());
                        if (number >= 2001 && number < 2030) lookup.Numerics.Add((number - 2000).ToString());
                    }
                    else
                    {
                        lookup.Singles.Add(password);
                    }
                }
            }
            else
            {
                if (!options.StemOnly) lookup.Alphas.Add(password);

                //Check if we should try stem the alpha
                if (options.Stem || options.StemOnly)
                {
                    var match = Regex.Match(password, "^([a-z]*)", RegexOptions.IgnoreCase);

                    if (match.Success && match.Value.Length > 3)
                    {
                        //We dont stem lower in case there are multiple capitals we wouldnt pick up with a rule
                        var stem = match.Value;

                        //Stem only adds if the words are different. and if we havent already added it for this identifier
                        if (!string.Equals(stem, password)) lookup.Alphas.Add(stem);
                    }
                }
            }
        }
    }
}
