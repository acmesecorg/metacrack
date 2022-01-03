using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Metacrack.Model;
using SQLite;

namespace Metacrack.Plugins
{
    public class CatalogPlugin: PluginBase
    {
        public static void Process(CatalogOptions options)
        {
            //Validate and display arguments
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
            var outputPath = Path.Combine(currentDirectory, options.OutputPath);
            WriteMessage($"Writing data to {outputPath}");

            //Determine input columns
            int[] columns = (options.Columns.Count() == 0) ? new int[] {1} : Array.ConvertAll(options.Columns.ToArray(), s => int.Parse(s));

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

            //Open up sqlite
            var db = new SQLiteConnection(outputPath);
            var entityResult = db.CreateTable<Entity>();

            WriteMessage((entityResult == CreateTableResult.Created) ? "Created new meta data table": "Found existing meta data table");

            //Get input files size
            var fileEntriesSize = GetFileEntriesSize(fileEntries);

            WriteMessage($"Found {fileEntries.Count()} text file entries ({FormatSize(fileEntriesSize)}) in all folders.");

            progressTotal = 0L;
            lineCount = 0L;
            var validCount = 0L;
            var fileCount = 0;

            WriteMessage($"Started adding values at {DateTime.Now.ToShortTimeString()}");

            //Process a file
            foreach (var lookupPath in fileEntries)
            {
                //Create a list of updates and inserts in memory per file
                var inserts = new Dictionary<long, Entity>();
                var updates = new Dictionary<long, Entity>();

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

                        foreach (var (split,index) in splits)
                        {
                            //Get the email, stem it and validate it 
                            if (index == 0)
                            {
                                if (ValidateEmail(split, out var emailStem))
                                {
                                    validCount++;

                                    var rowId = emailStem.ToRowId();

                                    //Determine if we already have an entity for this file
                                    //If we do, it will already be in the inserts and updates, and we will just update values
                                    if (!inserts.TryGetValue(rowId, out entity) && !updates.TryGetValue(rowId, out entity))
                                    {
                                        //Otherwise check if we have en entity in the database (if it existed first)
                                        entity = (entityResult == CreateTableResult.Created) ? default : db.Table<Entity>().Where(e => e.RowId == rowId).FirstOrDefault();

                                        //Not found in the database, so create a new one
                                        if (entity == null)
                                        {
                                            entity = new Entity();
                                            entity.RowId = rowId;

                                            inserts.Add(rowId, entity);
                                        }
                                        else
                                        {
                                            updates.Add(rowId, entity);
                                        }
                                    }

                                    //Stem email if required
                                    if (options.StemEmail || options.StemEmailOnly) StemEmail(emailStem, lookups, entity);
                                }
                                else
                                {
                                    //Dont continue getting values
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
                    }
                }

                WriteMessage($"Writing values to catalog.");

                //Write out the inserts and updates, and set the file creation type to something other than created
                if (updates.Count > 0) db.UpdateAll(updates.Values);
                if (inserts.Count > 0) db.InsertAll(inserts.Values);

                entityResult = CreateTableResult.Migrated;

                //Update the percentage
                WriteProgress($"Processing file {fileCount} of {fileEntries.Length}", progressTotal, fileEntriesSize);
            }

            WriteMessage($"Processed {validCount} lines out of {lineCount}");
            WriteMessage($"Finished adding values at {DateTime.Now.ToShortTimeString()}");
        }
    }
}
