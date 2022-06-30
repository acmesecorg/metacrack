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

        private FasterKV<RowKey, Entity> _store;
        private readonly object _sessionLock = new object();

        private Dictionary<char, ClientSession<RowKey, Entity, MyInput, MyOutput, MyContext, IFunctions<RowKey, Entity, MyInput, MyOutput, MyContext>>> _sessions;

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

            var serializerSettings = new SerializerSettings<RowKey, Entity>
            {
                keySerializer = () => new RowKeySerializer(),
                valueSerializer = () => new EntitySerializer()
            };

            _store = new FasterKV<RowKey, Entity>(1L << 20, logSettings, checkpointSettings, serializerSettings, null, null, true);
            _sessions = new Dictionary<char, ClientSession<RowKey, Entity, MyInput, MyOutput, MyContext, IFunctions<RowKey, Entity, MyInput, MyOutput, MyContext>>>();
        }

        public ClientSession<RowKey, Entity, MyInput, MyOutput, MyContext, IFunctions<RowKey, Entity, MyInput, MyOutput, MyContext>> GetSession(char hex)
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

        public void UpsertAll(char hex, IEnumerable<Entity> objects)
        {
            var session = GetSession(hex);
            var context = default(MyContext);

            foreach (var obj in objects)
            {
                var entity = obj;
                var key = new RowKey { key = obj.RowId };
                session.Upsert(ref key, ref entity, context, 0);
            }
        }

        public void Upsert(ClientSession<RowKey, Entity, MyInput, MyOutput, MyContext, IFunctions<RowKey, Entity, MyInput, MyOutput, MyContext>> session, MyContext context, Entity entity)
        {
            var key = new RowKey { key = entity.RowId };
            session.Upsert(ref key, ref entity, context, 0);
        }

        public Entity Select(ClientSession<RowKey, Entity, MyInput, MyOutput, MyContext, IFunctions<RowKey, Entity, MyInput, MyOutput, MyContext>> session, long rowId)
        {
            var key = new RowKey { key = rowId };
            var input = default(MyInput);
            var context = default(MyContext);

            var g1 = new MyOutput();

            session.Read(ref key, ref input, ref g1, context, 0);
            return g1.value;
        }

        public void Checkpoint()
        {
            _store.TakeHybridLogCheckpointAsync(CheckpointType.FoldOver).GetAwaiter().GetResult();
        }

        public void Flush()
        {
            _store.TakeFullCheckpointAsync(CheckpointType.FoldOver).AsTask().GetAwaiter().GetResult();            
        }

        public void Compact(ClientSession<RowKey, Entity, MyInput, MyOutput, MyContext, IFunctions<RowKey, Entity, MyInput, MyOutput, MyContext>> session)
        {
            session.Compact(_store.Log.HeadAddress, CompactionType.Scan);
            _store.TakeHybridLogCheckpointAsync(CheckpointType.FoldOver).GetAwaiter().GetResult();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    //Dispose any internal objects here
                    foreach (var session in _sessions.Values) session?.Dispose();
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
