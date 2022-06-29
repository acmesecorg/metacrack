using FASTER.core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Metacrack.Model
{
    public class RowKey: IFasterEqualityComparer<RowKey>
    {
        public long key;

        public long GetHashCode64(ref RowKey key)
        {
            return Utility.GetHashCode(key.key);
        }

        public bool Equals(ref RowKey key1, ref RowKey key2)
        {
            return key1.key == key2.key;
        }

        public override string ToString() => key.ToString();
    }

    public class RowKeySerializer : BinaryObjectSerializer<RowKey>
    {
        public override void Serialize(ref RowKey key)
        {
            writer.Write(key.key);
        }

        public override void Deserialize(out RowKey key)
        {
            key = new RowKey
            {
                key = reader.ReadInt64()
            };
        }
    }
}
