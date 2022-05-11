using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Metacrack
{
    public class LookupPlugin : PluginBase
    {
        private static int LookupValueLengthMax = 70;
        private static int IdentifierCountMax = 40;
        private static int InferenceIndexDepth = 10;

        private static Dictionary<string, List<(string Word, long Offset)>> _inferenceIndex;

        private static Regex _regexFast;

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
                WriteError($"No files found for {options.InputPath} in {currentDirectory}");
                return;
            }

            //Work out if we are using a filter
            var rules = (string.IsNullOrEmpty(options.Filter)) ? null : GetRules(options.Filter);

            if (rules != null) WriteMessage($"Loaded {rules.Count()} rules from {options.Filter}");

            WriteMessage($"Using prefix {options.Prefix}");

            var sourceFiles = Directory.GetFiles(options.SourceFolder, $"{options.Prefix}-*");

            if (sourceFiles.Length != 256)
            {
                if (sourceFiles.Count() == 0) WriteError($"No lookup files found for {options.SourceFolder}.");
                WriteError($"Expected 256 lookup files for {options.SourceFolder}.");
                return;
            }

            if (options.Tokenize) WriteMessage("Tokenize enabled");
            if (options.Hash > 0) WriteMessage($"Validating hash mode {options.Hash}");

            if (options.Stem && options.StemOnly)
            {
                WriteError("Options --stem and --stem-only cannot both be specified");
                return;
            }

            if (options.Export && (options.Stem || options.StemOnly))
            {
                WriteError("Options --stem and --stem-only cannot be used with option --export.");
                return;
            }

            if (options.Stem) WriteMessage($"Using stem option.");
            if (options.StemOnly) WriteMessage($"Using stem-only option.");
            if (options.Export) WriteMessage($"Using export option.");

            if (options.UseInference)
            {
                if (options.Export) WriteMessage($"Using inference.");
                _inferenceIndex = new Dictionary<string, List<(string Word, long Offset)>>();
            }

            Console.WriteLine($"Started at {DateTime.Now.ToShortTimeString()}.");

            var size = GetFileEntriesSize(fileEntries);
            var progressTotal = 0L;

            var updateMod = 1000;
            if (size > 100000000) updateMod = 10000;

            //We only want to iterate through a file once, so we have lists of files and lists of their contents in buckets by hex key
            var lookups = new List<FileLookup>();
            var variation = ".";

            if (options.Stem) variation = ".stem";
            if (options.StemOnly) variation = ".stemonly";

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

                                //Validate the hash
                                if (!options.Export)
                                {
                                    if (!ValidateHash(splits[1], hashInfo)) continue;
                                    if (hashInfo.Columns == 2 && !ValidateSalt(splits[2], hashInfo)) continue;
                                }

                                //if (email.StartsWith("mail.adikukreja@gmail.com")) email = email.ToLower();

                                //Validate the email is valid
                                if (ValidateEmail(email, out var emailStem))
                                {
                                    var emailHash = sha1.ComputeHash(Encoding.UTF8.GetBytes(emailStem));
                                    var key = emailHash[0].ToString("x2");
                                    var identifier = GetIdentifier(emailHash).Substring(2);

                                    //We will just add the hash(+salt?) into the output now
                                    if (!lookup.Buckets[key].ContainsKey(identifier)) lookup.Buckets[key].Add(identifier, options.Export ? emailStem: inputHash);
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
                        DoLookup(key, currentDirectory, variation, sourcePath, lookups, options, rules);

                        bucketCount++;
                        WriteProgress($"Looking up key {key}", bucketCount, 256);
                    }
                }

                WriteMessage($"Completed at {DateTime.Now.ToShortTimeString()}.");
            }
        }

        private static void DoLookup(string key, string currentDirectory, string variation, string sourcePath, List<FileLookup> fileLookups, LookupOptions options, List<List<string>> rules)
        {
            //See if we can shortcut this key
            var novalues = true;
            var delim = new char[] { ':' };

            //Clear each lookup outputs
            foreach (var fileLookup in fileLookups)
            {
                if (fileLookup.Buckets[key].Count > 0) novalues = false;

                fileLookup.Hashes.Clear();
                fileLookup.Words.Clear();
            }

            if (novalues) return;

            //Load the file
            using (var sha1 = SHA1.Create())
            using (var reader = new StreamReader(sourcePath))
            {
                var lastIdentifier = "";
                var lastIdentifierCount = 0;

                //Read through the database
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();

                    //Improve speed by not splitting, reading first n chars instead
                    var identifier = line[..18].ToLower();

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
                                var words = new List<string>();

                                foreach (var single in fileLookup.Singles)
                                {
                                    words.Add(single);
                                }

                                foreach (var alpha in fileLookup.Alphas)
                                {
                                    words.Add(alpha);

                                    foreach (var numeric in fileLookup.Numerics)
                                    {
                                        words.Add($"{alpha}{numeric}");
                                    }
                                }

                                //Add infrered words if set
                                if (options.UseInference) words = InferWords(words, options, sha1);

                                //If there are rules, filter the filelookup words, and then remove associated words and hashes by index
                                //Otherwise just make sure they are distinct
                                if (rules != null)
                                {
                                    words = RulesEngine.FilterByRules(words, rules);
                                }
                                else
                                {
                                    words = words.Distinct().ToList();
                                }

                                //Add currentHash n times depending on number of words
                                fileLookup.Hashes.AddRange(Enumerable.Repeat(currentHash, words.Count));
                                fileLookup.Words.AddRange(words);

                                fileLookup.CurrentHash.Clear();
                                fileLookup.Alphas.Clear();
                                fileLookup.Numerics.Clear();
                                fileLookup.Singles.Clear();
                            }
                        }
                    }

                    lastIdentifier = identifier;

                    if (lastIdentifierCount < IdentifierCountMax)
                    {
                        foreach (var fileLookup in fileLookups)
                        {
                            var bucketEntries = fileLookup.Buckets[key];

                            if (bucketEntries.TryGetValue(identifier, out var hash))
                            //if (bucketEntries.ContainsKey(identifier))
                            {
                                //Lookup the hash in the bucket entries for this identifier
                                //It wont change between identifiers
                                //var hash = bucketEntries[identifier];

                                if (!string.IsNullOrEmpty(hash))
                                {
                                    var splits = line.Split(delim, 2);

                                    if (splits.Length == 2 && !string.IsNullOrEmpty(splits[0]) && !string.IsNullOrEmpty(splits[1]) && splits[1].Length < LookupValueLengthMax)
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
            }

            //Dump into the output files
            foreach (var lookup in fileLookups)
            {
                if (lookup.Hashes.Count != lookup.Words.Count) throw new ApplicationException("Hashes count does not match wordlist count.");

                var filePathName = $"{currentDirectory}\\{lookup.Filename}";

                if (options.Export)
                {
                    var output = new List<string>();

                    for (var i = 0; i < lookup.Hashes.Count; i++) output.Add($"{lookup.Hashes[i]}:{lookup.Words[i]}");

                    File.AppendAllLines($"{filePathName}.export.txt", output);
                }
                else
                {
                    File.AppendAllLines($"{filePathName}{variation}.hash", lookup.Hashes);
                    File.AppendAllLines($"{filePathName}{variation}.word", lookup.Words);
                }
            }
        }

        private static void AddToLookup(string hash, string password, FileLookup lookup, LookupOptions options)
        {
            if (options.Export)
            {
                lookup.Singles.Add(password);
                return;
            }

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
                //Only add the word to alphas if it ands in a ltter
                if (!options.StemOnly)
                {
                    if (char.IsLetter(password[password.Length - 1]))
                    {
                        lookup.Alphas.Add(password);
                    }
                    else
                    {
                        lookup.Singles.Add(password);
                    }
                }

                //Check if we should try stem the alpha
                if (options.Stem || options.StemOnly)
                {
                    //https://stackoverflow.com/questions/39470506/c-sharp-regex-performance-very-slow
                    if (_regexFast == null) _regexFast = new Regex("^([a-z]*)", RegexOptions.Compiled & RegexOptions.IgnoreCase);

                    var match = _regexFast.Match(password);
                    
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

        private static List<string> InferWords(List<string> words, LookupOptions options, SHA1 sha1)
        {
            var additions = new HashSet<string>(words);

            foreach (var word in words)
            {
                //First, stem the word, so that it matches the words that have been indexed
                var stemmedWord = StemWord(word, true);

                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(stemmedWord));
                var key = hash[0].ToString("x2");

                //Get the file name 
                var path = $"{options.SourceFolder}\\xref\\{options.Prefix}-xref-{key}.txt";

                //Check if the indexes have been calculated
                if (!_inferenceIndex.ContainsKey(key)) _inferenceIndex.Add(key, CalculateInferenceFileIndexes(key, path));

                var indexes = _inferenceIndex[key];

                if (indexes.Count == 0) continue;

                var indexWord = indexes[0].Word;
                var i = 0;

                //Loop through until we get the starting offset
                while (string.Compare(indexWord, stemmedWord, false) < 0)
                {
                    i++;
                    if (i == indexes.Count) break;

                    indexWord = indexes[i].Word;
                }

                if (i == 0) continue;

                //Now we can open the file at the offset, and read forward until we find the word
                using (var fs = File.OpenRead(path))
                {
                    fs.Seek(indexes[i-1].Offset, SeekOrigin.Begin);

                    using (StreamReader sr = new StreamReader(fs))
                    {
                        var currentWord = "";
                        var line = "";

                        //Seek to start of line
                        if (!sr.EndOfStream) sr.ReadLine();

                        //Look until we find the word, or we go past it in the sort order
                        while (!sr.EndOfStream && string.Compare(currentWord, stemmedWord, false) < 0)
                        {
                            line = sr.ReadLine();
                            currentWord = line.Substring(0, line.IndexOf(':'));
                        }
                        
                        //Split and break apart all the words
                        if (currentWord == stemmedWord)
                        {
                            //Split the line after the key between the words and counts
                            //eg key:word:word:word:count:count:count
                            var splits = line.Split(':');
                            var length = (splits.Length - 1) / 2;
                            var j = 1;

                            while (j <= length)
                            {
                                if (splits[j].Length > 0) additions.Add(splits[j]);
                                j++;
                            }
                        }
                    }
                }
            }

            //Finally, merge additions with words
            return additions.ToList();
        }

        private static List<(string, long)> CalculateInferenceFileIndexes(string key, string path)
        {
            var fileInfo = new FileInfo(path);
            var indexes = new List<(string, long)>();

            if (key == "a9") indexes = new List<(string, long)>();

            if (!fileInfo.Exists) return indexes;

            var middle = fileInfo.Length / 2;

            //Use a file stream so that we can seek
            using (var fs = File.OpenRead(path))
            {
                CalculateInferenceFileIndex(indexes, fs, middle, middle / 2, 0);
            }

            //Sort the indexes by position
            indexes.Sort((x, y) => x.Item2.CompareTo(y.Item2));

            //Add to output
            return indexes;
        }

        private static void CalculateInferenceFileIndex(List<(string, long)> indexes, FileStream stream, long position, long size, int depth)
        {
            //Get halfway between start and middle (position), and middle and end 
            var top = position - size;
            var bottom = position + size;

            indexes.Add(SeekWordAtPosition(stream, top));
            indexes.Add(SeekWordAtPosition(stream, bottom));

            //Recurse to 2^10 levels deep (1024 index points)
            if (depth < InferenceIndexDepth)
            {
                depth++;
                size /= 2;
                CalculateInferenceFileIndex(indexes, stream, top, size, depth);
                CalculateInferenceFileIndex(indexes, stream, bottom, size, depth);
            }
        }

        private static (string, long) SeekWordAtPosition(FileStream stream, long position)
        {
            //Add the words and offsets
            stream.Seek(position, SeekOrigin.Begin);

            using (StreamReader sr = new StreamReader(stream, null, true, -1, true))
            {
                var chars = sr.ReadLine();
                //position += chars.Length;

                var line = sr.ReadLine();
                var word = line.Substring(0, line.IndexOf(':'));

                return (word, position);
            }
        }
    }
}
