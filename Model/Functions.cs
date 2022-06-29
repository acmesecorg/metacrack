using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FASTER.core;

namespace Metacrack.Model
{
    public class MyInput
    {
        public long value;

        public override string ToString() => value.ToString();
    }

    public class MyOutput
    {
        public Entity value;

        public override string ToString() => value.ToString();
    }

    public class MyContext { }

    public sealed class Functions : FunctionsBase<RowKey, Entity, MyInput, MyOutput, MyContext>
    {
        public override bool InitialUpdater(ref RowKey key, ref MyInput input, ref Entity value, ref MyOutput output, ref RMWInfo rmwInfo)
        {
            //value.value = input.value;
            return true;
        }

        public override bool CopyUpdater(ref RowKey key, ref MyInput input, ref Entity oldValue, ref Entity newValue, ref MyOutput output, ref RMWInfo rmwInfo)
        {
            newValue = oldValue;
            return true;
        }

        public override bool InPlaceUpdater(ref RowKey key, ref MyInput input, ref Entity value, ref MyOutput output, ref RMWInfo rmwInfo) 
        { 
            //value.value += input.value; 
            return true; 
        }

        public override bool SingleReader(ref RowKey key, ref MyInput input, ref Entity value, ref MyOutput dst, ref ReadInfo readInfo)
        {
            dst.value = value;
            return true;
        }

        public override bool ConcurrentReader(ref RowKey key, ref MyInput input, ref Entity value, ref MyOutput dst, ref ReadInfo readInfo)
        {
            dst.value = value;
            return true;
        }

        public override void ReadCompletionCallback(ref RowKey key, ref MyInput input, ref MyOutput output, MyContext ctx, Status status, RecordMetadata recordMetadata)
        {
            if (output.value.RowId == key.key)
                Console.WriteLine("Success!");
            else
                Console.WriteLine("Error!");
        }
    }
}
