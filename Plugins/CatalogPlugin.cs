using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Metacrack.Model;
using SQLite;

using Sqlite3Statement = SQLitePCL.sqlite3_stmt;

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
            var outputPath = Path.Combine(currentDirectory, options.OutputPath);
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

            var connectString = new SQLiteConnectionString(outputPath, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.NoMutex, true);

            //Open up sqlite
            using (var db = new SQLiteConnection(connectString))
            {
                WriteMessage($"Using Sqlite version {db.LibVersionNumber} .");

                var types = Entity.GetTypes();
                var entityResult = db.CreateTable<Entity0>();
                var isNew = entityResult == CreateTableResult.Created;

                //Loop through and create other tables
                foreach (var type in types)
                {
                    db.CreateTable(type);
                }

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
                    var insertBuckets = new Dictionary<char, Dictionary<long, Entity>>();
                    var updateBuckets = new Dictionary<char, Dictionary<long, Entity>>();

                    foreach (var hex in Hex)
                    {
                        insertBuckets.Add(hex, new Dictionary<long, Entity>());
                        updateBuckets.Add(hex, new Dictionary<long, Entity>());
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

                            foreach (var (split,index) in splits)
                            {
                                //Get the email, stem it and validate it 
                                if (index == 0)
                                {
                                    if (ValidateEmail(split, out var emailStem))
                                    {
                                        validCount++;

                                        var rowChar = emailStem.ToRowCharId();
                                        var bucket = rowChar.Char;
                                        var rowId = rowChar.Id;

                                        var inserts = insertBuckets[bucket];
                                        var updates = updateBuckets[bucket];

                                        //Determine if we already have an entity for this file
                                        //If we do, it will already be in the inserts and updates, and we will just update values
                                        if (!inserts.TryGetValue(rowId, out entity) && !updates.TryGetValue(rowId, out entity))
                                        {
                                            //Otherwise check if we have an entity in the database (if it existed first)
                                            entity = (isNew) ? default : Entity.GetEntity(db, bucket, rowId);

                                            //Not found in the database, so create a new one
                                            if (entity == null)
                                            {
                                                entity = Entity.Create(bucket);
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

                            //Write to database after we process a certain number of lines (100 million or so)
                            if (lineCount % memoryLines == 0)
                            {
                                //Writes and clears the buckets
                                WriteBuckets(db, insertBuckets, updateBuckets, isNew);

                                entityResult = CreateTableResult.Migrated;
                                isNew = false;
                            }
                        }
                    }

                    WriteMessage($"Writing final values to catalog.");
                    WriteBuckets(db, insertBuckets, updateBuckets, isNew);

                    //Update the files percentage
                    WriteProgress($"Processing file {fileCount} of {fileEntries.Length}", progressTotal, fileEntriesSize);
                }

                WriteMessage($"Processed {validCount} lines out of {lineCount}");
                WriteMessage($"Finished adding values at {DateTime.Now.ToShortTimeString()}");
            }
        }

        private static void WriteBuckets(SQLiteConnection db, Dictionary<char, Dictionary<long, Entity>> insertBuckets, Dictionary<char, Dictionary<long, Entity>> updateBuckets, bool isNew)
        {
            //Write out the inserts and updates, and set the file creation type to something other than created
            var count = 0;

            //Sqlite doesnt handle concurrent writes well
            //Instead we keep the table size down by splitting tables, which still makes updates quicker
            foreach (var hex in Hex)
            {
                WriteProgress($"Writing bucket {hex}", count, 15);

                var inserts = insertBuckets[hex];
                var updates = updateBuckets[hex];
                var type = Entity.GetTypes()[count];

                //If we are looping, then we always need to do an update
                if (isNew)
                {
                    if (updates.Count > 0) db.UpdateAll(updates.Values, true);
                    if (inserts.Count > 0) db.InsertAll(inserts.Values, type, true);
                }
                else
                {
                    if (updates.Count > 0) db.UpdateAll(updates.Values);
                    if (inserts.Count > 0) db.UpdateAll(inserts.Values);
                }

                updates.Clear();
                inserts.Clear();

                count++;
            }

            //Lets also allow managed code to collect this memory
            GC.Collect();
        }

        private static void InsertAll(SQLiteConnection db, System.Collections.IEnumerable objects)
        {
            var map = db.GetMapping(Orm.GetType(new Entity()));

            db.RunInTransaction(() => {
                foreach (var r in objects)
                {
                    Insert(db, r, map);
                }
            });
        }

        public static void Insert(SQLiteConnection db, object obj, TableMapping map)
        {
            var cols = map.InsertColumns;
            var vals = new object[cols.Length];

            for (var i = 0; i < vals.Length; i++)
            {
                vals[i] = cols[i].GetValue(obj);
            }

            var insertCmd = GetInsertCommand(db, map);

            // We lock here to protect the prepared statement returned via GetInsertCommand.
            // A SQLite prepared statement can be bound for only one operation at a time.
            try
            {
                insertCmd.ExecuteNonQuery(vals);
            }
            catch (SQLiteException ex)
            {
                if (SQLite3.ExtendedErrCode(db.Handle) == SQLite3.ExtendedResult.ConstraintNotNull)
                {
                    throw NotNullConstraintViolationException.New(ex.Result, ex.Message, map, obj);
                }
                throw;
            }

            //if (map.HasAutoIncPK)
            //{
            //    var id = SQLite3.LastInsertRowid(db.Handle);
            //    map.SetAutoIncPK(obj, id);
            //}
        }

        private static PreparedSqlLiteInsertCommand GetInsertCommand(SQLiteConnection db, TableMapping map)
        {
            PreparedSqlLiteInsertCommand prepCmd;

            var key = Tuple.Create(map.MappedType.FullName, "");

            //Get from cache, we will always do this
            //lock (_insertCommandMap)
            //{
            //    if (_insertCommandMap.TryGetValue(key, out prepCmd))
            //    {
            //        return prepCmd;
            //    }
            //}

            var cols = map.InsertColumns;
            string insertSql;

            if (cols.Length == 0 && map.Columns.Length == 1 && map.Columns[0].IsAutoInc)
            {
                insertSql = string.Format($"insert into \"{map.TableName}\" default values");
            }
            else
            {
                insertSql = string.Format("insert into \"{0}\"({1}) values ({2})", map.TableName,
                                   string.Join(",", (from c in cols
                                                     select "\"" + c.Name + "\"").ToArray()),
                                   string.Join(",", (from c in cols
                                                     select "?").ToArray()));

            }

            prepCmd = new PreparedSqlLiteInsertCommand(db, insertSql);

            //lock (_insertCommandMap)
            //{
            //    if (_insertCommandMap.TryGetValue(key, out var existing))
            //    {
            //        prepCmd.Dispose();
            //        return existing;
            //    }

            //    _insertCommandMap.Add(key, prepCmd);
            //}

            return prepCmd;
        }


        /// <summary>
        /// Since the insert never changed, we only need to prepare once.
        /// </summary>
        class PreparedSqlLiteInsertCommand : IDisposable
        {
            bool Initialized;

            SQLiteConnection Connection;

            string CommandText;

            Sqlite3Statement Statement;
            static readonly Sqlite3Statement NullStatement = default(Sqlite3Statement);

            public PreparedSqlLiteInsertCommand(SQLiteConnection conn, string commandText)
            {
                Connection = conn;
                CommandText = commandText;
            }

            public int ExecuteNonQuery(object[] source)
            {
                if (Initialized && Statement == NullStatement)
                {
                    throw new ObjectDisposedException(nameof(PreparedSqlLiteInsertCommand));
                }

                if (Connection.Trace)
                {
                    Connection.Tracer?.Invoke("Executing: " + CommandText);
                }

                var r = SQLite3.Result.OK;

                if (!Initialized)
                {
                    Statement = SQLite3.Prepare2(Connection.Handle, CommandText);
                    Initialized = true;
                }

                //bind the values.
                if (source != null)
                {
                    for (int i = 0; i < source.Length; i++)
                    {
                        BindParameter(Statement, i + 1, source[i], Connection.StoreDateTimeAsTicks, Connection.DateTimeStringFormat, Connection.StoreTimeSpanAsTicks);
                    }
                }
                r = SQLite3.Step(Statement);

                if (r == SQLite3.Result.Done)
                {
                    int rowsAffected = SQLite3.Changes(Connection.Handle);
                    SQLite3.Reset(Statement);
                    return rowsAffected;
                }
                else if (r == SQLite3.Result.Error)
                {
                    string msg = SQLite3.GetErrmsg(Connection.Handle);
                    SQLite3.Reset(Statement);
                    throw SQLiteException.New(r, msg);
                }
                else if (r == SQLite3.Result.Constraint && SQLite3.ExtendedErrCode(Connection.Handle) == SQLite3.ExtendedResult.ConstraintNotNull)
                {
                    SQLite3.Reset(Statement);
                    throw NotNullConstraintViolationException.New(r, SQLite3.GetErrmsg(Connection.Handle));
                }
                else
                {
                    SQLite3.Reset(Statement);
                    throw SQLiteException.New(r, SQLite3.GetErrmsg(Connection.Handle));
                }
            }

            static IntPtr NegativePointer = new IntPtr(-1);

            internal static void BindParameter(Sqlite3Statement stmt, int index, object value, bool storeDateTimeAsTicks, string dateTimeStringFormat, bool storeTimeSpanAsTicks)
            {
                if (value == null)
                {
                    SQLite3.BindNull(stmt, index);
                }
                else
                {
                    if (value is Int32)
                    {
                        SQLite3.BindInt(stmt, index, (int)value);
                    }
                    else if (value is String)
                    {
                        SQLite3.BindText(stmt, index, (string)value, -1, NegativePointer);
                    }
                    else if (value is Byte || value is UInt16 || value is SByte || value is Int16)
                    {
                        SQLite3.BindInt(stmt, index, Convert.ToInt32(value));
                    }
                    else if (value is Boolean)
                    {
                        SQLite3.BindInt(stmt, index, (bool)value ? 1 : 0);
                    }
                    else if (value is UInt32 || value is Int64)
                    {
                        SQLite3.BindInt64(stmt, index, Convert.ToInt64(value));
                    }
                    else if (value is Single || value is Double || value is Decimal)
                    {
                        SQLite3.BindDouble(stmt, index, Convert.ToDouble(value));
                    }
                    else if (value is TimeSpan)
                    {
                        if (storeTimeSpanAsTicks)
                        {
                            SQLite3.BindInt64(stmt, index, ((TimeSpan)value).Ticks);
                        }
                        else
                        {
                            SQLite3.BindText(stmt, index, ((TimeSpan)value).ToString(), -1, NegativePointer);
                        }
                    }
                    else if (value is DateTime)
                    {
                        if (storeDateTimeAsTicks)
                        {
                            SQLite3.BindInt64(stmt, index, ((DateTime)value).Ticks);
                        }
                        else
                        {
                            SQLite3.BindText(stmt, index, ((DateTime)value).ToString(dateTimeStringFormat, System.Globalization.CultureInfo.InvariantCulture), -1, NegativePointer);
                        }
                    }
                    else if (value is DateTimeOffset)
                    {
                        SQLite3.BindInt64(stmt, index, ((DateTimeOffset)value).UtcTicks);
                    }
                    else if (value is byte[])
                    {
                        SQLite3.BindBlob(stmt, index, (byte[])value, ((byte[])value).Length, NegativePointer);
                    }
                    else if (value is Guid)
                    {
                        SQLite3.BindText(stmt, index, ((Guid)value).ToString(), 72, NegativePointer);
                    }
                    else if (value is Uri)
                    {
                        SQLite3.BindText(stmt, index, ((Uri)value).ToString(), -1, NegativePointer);
                    }
                    else if (value is StringBuilder)
                    {
                        SQLite3.BindText(stmt, index, ((StringBuilder)value).ToString(), -1, NegativePointer);
                    }
                    else if (value is UriBuilder)
                    {
                        SQLite3.BindText(stmt, index, ((UriBuilder)value).ToString(), -1, NegativePointer);
                    }
                    //else
                    //{
                    //    // Now we could possibly get an enum, retrieve cached info
                    //    var valueType = value.GetType();
                    //    var enumInfo = EnumCache.GetInfo(valueType);
                    //    if (enumInfo.IsEnum)
                    //    {
                    //        var enumIntValue = Convert.ToInt32(value);
                    //        if (enumInfo.StoreAsText)
                    //            SQLite3.BindText(stmt, index, enumInfo.EnumValues[enumIntValue], -1, NegativePointer);
                    //        else
                    //            SQLite3.BindInt(stmt, index, enumIntValue);
                    //    }
                    //    else
                    //    {
                    //        throw new NotSupportedException("Cannot store type: " + Orm.GetType(value));
                    //    }
                    //}
                }
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            void Dispose(bool disposing)
            {
                var s = Statement;
                Statement = NullStatement;
                Connection = null;
                if (s != NullStatement)
                {
                    SQLite3.Finalize(s);
                }
            }

            ~PreparedSqlLiteInsertCommand()
            {
                Dispose(false);
            }
        }
    }
}
