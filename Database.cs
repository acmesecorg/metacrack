using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

using RocksDbSharp;
using Metacrack.Model;

namespace Metacrack
{
    public class Database: IDisposable 
    {
        private bool _disposedValue;
        private string _outputFolder;
        private bool _readOnly = false;

        private RocksDb _db;
        private readonly object _sessionLock = new object();

        private Dictionary<char, WriteBatchWithIndex> _sessions;

        public Database(string outputFolder, bool readOnly = false)
        {
            if (!outputFolder.EndsWith("\\")) outputFolder += "\\";
            _outputFolder = outputFolder;
            _readOnly = readOnly;
        }

        public void Restore()
        {
            _sessions = new Dictionary<char, WriteBatchWithIndex>();

            var options = new DbOptions().SetCreateIfMissing(true);

            //Check if exists
            var exists = Directory.Exists(_outputFolder) && Directory.EnumerateFiles(_outputFolder).Count() > 0;

            if (!exists)
            {
                //https://github.com/facebook/rocksdb/wiki/Setup-Options-and-Basic-Tuning
                options.SetLevelCompactionDynamicLevelBytes(true);
                options.SetMaxBackgroundCompactions(4);
                options.SetMaxBackgroundFlushes(2);
                options.SetBytesPerSync(1048576);
                //options.compaction_pri = MinOverlappingRatio;

                //table_options.block_size = 16 * 1024;
                //table_options.cache_index_and_filter_blocks = true;
                //table_options.pin_l0_filter_and_index_blocks_in_cache = true;
                //table_options.format_version = < the latest version>;
            }

            if (_readOnly)
            {
                _db = RocksDb.OpenReadOnly(options, _outputFolder, false);
                return;
            }
            
            _db = RocksDb.Open(options, _outputFolder);
        }

        public WriteBatchWithIndex GetSession(char hex)
        {
            if (!_sessions.ContainsKey(hex))
            {
                lock (_sessionLock)
                {
                    var newSession = new WriteBatchWithIndex();
                    _sessions.Add(hex, newSession);

                    return newSession;
                }
            }
            
            return _sessions[hex];
        }

        public void ReadModifyWriteAll(char hex, IEnumerable<Entity> objects)
        {
            var session = GetSession(hex);

            foreach (var entity in objects)
            {
                var key =  entity.RowId;

                //TODO: look into a merge operator here
                //Check for existing, if found, we have to merge the changes into the new entity
                //We want to read from the batch and the db (read through)
                //https://github.com/curiosity-ai/rocksdb-sharp/blob/master/csharp/src/WriteBatchWithIndex.cs
                var bytes = session.Get(_db, key);

                if (bytes != null)
                {
                    var existing = Entity.FromBytes(bytes);
                    existing.CopyFrom(entity);
                    bytes = Entity.ToBytes(existing);
                }
                else
                {
                    bytes = Entity.ToBytes(entity);
                }

                //Add to the batch 
                session.Put(key, bytes);
            }
        }


        public Entity Select(byte[] rowId)
        {
            var bytes = _db.Get(rowId);

            if (bytes == null) return null;

            return Entity.FromBytes(bytes);
        }

        public void Flush()
        {
            //Wait for all sessions to complete, then commit and clear memory objects
            lock (_sessionLock)
            {
                //Copy the session references so that code can continue writing into new sessions
                var flushSessions = new Dictionary<char, WriteBatchWithIndex>(_sessions);
                _sessions.Clear();

                if (flushSessions != null)
                {
                    foreach (var flushSession in flushSessions.Values)
                    {
                        _db.Write(flushSession);
                        flushSession.Dispose();
                    }
                }
            }

            GC.Collect(3, GCCollectionMode.Default);
        }

        public void Compact()
        {
            //Write all sessions
            //If either begin or end are NULL, it is taken to mean the key before all keys in the db or the key after all keys respectively.
            //CompactRangeOptions::exclusive_manual_compaction
            _db.CompactRange(null, null, null);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    //Dispose any internal objects here
                    if (_sessions != null)
                    {
                        foreach (var session in _sessions.Values) session?.Dispose();
                    }
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
