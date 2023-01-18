using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Metacrack
{
    public class CatalogPlugin : PluginBase
    {
        //https://blog.cdemi.io/async-waiting-inside-c-sharp-locks/
        private static Dictionary<string, SemaphoreSlim> _locks;

        public static void Process(CatalogOptions options)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            string[] fileEntries = { };

            //Absolute path
            if (Path.IsPathFullyQualified(options.InputPath))
            {
                string path = options.InputPath.Replace("\\", "/");
                int pos = path.LastIndexOf('/');

                if (File.Exists(options.InputPath) || Directory.Exists(options.InputPath))
                    fileEntries = Directory.GetFiles(path[..pos], path[(pos + 1)..], SearchOption.AllDirectories);
            }
            //Relative path
            else
                fileEntries = Directory.GetFiles(currentDirectory, options.InputPath, SearchOption.AllDirectories);

            //Validate and display arguments
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
            if (options.StemDomain) WriteMessage("Stem domain enabled");
            if (options.XReference) WriteMessage("X reference enabled");
            if (options.EmailOnly) WriteMessage("Email only enabled");

            //Determine columns;
            int[] columns = (options.Columns.Count() == 0) ? new int[] { 1 } : Array.ConvertAll(options.Columns.ToArray(), s => int.Parse(s));

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

                        //We add the lower case version for comparison only
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

            if (options.XReferenceOnly)
            {
                WriteMessage($"Skipping adding values.");
            }
            else
            {
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
                                        emailStem = emailStem.ToLower();

                                        //We hash the email address to put it in the correct bucket
                                        //We create 256 buckets based on the first byte of the hash
                                        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(emailStem));
                                        var key = hash[0].ToString("x2");

                                        //Leave the first two chars (1 byte) as it is the same for the whole file
                                        var identifier = GetIdentifier(hash).Substring(2);
                                        var finals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                                        if (options.EmailOnly)
                                        {
                                            finals.Add(splits[0].ToLower());
                                        }
                                        else
                                        {
                                            //Write out each split, so we need to choose columns here

                                            //TODO: just use all columns
                                            //Set a flag at the start, and keep increasing the columns collection by the size of the splits
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
                                                        }
                                                        else
                                                        {
                                                            finals.Add(split);
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        //Stem email if required
                                        if (options.StemEmail || options.StemEmailOnly) StemEmail(emailStem, lookups, finals, options);

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

            if (options.XReference)
            {
                DoXReference(options).GetAwaiter().GetResult();
                WriteMessage($"Finished x referenceing values at {DateTime.Now.ToShortTimeString()}.");
            }
        }

        private static async Task DoXReference(CatalogOptions options)
        {
            var xrefFolder = $"{options.OutputFolder}\\xref\\";
            if (!Directory.Exists(xrefFolder))
            {
                WriteMessage($"Creating new xref folder at {xrefFolder}");
                Directory.CreateDirectory(xrefFolder);
            }

            //Clear any existing lock objects
            _locks = new Dictionary<string, SemaphoreSlim>();
            foreach (var hex1 in Hex)
            {
                foreach (var hex2 in Hex)
                {
                    //Create a new lock object for this hex key
                    _locks.Add($"{hex1}{hex2}", new SemaphoreSlim(1, 1));
                }
            }

            //Loop through each file with this prefix in the output folder
            var bucketCount = 0;

            WriteProgress($"Processing files", bucketCount, 256);

            foreach (var hex1 in Hex)
            {
                var tasks = new List<Task>();

                foreach (var hex2 in Hex)
                {
                    var key = $"{hex1}{hex2}";
                    var path = $"{options.OutputFolder}\\{options.Prefix}-{key}.txt";

                    tasks.Add(CalculateXRef(path, options));
                }

                //Wait for these tasks to complete (16 at a time)
                while (tasks.Count > 0)
                {
                    var completedTask = await Task.WhenAny(tasks.ToArray());

                    bucketCount++;

                    WriteProgress($"Processing files", bucketCount, 256);

                    tasks.Remove(completedTask);
                }
            }

            //We now have 256 files full of associated words, a word can appear multiple times, but only in one file
            //Loop through each file, combine entries, then optimise the file            
            bucketCount = 0;
            WriteProgress($"Optimising files", bucketCount, 256);

            foreach (var hex1 in Hex)
            {
                var tasks = new List<Task>();

                foreach (var hex2 in Hex)
                {
                    var key = $"{hex1}{hex2}";
                    var mapPath = $"{options.OutputFolder}\\xref\\{options.Prefix}-xref-{key}.tmp";
                    var outputPath = $"{options.OutputFolder}\\xref\\{options.Prefix}-xref-{key}.txt";

                    tasks.Add(OptimiseFile(mapPath, outputPath));
                }

                while (tasks.Count > 0)
                {
                    var completedTask = await Task.WhenAny(tasks.ToArray());

                    bucketCount++;

                    WriteProgress($"Optimising files", bucketCount, 256);

                    tasks.Remove(completedTask);
                }
            }
        }

        private static async Task CalculateXRef(string path, CatalogOptions options)
        {
            //For this file collect the passwords by email hash and stem them into a unique collection.
            //Remember to decode $HEX[] passwords
            //Then, create an association between each password in the group. At the end, write out the file corresponding to the name of the input file
            //password:word,word,word:count,count,count
            //Remember to re-ecode $HEX[] passwords

            try
            {
                using (var reader = new StreamReader(path))
                {
                    var lastIdentifier = "";
                    var lastIdentifierCount = 0;

                    var associates = new Dictionary<string, Dictionary<string, int>>();
                    var candidates = new HashSet<string>();

                    //Read through the database
                    while (!reader.EndOfStream)
                    {
                        var lines = await reader.ReadLinesAsync(10);

                        //Mark the end of the file by placing an end of file line that doesnt get processed
                        if (reader.EndOfStream) lines.Add("ffffffffffffffffff:end:0");

                        foreach (var line in lines)
                        {
                            //Improve speed by not splitting, reading first n chars instead
                            var identifier = line[..18].ToLower();
                            var word = line[19..];

                            //Convert word from hex if needed
                            if (word.StartsWith("$HEX["))
                            {
                                var passwordHex = word.Substring(5, word.Length - 6);
                                word = FromHexString(passwordHex);
                            }

                            //We need to stem this word to remove permutations of the same thing
                            //Because we are using a hashset, candidates wont be repeated
                            word = StemWord(word, true);

                            //For now, we are going to skip numbers and other specials, due to volume
                            if (string.IsNullOrEmpty(word)) continue;

                            //We need to filter out very long strings too
                            if (word.Length > 20) continue;

                            if (lastIdentifier == "" || lastIdentifier == identifier)
                            {
                                lastIdentifierCount++;
                            }
                            else
                            {
                                lastIdentifierCount = 0;

                                //We now need to add all the candidates to the associates and cross reference them with a count
                                //Ignore email hashes with only one password
                                if (candidates.Count > 1)
                                {
                                    foreach (var candidate in candidates)
                                    {
                                        if (!associates.ContainsKey(candidate)) associates.Add(candidate, new Dictionary<string, int>());

                                        foreach (var candidate2 in candidates)
                                        {
                                            if (candidate == candidate2) continue;

                                            if (!associates[candidate].ContainsKey(candidate2)) associates[candidate].Add(candidate2, 0);
                                            associates[candidate][candidate2]++;
                                        }
                                    }
                                }

                                candidates.Clear();
                            }

                            //There are some bad data email hashes with many words, so skips those
                            if (lastIdentifierCount < 25)
                            {
                                candidates.Add(word);

                                //Add the stemmed version as well
                                candidates.Add(StemWord(word, true));
                            }
                            lastIdentifier = identifier;
                        }
                    }

                    //Write out the associates table to the intermediate final files
                    await WriteFiles(associates, options);
                }
            }
            catch (Exception ex)
            {
                WriteError($"Exception calculating xref for {path}. {ex.Message}");
            }
        }

        private static async Task WriteFiles(Dictionary<string, Dictionary<string, int>> associates, CatalogOptions options)
        {
            var output = new Dictionary<string, List<string>>();

#pragma warning disable SYSLIB0021
            //We keep using Sha1Managed for performance reasons
            using (var sha1 = new SHA1Managed())
            {
                foreach (var de in associates)
                {
                    var line = new StringBuilder();
                    var key = de.Key;

                    //We hash the password to put it in a file bucket
                    //We create 256 buckets based on the first byte of the hash
                    var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(key));
                    var fileKey = hash[0].ToString("x2");

                    //if (key == "56jgg") flag = true;

                    if (key.Contains(':')) key = $"$HEX[{ToHexString(key)}]";

                    line.Append(key);
                    line.Append(':');

                    var keys = new List<string>();
                    var values = new List<string>();

                    //Loop through and add values and counts
                    foreach (var de2 in associates[de.Key])
                    {
                        var key2 = de2.Key;
                        if (key2.Contains(':')) key2 = $"$HEX[{ToHexString(key2)}]";

                        keys.Add(key2);
                        values.Add(de2.Value.ToString());
                    }

                    //Use : as a sub seperator as well, which means we need to use the occurences of : when parsing back
                    var keysString = string.Join(":", keys);
                    keysString = keysString.Replace("\r", "").Replace("\n", "").Replace("\t", "");

                    line.Append(keysString);
                    line.Append(':');

                    var valuesString = string.Join(":", values);
                    valuesString = valuesString.Replace("\r", "").Replace("\n", "").Replace("\t", "");

                    line.Append(valuesString);

                    if (!output.ContainsKey(fileKey)) output.Add(fileKey, new List<string>());

                    //Add to the list for the filekey
                    output[fileKey].Add(line.ToString());
                }
            }

            //Loop through each de, lock and write out the lines
            foreach (var de in output)
            {
                var path = $"{options.OutputFolder}\\xref\\{options.Prefix}-xref-{de.Key}.tmp";

                //https://blog.cdemi.io/async-waiting-inside-c-sharp-locks/           
                try
                {
                    await _locks[de.Key].WaitAsync();
                    await File.AppendAllLinesAsync(path, de.Value);
                }
                catch (Exception ex)
                {
                    WriteError(ex.Message);
                }
                finally
                {
                    //When the task is ready, always release the semaphore. 
                    _locks[de.Key].Release();
                }
            }
        }

        //Sort by key, and optimise key/words
        private static async Task OptimiseFile(string path, string outputPath)
        {
            try
            {
                //Dictionary already sorted by key
                var map = await ReadCombineFile(path);
                var output = new List<string>();

                //Get rid of excess words for a key
                //Write out to final file.
                foreach (var de in map)
                {
                    //Get rid of data we cant keep, by removing the lowest counts
                    //while (de.Value.Count > 100) de.Value.RemoveLowest();
                    while (de.Value.Count > 10) de.Value.RemoveLowest();

                    //Create final line and write it out
                    var line = new StringBuilder();

                    line.Append(de.Key);
                    line.Append(':');
                    line.Append(string.Join(":", de.Value.Keys));
                    line.Append(':');
                    line.Append(string.Join(":", de.Value.Values));

                    output.Add(line.ToString());

                    if (output.Count > 1000)
                    {
                        await File.AppendAllLinesAsync(outputPath, output);
                        output.Clear();
                    }
                }

                //Write any final lines
                if (output.Count > 0) await File.AppendAllLinesAsync(outputPath, output);
            }
            catch (Exception ex)
            {
                WriteError($"Exception optimising {path}. {ex.Message}");
            }
        }

        //Read a file of key words, their related words and counts
        private static async Task<SortedDictionary<string, Dictionary<string, int>>> ReadCombineFile(string path)
        {
            //Loop through and turn file entries into a dictionary
            var result = new SortedDictionary<string, Dictionary<string, int>>();

            using (var reader = new StreamReader(path))
            {
                while (!reader.EndOfStream)
                {
                    //Read more lines into buffer at once
                    var lines = await reader.ReadLinesAsync(10);

                    foreach (var line in lines)
                    {
                        var splits = line.Split(':');
                        var count = splits.Length - 1;
                        var length = count / 2;
                        var key = splits[0];

                        var i = 1;
                        var words = new List<string>();
                        var values = new List<int>();

                        //Split the line after the key between the words and counts eg word:word:word:count:count:count
                        while (i <= count)
                        {
                            if (i <= length)
                            {
                                words.Add(splits[i]);
                            }
                            else
                            {
                                values.Add(Convert.ToInt32(splits[i]));
                            }

                            i++;
                        }

                        if (!result.ContainsKey(key))
                        {
                            var keyValues = new Dictionary<string, int>();

                            //Load the key values in the dictionary
                            //We shoudlnt, but sometime we get duplicates
                            for (var j = 0; j < words.Count; j++) keyValues.TryAdd(words[j], values[j]);

                            result.TryAdd(key, keyValues);
                        }
                        else
                        {
                            var keyValues = result[key];

                            for (var j = 0; j < words.Count; j++)
                            {
                                if (keyValues.ContainsKey(words[j]))
                                {
                                    keyValues[words[j]] = keyValues[words[j]] + values[j];
                                }
                                else
                                {
                                    keyValues.Add(words[j], values[j]);
                                }
                            }
                        }
                    }
                }
            }

            return result;
        }
    }
}
