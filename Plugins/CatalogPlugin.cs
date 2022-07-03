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
            var checkpointLines = 4000000;
            var flushLines = checkpointLines * 8;
            var currentDirectory = Directory.GetCurrentDirectory();

            var inputPath = Path.Combine(currentDirectory, options.InputPath);
            var fileEntries = Directory.GetFiles(Path.GetDirectoryName(inputPath), Path.GetFileName(inputPath));

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

            var isNew = !(Directory.Exists(outputPath) && Directory.GetFiles(outputPath).Length > 0);

            try
            {
                //Open up sqlite
                using (var db = new Database(outputPath))
                {
                    WriteMessage((isNew) ? "Creating new key value store" : "Please wait. Restoring key value store");

                    db.Restore();

                    //Get input files size
                    var fileEntriesSize = GetFileEntriesSize(fileEntries);

                    WriteMessage($"Found {fileEntries.Count()} text file entries ({FormatSize(fileEntriesSize)}) in all folders.");

                    progressTotal = 0L;
                    lineCount = 0L;
                    var validCount = 0L;
                    var invalidCount = 0L;
                    var fileCount = 0;

                    //Create a list of updates and inserts in memory
                    var inputBuckets = new Dictionary<char, List<Entity>>();
                    foreach (var hex in Hex)
                    {
                        inputBuckets.Add(hex, new List<Entity>());
                    }

                    var task = Task.CompletedTask;

                    WriteMessage($"Started adding values at {DateTime.Now.ToShortTimeString()}");

                    //Process a file
                    foreach (var lookupPath in fileEntries)
                    {
                        var line = "";
                        var fileName = Path.GetFileName(lookupPath);
                        fileCount++;

                        try
                        {
                            using (var reader = new StreamReader(lookupPath))
                            {
                                while (!reader.EndOfStream)
                                {
                                    line = reader.ReadLine();

                                    lineCount++;
                                    progressTotal += line.Length;

                                    var result = ProcessLine(line, options, fields, columns, lookups);

                                    if (result.Entity == null)
                                    {
                                        invalidCount++;
                                    }
                                    else
                                    {
                                        validCount++;
                                        inputBuckets[result.Bucket].Add(result.Entity);
                                    }

                                    if (lineCount % 10000 == 0) WriteProgress($"Processing {fileName}", progressTotal, fileEntriesSize);

                                    //Write to database after we process a certain number of lines (100 million or so)
                                    if (lineCount % checkpointLines == 0)
                                    {
                                        //We wait for the previous checkpoint to complete
                                        task.Wait();

                                        //Writes and clears the buckets
                                        WriteBuckets(db, inputBuckets);

                                        //Check if we are also at a full checkpoint (ie flush)
                                        if (lineCount % flushLines == 0)
                                        {
                                            //task = Task.Run(() => db.Flush());
                                            db.Flush();
                                            task = Task.CompletedTask;
                                        }
                                        else
                                        {
                                            task = Task.Run(() => db.Checkpoint());
                                        }

                                        isNew = false;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteError($"Exception processing {fileName} ({line}). {ex.Message}");
                        }
                    }

                    //Ensure final writes occur
                    task.Wait();

                    //Writes and clears the buckets
                    WriteBuckets(db, inputBuckets);

                    //Compaction doesnt appear to reduce the size of the store, but the option is available 
                    if (options.Compact)
                    {
                        WriteMessage($"Compacting key value store.");
                        db.Compact();
                    }

                    WriteMessage($"Flushing key value store data to disk.");
                    db.Flush();

                    WriteMessage($"Processed {validCount} lines out of {lineCount} ({invalidCount} invalid)");
                    WriteMessage($"Finished adding values at {DateTime.Now.ToShortTimeString()}");
                }
            }
            catch (Exception ex)
            {
                WriteError($"Exception writing catalog. {ex.Message}");
            }
        }

        private static (char Bucket, Entity Entity) ProcessLine(string line, CatalogOptions options, string[] fields, int[] columns, HashSet<string> lookups)
        {
            var lineSpan = line.AsSpan();
            var splits = lineSpan.SplitByChar(':');
            var entity = default(Entity);
            var fieldIndex = 0;
            var bucket = default(char);

            foreach (var (split, index) in splits)
            {
                //Get the email, stem it and validate it 
                if (index == 0)
                {
                    if (ValidateEmail(split, out var emailStem))
                    {
                        var rowChar = emailStem.ToRowIdAndChar();
                        entity = new Entity();
                        entity.RowId = rowChar.Id;

                        bucket = rowChar.Char;

                        //Stem email if required
                        if (options.StemEmail || options.StemEmailOnly) StemEmail(emailStem, lookups, entity);
                    }
                    else
                    {
                        //Dont continue getting values
                        return (default, default);
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
                            if (trimToken.Length > 0) entity.SetValue(trimToken.ToString(), fields[fieldIndex]);
                        }
                    }
                    else
                    {
                        entity.SetValue(split.ToString(), fields[fieldIndex]);
                    }
                    fieldIndex++;
                }

                //Shortcut if we have parsed all the data we need to for this line
                if (fieldIndex >= columns.Count()) break;
            }

            return (bucket, entity);
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
                //if (inserts.Count > 0) WriteBucket(hex, db, inserts);
            }

            Task.WhenAll(tasks).Wait();

            //Allow this memory to be freed
            GC.Collect();
        }

        public static void WriteBucket(char hex, Database db, List<Entity> inserts)
        {
            db.ReadModifyWriteAll(hex, inserts);
            inserts.Clear();
        }
    }
}
