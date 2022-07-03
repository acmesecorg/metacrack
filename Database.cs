using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using FASTER.core;
using Metacrack.Model;

namespace Metacrack
{
    public class Database: IDisposable 
    {
        private bool _disposedValue;
        private string _outputFolder;

        private FasterKV<long, Entity> _store;
        private readonly object _sessionLock = new object();

        private Dictionary<char, ClientSession<long, Entity, MyInput, MyOutput, Empty, IFunctions<long, Entity, MyInput, MyOutput, Empty>>> _sessions;

        public Database(string outputFolder, bool readOnly = false)
        {
            // With non-blittable types, you need an object log device in addition to the
            // main device. FASTER serializes the actual objects in the object log.
            if (!outputFolder.EndsWith("\\")) outputFolder += "\\";
            _outputFolder = outputFolder;
        }

        public void Restore()
        {
            var logSettings = new LogSettings
            {
                LogDevice = Devices.CreateLogDevice(_outputFolder + "hlog.log"),
                ObjectLogDevice = Devices.CreateLogDevice(_outputFolder + "hlog.obj.log")
            };

            var checkpointSettings = new CheckpointSettings();
            checkpointSettings.CheckpointDir = _outputFolder;
            checkpointSettings.RemoveOutdated = true;

            var serializerSettings = new SerializerSettings<long, Entity>
            {
                //keySerializer = () => new RowKeySerializer(),
                valueSerializer = () => new EntitySerializer()
            };

            _store = new FasterKV<long, Entity>(1L << 20, logSettings, checkpointSettings, serializerSettings, null, null, true);
            _sessions = new Dictionary<char, ClientSession<long, Entity, MyInput, MyOutput, Empty, IFunctions<long, Entity, MyInput, MyOutput, Empty>>>();
        }

        public ClientSession<long, Entity, MyInput, MyOutput, Empty, IFunctions<long, Entity, MyInput, MyOutput, Empty>> GetSession(char hex)
        {
            if (!_sessions.ContainsKey(hex))
            {
                lock (_sessionLock)
                {
                    var newSession = _store.NewSession(new Functions());
                    _sessions.Add(hex, newSession);

                    return newSession;
                }
            }
            
            return _sessions[hex];
        }

        public void ReadModifyWriteAll(char hex, IEnumerable<Entity> objects)
        {
            var session = GetSession(hex);

            foreach (var obj in objects)
            {
                var entity = obj;
                var key =  obj.RowId;
                var myInput = new MyInput {Value = entity };
                var myOutput = new MyOutput();

                session.RMW(ref key, ref myInput, ref myOutput);
            }
        }


        public Entity Select(ClientSession<long, Entity, MyInput, MyOutput, Empty, IFunctions<long, Entity, MyInput, MyOutput, Empty>> session, long rowId)
        {
            var key = rowId;
            var input = default(MyInput);
            var context = default(Empty);

            var g1 = new MyOutput();

            if (key == -2641243622861321423) key = -2641243622861321423;

            session.Read(ref key, ref input, ref g1, context, 0);
            if (g1.Value != null) g1.Value.RowId = rowId;
            return g1.Value;
        }

        public void Checkpoint()
        {
            _store.TakeHybridLogCheckpointAsync(CheckpointType.FoldOver).GetAwaiter().GetResult();
        }

        public void Flush()
        {
            //Wait for all sessions to complete, then take a full checkpoint and clear memory objects
            lock (_sessionLock)
            {
                if (_sessions != null)
                {
                    foreach (var session in _sessions.Values)
                    {
                        session.CompletePending(true);
                        session.Dispose();
                    }

                    _sessions.Clear();
                }

                _store.TakeFullCheckpointAsync(CheckpointType.FoldOver).AsTask().GetAwaiter().GetResult();
                _store.Log.FlushAndEvict(true);
            }

            GC.Collect(3, GCCollectionMode.Default);
        }

        public void Compact()
        {
            //Write all sessions
            //Dispose any internal objects here
            lock (_sessionLock)
            {
                if (_sessions != null)
                {
                    foreach (var session in _sessions.Values)
                    {
                        session.Compact(_store.Log.HeadAddress, CompactionType.Scan);
                    }
                }
            }
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
                    _store?.Dispose();
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
