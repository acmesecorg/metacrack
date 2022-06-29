using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Metacrack.Model;

namespace Metacrack
{
    public class LookupPlugin: PluginBase
    {
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

            //Work out if we are using a rule filter
            var rules = default(List<List<string>>);

            if (!string.IsNullOrEmpty(options.RulePath))
            {
                var rulePath = Path.Combine(currentDirectory, options.RulePath);
                if (!File.Exists(rulePath))
                {
                    WriteError($"No rule file found for {options.RulePath}");
                    return;
                }

                rules = GetRules(rulePath);
                if (rules != null) WriteMessage($"Loaded {rules.Count()} rules from {options.RulePath}");
            }

            //Try to load catalog
            var path = Path.Combine(currentDirectory, options.CatalogPath);
            if (!Directory.Exists(path))
            {
                WriteError($"Catalog path not found: {path}");
                return;
            }

            //Parse part
            if (options.Part.Length == 1) options.Part = $"{options.Part}000000";
            if (!TryParse(options.Part, out var part))
            {
                WriteError($"Could not parse part value {options.Part} to a number.");
                return;
            }
            if (part > 0 && part < 100000)
            {
                WriteError($"Part value {part} should not be less than 100000.");
                return;
            }

            if (options.HashType > 0) WriteMessage($"Validating hash mode {options.HashType}");

            //Determine fields
            string[] fields = (options.Fields.Count() == 0) ? new string[] { "p" } : options.Fields.ToArray();

            Console.WriteLine($"Started at {DateTime.Now.ToShortTimeString()}.");

            var size = GetFileEntriesSize(fileEntries);
            var progressTotal = 0L;

            var updateMod = 1000;
            if (size > 100000000) updateMod = 10000;

            var hashInfo = GetHashInfo(options.HashType);
            
            //We initially load the row ids and their associated hashes 
            var identifiers = new Dictionary<char, List<long>>();
            var hashes = new Dictionary<char, List<string>>();

            foreach (var hex in Hex)
            {
                identifiers.Add(hex, new List<long>());
                hashes.Add(hex, new List<string>());    
            }

            //Open database
            using (var db = new Database(path, true))
            {
                foreach (var filePath in fileEntries)
                {
                    //Create a version based on the file size, so that the hash and dict are bound together
                    var fileInfo = new FileInfo(filePath);
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var lineCount = 0L;
                    var writeCount = 0L;
                    //var sessionManager = new SessionManager(fileName, options.Sessions, part);

                    //Loop through the file
                    //A line must be valid+email:hash valid+email:hash:salt
                    try
                    {
                        using (var reader = new StreamReader(filePath))
                        {
                            while (!reader.EndOfStream)
                            {
                                var line = reader.ReadLine().AsSpan();

                                lineCount++;
                                progressTotal += line.Length;

                                var index = line.IndexOf(':');

                                if (index == -1) continue;

                                var email = line.Slice(0, index);
                                var hash = line.Slice(index + 1);

                                //Check has email and has hash
                                if (email.Length == 0 || hash.Length == 0) continue;

                                //Validate the email
                                if (!ValidateEmail(email, out string validEmail)) continue;

                                //Check hash looks correct
                                var hashParts = hash.SplitByChar(':');
                                var count = 0;

                                //Loop through hash parts here and validate hash portion, salt portion, and count if not correct vv hashinfo
                                foreach (var (hashPart, index2) in hashParts)
                                {
                                    if (index2 == 0 && !ValidateHash(hashPart, hashInfo)) continue;
                                    if (index2 == 1 && !ValidateSalt(hashPart, hashInfo)) continue;
                                    count++;
                                }

                                //Validate if we have correct column count in hash
                                if (count != hashInfo.Columns) continue;

                                //Place in a bucket for parallel processing
                                var rowChar = validEmail.ToRowIdAndChar();
                                var rowId = rowChar.Id;
                                var bucket = rowChar.Char;

                                identifiers[bucket].Add(rowId);
                                hashes[bucket].Add(hash.ToString());

                                //Update the percentage
                                if (lineCount % updateMod == 0) WriteProgress($"Reading {fileInfo.Name} into memory", progressTotal, size);
                            }
                        }

                        //Do threaded lookups and writes here
                        var progressIndicator = new Progress<long>((long value) => WriteMessage($"Got progress value {value}"));
                        var tasks = new List<Task>();

                        foreach (var hex in Hex)
                        {
                            writeCount += AddValues(progressIndicator, db, identifiers[hex], hashes[hex], fields, rules, options, fileName, hex);
                            //tasks.Add(task);
                        }

                        while (tasks.Count > 0)
                        {
                            var completedTask = Task.WhenAny(tasks.ToArray()).GetAwaiter().GetResult();
                            tasks.Remove(completedTask);
                        }

                        var finalHashPath = Path.Combine(currentDirectory, $"{fileName}.hash");
                        var finalWordPath = Path.Combine(currentDirectory, $"{fileName}.word");

                        //Combine all the files here
                        //TODO: make this work for file sizes larger than memory
                        foreach (var hex in Hex)
                        {
                            var hashPath = Path.Combine(currentDirectory, $"{fileName}.{hex}.hash");
                            var wordPath = Path.Combine(currentDirectory, $"{fileName}.{hex}.word");

                            if (File.Exists(hashPath))
                            { 
                                File.AppendAllLines(finalHashPath, File.ReadAllLines(hashPath));
                                File.AppendAllLines(finalWordPath, File.ReadAllLines(wordPath));

                                File.Delete(hashPath);
                                File.Delete(wordPath);
                            }
                        }
                    }
                    catch (SessionException ex)
                    {
                        WriteError(ex.Message);
                        continue;
                    }
                    finally
                    {
                        //Ensure data is saved
                        //sessionManager.Dispose();
                    }

                    WriteMessage($"Read {lineCount} lines and wrote {writeCount} lines for {fileInfo.Name}.");
                }
            }


            WriteMessage($"Completed at {DateTime.Now.ToShortTimeString()}.");
        }

        private static long AddValues(IProgress<long> progress, Database db, List<long> identifiers, List<string> hashes, string[] fields, List<List<string>> rules, LookupOptions options, string filename, char hex)
        {
            var writeCount = 0L;
            var bufferCount = 0L;

            var currentDirectory = Directory.GetCurrentDirectory();
            var hashPath = Path.Combine(currentDirectory, $"{filename}.{hex}.hash");
            var wordPath = Path.Combine(currentDirectory, $"{filename}.{hex}.word");

            var hashesBuffer = new List<string>();
            var wordsBuffer = new List<string>();

            for (var i = 0; i < identifiers.Count; i++)
            {
                var entity = db.Select(hex, identifiers[i]);

                //An entry was found in the table
                if (entity != null)
                {
                    //Get distinct list of words from the entity
                    var words = entity.GetValues(fields);

                    //If there are rules, remove any words returned from 
                    //Otherwise just make sure they are distinct
                    if (rules != null) words = RulesEngine.FilterByRules(words, rules);

                    //Limit the words to the maximum
                    if (words.Count() > options.HashMaximum) words = words.Take(options.HashMaximum).ToList();

                    var hash = hashes[i];

                    foreach (var word in words)
                    {
                        hashesBuffer.Add(hash);
                        wordsBuffer.Add(word);
                    }

                    writeCount += words.Count;
                    bufferCount += words.Count;
                }

                //Check if we need to append 
                if (bufferCount > 1000)
                {
                    File.AppendAllLines(hashPath, hashesBuffer.ToArray());
                    File.AppendAllLines(wordPath, wordsBuffer.ToArray());

                    hashesBuffer.Clear();
                    wordsBuffer.Clear();

                    progress?.Report(bufferCount);
                    bufferCount = 0;
                }
            }

            //Write final values
            //Check if we need to append 
            if (bufferCount > 0)
            {
                File.AppendAllLines(hashPath, hashesBuffer.ToArray());
                File.AppendAllLines(wordPath, wordsBuffer.ToArray());

                hashesBuffer.Clear();
                wordsBuffer.Clear();

                progress?.Report(bufferCount);
                bufferCount = 0;
            }

            return writeCount;
        }
    }
}
