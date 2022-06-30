using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Metacrack.Model;

namespace Metacrack.Plugins
{
    public class CatalogPlugin: PluginBase
    {
        public static void Process(CatalogOptions options)
        {
            //Validate and display arguments
            var memoryLines = 6000000 * 4; //20 millon = 3gb, so roughly 4GB
            var currentDirectory = Directory.GetCurrentDirectory();
            var fileEntries = Directory.GetFiles(currentDirectory, options.InputPath);

            if (fileEntries.Length == 0)
            {
                WriteError($"No .txt files found for {options.InputPath}");
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

            if (options.Tokenize) WriteMessage("Tokenize enabled");
            if (options.StemEmail) WriteMessage("Stem email enabled");
            if (options.StemEmailOnly) WriteMessage("Stem email only enabled");

            //Determine output path
            var outputPath = Path.Combine(currentDirectory, options.OutputFolder);
            WriteMessage($"Writing data to {outputPath}");

            //Determine input columns
            int[] columns = (options.Columns.Count() == 0) ? new int[] { 1 } : Array.ConvertAll(options.Columns.ToArray(), s => int.Parse(s));

            WriteMessage($"Using input columns: {String.Join(',', columns)}");

            //Determine fields
            string[] fields = (options.Fields.Count() == 0) ? new string[] { "p" } : options.Fields.ToArray();

            //Validate fields
            foreach (var field in fields)
            {
                if (!ValidFields.Contains(field))
                {
                    WriteError($"Invalid field {field}. Field must be one of the following: {string.Join(",", ValidFields)}");
                    return;
                }
            }

            //Validate fields and columns
            if (columns.Length != fields.Length)
            {
                WriteError($"Columns ({columns.Length}) cannot be mapped to the number of fields ({fields.Length}).");
                return;
            }

            //Load names input (if any)
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

            var isNew = File.Exists(outputPath) && Directory.GetFiles(outputPath).Count() > 0;

            //Open up sqlite
            using (var db = new Database(outputPath))
            {
                WriteMessage((isNew) ? "Creating new key value store" : "Found existing key value store");

                WriteMessage("Please wait. Restoring key value store");
                db.Restore();

                //Get input files size
                var fileEntriesSize = GetFileEntriesSize(fileEntries);

                WriteMessage($"Found {fileEntries.Count()} text file entries ({FormatSize(fileEntriesSize)}) in all folders.");

                progressTotal = 0L;
                lineCount = 0L;
                var validCount = 0L;
                var invalidCount = 0L;
                var fileCount = 0;

                WriteMessage($"Started adding values at {DateTime.Now.ToShortTimeString()}");

                //Process a file
                foreach (var lookupPath in fileEntries)
                {
                    //Create a list of updates and inserts in memory per file
                    var inputBuckets = new Dictionary<char, List<Entity>>();

                    foreach (var hex in Hex)
                    {
                        inputBuckets.Add(hex, new List<Entity>());
                    }

                    fileCount++;

                    using (var reader = new StreamReader(lookupPath))
                    {
                        while (!reader.EndOfStream)
                        {
                            lineCount++;

                            var line = reader.ReadLine().AsSpan();
                            var splits = line.SplitByChar(':');
                            var entity = default(Entity);
                            var fieldIndex = 0;

                            progressTotal += line.Length;

                            foreach (var (split, index) in splits)
                            {
                                //Get the email, stem it and validate it 
                                if (index == 0)
                                {
                                    if (ValidateEmail(split, out var emailStem))
                                    {
                                        validCount++;

                                        var rowChar = emailStem.ToRowIdAndChar();
                                        var bucket = rowChar.Char;
                                        var rowId = rowChar.Id;

                                        var inputs = inputBuckets[bucket];

                                        entity = new Entity();
                                        entity.RowId = rowId;

                                        inputs.Add(entity);

                                        //Stem email if required
                                        if (options.StemEmail || options.StemEmailOnly) StemEmail(emailStem, lookups, entity);
                                    }
                                    else
                                    {
                                        //Dont continue getting values
                                        invalidCount++;
                                        break;
                                    }
                                }
                                //Else map the index to the correct entity type and perform and functions
                                else if (!options.StemEmailOnly && columns.Contains(index))
                                {
                                    if (options.Tokenize)
                                    {
                                        var tokens = split.SplitByChar(' ');
                                        foreach (ReadOnlySpan<char> token in tokens)
                                        {
                                            //We trim the token, but we dont change capitalisation. We leave that to the lookup
                                            var trimToken = token.Trim();
                                            if (trimToken.Length > 0) entity.SetValue(trimToken, fields[fieldIndex]);
                                        }
                                    }
                                    else
                                    {
                                        entity.SetValue(split, fields[fieldIndex]);
                                    }
                                    fieldIndex++;
                                }

                                //Shortcut if we have parsed all the data we need to for this line
                                if (fieldIndex >= columns.Count()) break;
                            }

                            if (lineCount % 10000 == 0) WriteProgress($"Adding values", progressTotal, fileEntriesSize);

                            //Write to database after we process a certain number of lines (100 million or so)
                            if (lineCount % memoryLines == 0)
                            {
                                //Writes and clears the buckets
                                WriteMessage($"Writing checkpoint");
                                
                                WriteBuckets(db, inputBuckets);
                                db.Checkpoint();

                                isNew = false;
                            }
                        }
                    }

                    WriteMessage($"Writing final values to catalog.");
                    WriteBuckets(db, inputBuckets);

                    //Update the files percentage
                    WriteProgress($"Processing file {fileCount} of {fileEntries.Length}", progressTotal, fileEntriesSize);
                }

                WriteMessage($"Flushing key value store data to disk.");
                db.Flush();

                WriteMessage($"Processed {validCount} lines out of {lineCount} ({invalidCount} invalid)");
                WriteMessage($"Finished adding values at {DateTime.Now.ToShortTimeString()}");
            }
        }

        private static void WriteBuckets(Database db, Dictionary<char, List<Entity>> insertBuckets)
        {
            //Write out the inserts and updates, and set the file creation type to something other than created
            var tasks = new List<Task>();

            foreach (var hex in Hex)
            {
                var inserts = insertBuckets[hex];

                //If we are looping, then we always need to do an update
                if (inserts.Count > 0) tasks.Add(Task.Run(() => WriteBucket(hex, db, inserts)));
            }

            Task.WhenAll(tasks).Wait(); 

            //Lets also allow managed code to collect this memory
            GC.Collect();
        }

        public static void WriteBucket(char hex, Database db, List<Entity> inserts)
        {
            db.UpsertAll(hex, inserts);
            inserts.Clear();
        }
    }
}
