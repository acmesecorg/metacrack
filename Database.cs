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

        private FasterKV<RowKey, Entity> _store;

        private Dictionary<char, ClientSession<RowKey, Entity, MyInput, MyOutput, MyContext, IFunctions<RowKey, Entity, MyInput, MyOutput, MyContext>>> _sessions;

        public Database(string outputFolder, bool readOnly = false)
        {
            // With non-blittable types, you need an object log device in addition to the
            // main device. FASTER serializes the actual objects in the object log.
            if (!outputFolder.EndsWith("\\")) outputFolder += "\\";

            var logSettings = new LogSettings 
            { 
                LogDevice = Devices.CreateLogDevice(outputFolder + "hlog.log"), 
                ObjectLogDevice = Devices.CreateLogDevice(outputFolder + "hlog.obj.log")
            };

            var checkpointSettings = new CheckpointSettings();
            checkpointSettings.CheckpointDir = outputFolder;
            
            var serializerSettings = new SerializerSettings<RowKey, Entity>
            {
                keySerializer = () => new RowKeySerializer(),
                valueSerializer = () => new EntitySerializer()
            };

            _store = new FasterKV<RowKey, Entity>(1L << 20, logSettings, checkpointSettings, serializerSettings, null, null, true); 
            _sessions = new Dictionary<char, ClientSession<RowKey, Entity, MyInput, MyOutput, MyContext, IFunctions<RowKey, Entity, MyInput, MyOutput, MyContext>>>();
        }

        public void InsertAll(char hex, IEnumerable<Entity> objects)
        {
            UpdateAll(hex, objects);
        }

        public void UpdateAll(char hex, IEnumerable<Entity> objects)
        {
            if (!_sessions.ContainsKey(hex)) _sessions.Add(hex, _store.NewSession(new Functions()));

            var session = _sessions[hex];
            var context = default(MyContext);

            foreach (var obj in objects)
            {
                var entity = obj;
                var key = new RowKey { key = obj.RowId };
                session.Upsert(ref key, ref entity, context, 0);
            }
        }

        public Entity Select(char hex, long rowId)
        {
            if (!_sessions.ContainsKey(hex)) _sessions.Add(hex, _store.NewSession(new Functions()));
            var session = _sessions[hex];
            var key = new RowKey { key = rowId };
            var input = default(MyInput);
            var context = default(MyContext);

            var g1 = new MyOutput();

            var status = session.Read(ref key, ref input, ref g1, context, 0);
            return g1.value;
        }

        public void Flush()
        {
            _store.TakeFullCheckpointAsync(CheckpointType.FoldOver).AsTask().GetAwaiter().GetResult();
            _store.Log.FlushAndEvict(true);
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
