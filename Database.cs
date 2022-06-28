using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Metacrack.Model;
using SQLite;
using static SQLite.SQLiteConnection;
using Sqlite3Statement = SQLitePCL.sqlite3_stmt;


namespace Metacrack
{
    public class Database: IDisposable 
    {
        private static IntPtr _negativePointer = new IntPtr(-1);
        
        private Type _lastMapType;
        private TableMapping _lastMap;

        private SQLiteConnection _db;
        private char _hex;
        private bool _disposedValue;

        private struct IndexInfo
        {
            public string IndexName;
            public string TableName;
            public bool Unique;
            public List<IndexedColumn> Columns;
        }

        private struct IndexedColumn
        {
            public int Order;
            public string ColumnName;
        }

        public Database(string outputPath, bool readOnly = false)
        {
            var flags = (readOnly) ? SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.NoMutex : SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.NoMutex;
            var connectString = new SQLiteConnectionString(outputPath, flags, true);
            _db = new SQLiteConnection(connectString);
        }

        //Set the table name modifier which is appended to the table name for all database operations
        public void SetModifier(char hex)
        {
            _hex = hex;
        }

        public CreateTableResult CreateTable<T>(CreateFlags createFlags = CreateFlags.None)
        {
            var map = _db.GetMapping(typeof(T), createFlags);

            // Check if the table exists
            var result = CreateTableResult.Created;
            var existingCols = _db.GetTableInfo(map.TableName + _hex);

            // Create or migrate it
            if (existingCols.Count == 0)
            {
                // Facilitate virtual tables a.k.a. full-text search.
                bool fts3 = (createFlags & CreateFlags.FullTextSearch3) != 0;
                bool fts4 = (createFlags & CreateFlags.FullTextSearch4) != 0;
                bool fts = fts3 || fts4;
                var @virtual = fts ? "virtual " : string.Empty;
                var @using = fts3 ? "using fts3 " : fts4 ? "using fts4 " : string.Empty;

                // Build query.
                var query = "create " + @virtual + "table if not exists \"" + map.TableName + _hex + "\" " + @using + "(\n";
                var decls = map.Columns.Select(p => Orm.SqlDecl(p, _db.StoreDateTimeAsTicks, _db.StoreTimeSpanAsTicks));
                var decl = string.Join(",\n", decls.ToArray());
                query += decl;
                query += ")";
                if (map.WithoutRowId)
                {
                    query += " without rowid";
                }

                _db.Execute(query);
            }
            else
            {
                result = CreateTableResult.Migrated;
                MigrateTable(map, existingCols);
            }

            var indexes = new Dictionary<string, IndexInfo>();
            foreach (var c in map.Columns)
            {
                foreach (var i in c.Indices)
                {
                    var iname = i.Name ?? map.TableName + "_" + c.Name;
                    IndexInfo iinfo;
                    if (!indexes.TryGetValue(iname, out iinfo))
                    {
                        iinfo = new IndexInfo
                        {
                            IndexName = iname,
                            TableName = map.TableName,
                            Unique = i.Unique,
                            Columns = new List<IndexedColumn>()
                        };
                        indexes.Add(iname, iinfo);
                    }

                    if (i.Unique != iinfo.Unique)
                        throw new Exception("All the columns in an index must have the same value for their Unique property");

                    iinfo.Columns.Add(new IndexedColumn
                    {
                        Order = i.Order,
                        ColumnName = c.Name
                    });
                }
            }

            foreach (var indexName in indexes.Keys)
            {
                var index = indexes[indexName];
                var columns = index.Columns.OrderBy(i => i.Order).Select(i => i.ColumnName).ToArray();
                _db.CreateIndex(indexName, index.TableName, columns, index.Unique);
            }

            return result;
        }

        public void InsertAll<T>(IEnumerable objects)
        {
            var map = _db.GetMapping(Orm.GetType(typeof(T)));
            var insertStatement = GetInsertCommand(map);

            try
            {
                _db.RunInTransaction(() =>
                {
                    foreach (var r in objects)
                    {
                        Insert(r, map, insertStatement);
                    }
                });
            }
            finally
            {
                SQLite3.Finalize(insertStatement);
            }
        }

        public void UpdateAll<T>(IEnumerable objects)
        {
            var map = _db.GetMapping(Orm.GetType(typeof(T)));

            try
            {
                _db.RunInTransaction(() =>
                {
                    foreach (var r in objects)
                    {
                        Update(r, map);
                    }
                });
            }
            finally
            {

            }
        }

        public List<T> Select<T>(object id)
        {
            var map = _lastMap;

            if (typeof(T) != _lastMapType)
            {
                _lastMapType = typeof(T);
                _lastMap = _db.GetMapping(Orm.GetType(_lastMapType));
                map = _lastMap;
            }

            var cmdText = "select * from \"" + map.TableName + _hex + "\" where " + map.PK.Name + "=?";
            var command =  _db.CreateCommand(cmdText, new object[] { id });
            return command.ExecuteQuery<T>();
        }

        private void Insert(object obj, TableMapping map, Sqlite3Statement insertStatement)
        {
            var cols = map.InsertColumns;
            var vals = new object[cols.Length];

            for (var i = 0; i < vals.Length; i++)
            {
                vals[i] = cols[i].GetValue(obj);
            }

            // We lock here to protect the prepared statement returned via GetInsertCommand.
            // A SQLite prepared statement can be bound for only one operation at a time.
            try
            {
                ExecuteNonQuery(insertStatement, vals);
            }
            catch (SQLiteException ex)
            {
                if (SQLite3.ExtendedErrCode(_db.Handle) == SQLite3.ExtendedResult.ConstraintNotNull)
                {
                    throw NotNullConstraintViolationException.New(ex.Result, ex.Message, map, obj);
                }
                throw;
            }
        }

        private void Update(object obj, TableMapping map)
        {
            var pk = map.PK;

            if (pk == null)
            {
                throw new NotSupportedException("Cannot update " + map.TableName + ": it has no PK");
            }

            var cols = from p in map.Columns
                       where p != pk
                       select p;
            var vals = from c in cols
                       select c.GetValue(obj);
            var ps = new List<object>(vals);
            
            if (ps.Count == 0)
            {
                // There is a PK but no accompanying data,
                // so reset the PK to make the UPDATE work.
                cols = map.Columns;
                vals = from c in cols
                       select c.GetValue(obj);
                ps = new List<object>(vals);
            }
            ps.Add(pk.GetValue(obj));

            var q = string.Format("update \"{0}\" set {1} where \"{2}\" = ? ", map.TableName + _hex, 
                string.Join(",", (from c in cols                                                                                                                
                    select "\"" + c.Name + "\" = ? ").ToArray()), pk.Name);

            try
            {
                _db.Execute(q, ps.ToArray());
            }
            catch (SQLiteException ex)
            {

                if (ex.Result == SQLite3.Result.Constraint && SQLite3.ExtendedErrCode(_db.Handle) == SQLite3.ExtendedResult.ConstraintNotNull)
                {
                    throw NotNullConstraintViolationException.New(ex, map, obj);
                }

                throw ex;
            }
        }

        private Sqlite3Statement GetInsertCommand(TableMapping map)
        {
            var cols = map.InsertColumns;
            var insertSql = string.Format("insert into \"{0}\"({1}) values ({2})", map.TableName + _hex,
                                string.Join(",", (from c in cols
                                                  select "\"" + c.Name + "\"").ToArray()),
                                string.Join(",", (from c in cols
                                                  select "?").ToArray()));

            return SQLite3.Prepare2(_db.Handle, insertSql);
        }

        private int ExecuteNonQuery(Sqlite3Statement statement, object[] source)
        {
            var r = SQLite3.Result.OK;

            //bind the values.
            if (source != null)
            {
                for (int i = 0; i < source.Length; i++)
                {
                    BindParameter(statement, i + 1, source[i], _db.StoreDateTimeAsTicks, _db.DateTimeStringFormat, _db.StoreTimeSpanAsTicks);
                }
            }

            r = SQLite3.Step(statement);

            if (r == SQLite3.Result.Done)
            {
                int rowsAffected = SQLite3.Changes(_db.Handle);
                SQLite3.Reset(statement);
                return rowsAffected;
            }
            else if (r == SQLite3.Result.Error)
            {
                string msg = SQLite3.GetErrmsg(_db.Handle);
                SQLite3.Reset(statement);
                throw SQLiteException.New(r, msg);
            }
            else if (r == SQLite3.Result.Constraint && SQLite3.ExtendedErrCode(_db.Handle) == SQLite3.ExtendedResult.ConstraintNotNull)
            {
                SQLite3.Reset(statement);
                throw NotNullConstraintViolationException.New(r, SQLite3.GetErrmsg(_db.Handle));
            }
            else
            {
                SQLite3.Reset(statement);
                throw SQLiteException.New(r, SQLite3.GetErrmsg(_db.Handle));
            }
        }

        private void BindParameter(Sqlite3Statement stmt, int index, object value, bool storeDateTimeAsTicks, string dateTimeStringFormat, bool storeTimeSpanAsTicks)
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
                    SQLite3.BindText(stmt, index, (string)value, -1, _negativePointer);
                }
                else if (value is Byte || value is UInt16 || value is SByte || value is Int16)
                {
                    SQLite3.BindInt(stmt, index, Convert.ToInt32(value));
                }
                else if (value is Boolean)
                {
                    SQLite3.BindInt(stmt, index, (bool)value ? 1 : 0);
                }
                else if (value is UInt32 || value is long)
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
                        SQLite3.BindText(stmt, index, ((TimeSpan)value).ToString(), -1, _negativePointer);
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
                        SQLite3.BindText(stmt, index, ((DateTime)value).ToString(dateTimeStringFormat, System.Globalization.CultureInfo.InvariantCulture), -1, _negativePointer);
                    }
                }
                else if (value is DateTimeOffset)
                {
                    SQLite3.BindInt64(stmt, index, ((DateTimeOffset)value).UtcTicks);
                }
                else if (value is byte[])
                {
                    SQLite3.BindBlob(stmt, index, (byte[])value, ((byte[])value).Length, _negativePointer);
                }
                else if (value is Guid)
                {
                    SQLite3.BindText(stmt, index, ((Guid)value).ToString(), 72, _negativePointer);
                }
                else if (value is Uri)
                {
                    SQLite3.BindText(stmt, index, ((Uri)value).ToString(), -1, _negativePointer);
                }
                else if (value is StringBuilder)
                {
                    SQLite3.BindText(stmt, index, ((StringBuilder)value).ToString(), -1, _negativePointer);
                }
                else if (value is UriBuilder)
                {
                    SQLite3.BindText(stmt, index, ((UriBuilder)value).ToString(), -1, _negativePointer);
                }
                else
                {
                    throw new NotSupportedException("Cannot store type: " + Orm.GetType(value));
                }
            }
        }

        private void MigrateTable(TableMapping map, List<ColumnInfo> existingCols)
        {
            var toBeAdded = new List<TableMapping.Column>();

            foreach (var p in map.Columns)
            {
                var found = false;

                foreach (var c in existingCols)
                {
                    found = (string.Compare(p.Name, c.Name, StringComparison.OrdinalIgnoreCase) == 0);
                    if (found) break;
                }

                if (!found)
                {
                    toBeAdded.Add(p);
                }
            }

            foreach (var p in toBeAdded)
            {
                var addCol = "alter table \"" + map.TableName + "\" add column " + Orm.SqlDecl(p, _db.StoreDateTimeAsTicks, _db.StoreTimeSpanAsTicks);
                _db.Execute(addCol);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _db?.Dispose();
                }
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
