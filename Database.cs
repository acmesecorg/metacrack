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

        private Dictionary<char, WriteBatch> _sessions;

        public Database(string outputFolder, bool readOnly = false)
        {
            if (!outputFolder.EndsWith("\\")) outputFolder += "\\";
            _outputFolder = outputFolder;
            _readOnly = readOnly;
        }

        public void Restore()
        {
            _sessions = new Dictionary<char, WriteBatch>();

            var options = new DbOptions().SetCreateIfMissing(true);

            if (_readOnly)
            {
                _db = RocksDb.OpenReadOnly(options, _outputFolder, false);
                return;
            }
            
            _db = RocksDb.Open(options, _outputFolder);
        }

        public WriteBatch GetSession(char hex)
        {
            if (!_sessions.ContainsKey(hex))
            {
                lock (_sessionLock)
                {
                    var newSession = new WriteBatch();
                    _sessions.Add(hex, newSession);

                    return newSession;
                }
            }
            
            return _sessions[hex];
        }

        public void ReadModifyWriteAll(char hex, IEnumerable<Entity> objects)
        {
            var session = GetSession(hex);

            //TODO: look into a merge operator here
            foreach (var entity in objects)
            {
                var key =  entity.RowId;

                //Check for existing, if found, we have to merge the changes into the new 
                var bytes = _db.Get(key);

                if (bytes != null)
                {
                    var existing = Entity.FromBytes(bytes);
                    existing.CopyFrom(entity);
                    bytes = Entity.ToBytes(existing);
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

        //For now, a checkpoint and a flush will be the same thing
        public void Checkpoint()
        {
            Flush();
        }

        public void Flush()
        {
            //Wait for all sessions to complete, then commit and clear memory objects
            lock (_sessionLock)
            {
                //Copy the session references so that code can continue writing into new sessions
                var flushSessions = new Dictionary<char, WriteBatch>(_sessions);
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
            //Dispose any internal objects here
            
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
