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
        public Entity Value;

        public override string ToString() => Value.ToString();
    }

    public class MyOutput
    {
        public Entity Value;

        public override string ToString() => Value.ToString();
    }

    public sealed class Functions : FunctionsBase<long, Entity, MyInput, MyOutput, Empty>
    {
        public override bool InitialUpdater(ref long key, ref MyInput input, ref Entity value, ref MyOutput output, ref RMWInfo rmwInfo)
        {
            if (value == null)
            {
                value = input.Value;
                return true;
            }
            
            //Merge input.value into value
            value.CopyFrom(input.Value);
            return true;
        }

        //This appears to happen when you are updated values that have already been checkpointed
        public override bool CopyUpdater(ref long key, ref MyInput input, ref Entity oldValue, ref Entity newValue, ref MyOutput output, ref RMWInfo rmwInfo)
        {
            if (newValue == null)
            {
                newValue = input.Value;
                newValue.CopyFrom(oldValue);
                return true;
            }

            //Merge input.value and old.value into newValue
            newValue.CopyFrom(oldValue);
            newValue.CopyFrom(input.Value);
            
            return true;
        }

        //This appears to happen when you are updating values recently added
        public override bool InPlaceUpdater(ref long key, ref MyInput input, ref Entity value, ref MyOutput output, ref RMWInfo rmwInfo) 
        {
            value.CopyFrom(input.Value);
            return true; 
        }

        public override bool SingleReader(ref long key, ref MyInput input, ref Entity value, ref MyOutput dst, ref ReadInfo readInfo)
        {
            dst.Value = value;
            return true;
        }

        public override bool ConcurrentReader(ref long key, ref MyInput input, ref Entity value, ref MyOutput dst, ref ReadInfo readInfo)
        {
            dst.Value = value;
            return true;
        }
    }
}
