using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Metacrack.Model;
using SQLite;

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
            if (!File.Exists(path))
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
                    var sessionManager = new SessionManager(fileName, options.Sessions, part);

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

                                //Lookup entity
                                var rowId = validEmail.ToRowId();
                                var rowChar = validEmail.ToRowCharId();
                                var bucket = rowChar.Char;

                                db.SetModifier(bucket);

                                var entity = db.Select<Entity>(rowId).FirstOrDefault();

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

                                    //Write out to buffered disk
                                    sessionManager.AddWords(hash, words);

                                    writeCount += words.Count;
                                }

                                //Update the percentage
                                if (lineCount % updateMod == 0) WriteProgress($"Processing {fileInfo.Name}", progressTotal, size);
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
                        sessionManager.Dispose();
                    }

                    WriteMessage($"Read {lineCount} lines and wrote {writeCount} lines for {fileInfo.Name}.");
                }
            }

            WriteMessage($"Completed at {DateTime.Now.ToShortTimeString()}.");
        }
    }
}
