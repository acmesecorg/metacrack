using FASTER.core;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Metacrack.Model
{
    [ProtoContract]
    public class Entity
    {
        private static int ValueLengthMax = 70;

        //We dont serialize the rowId, this is stored in the index log instead
        public long RowId { get; set; }

        [ProtoMember(1)]
        public string Passwords { get; set; }

        [ProtoMember(2)]
        public string Usernames { get; set; }

        [ProtoMember(3)]
        public string Names { get; set; }

        [ProtoMember(4)]
        public string Dates { get; set; }

        [ProtoMember(5)]
        public string Numbers { get; set; }

        [ProtoMember(6)]
        public string Values { get; set; }

        public void CopyFrom(Entity entity)
        {
            if (entity == null) return;

            AddPasswords(entity.Passwords);
            AddUsernames(entity.Usernames);
            AddNames(entity.Names);
            AddDates(entity.Dates);
            AddNumbers(entity.Numbers);
            AddValues(entity.Values);
        }

        public void AddPasswords(string values)
        {
            if (string.IsNullOrEmpty(values)) return;
            if (string.IsNullOrEmpty(Passwords))
            {
                Passwords = values;
                return;
            }

            Passwords = Passwords.MergeWith(values, ':');
        }


        public void AddUsernames(string values)
        {
            if (string.IsNullOrEmpty(values)) return;
            if (string.IsNullOrEmpty(Usernames))
            {
                Usernames = values;
                return;
            }

            Usernames = Usernames.MergeWith(values, ':');
        }

        public void AddNames(string values)
        {
            if (string.IsNullOrEmpty(values)) return;
            if (string.IsNullOrEmpty(Names))
            {
                Names = values;
                return;
            }

            Names = Names.MergeWith(values, ':');
        }

        public void AddDates(string values)
        {
            if (string.IsNullOrEmpty(values)) return;
            if (string.IsNullOrEmpty(Dates))
            {
                Dates = values;
                return;
            }

            Dates = Dates.MergeWith(values, ':');
        }

        public void AddNumbers(string values)
        {
            if (string.IsNullOrEmpty(values)) return;
            if (string.IsNullOrEmpty(Numbers))
            {
                Numbers = values;
                return;
            }

            Numbers = Numbers.MergeWith(values, ':');
        }

        public void AddValues(string values)
        {
            if (string.IsNullOrEmpty(values)) return;
            if (string.IsNullOrEmpty(Values))
            {
                Values = values;
                return;
            }

            Values = Values.MergeWith(values, ':');
        }

        public List<string> GetValues(IEnumerable<string> fields)
        {
            var result = new List<string>();
            
            //First collect fields that could be modified by a date or number
            foreach (var field in fields)
            {
                if (field == "p" || field == "password")
                {
                    if (!string.IsNullOrEmpty(Passwords))
                    {
                        foreach (var (value, index) in Passwords.SplitByChar(':'))
                        {
                            var valueString = value.ToString();
                            if (!result.Contains(valueString)) result.Add(valueString);
                        }
                    }
                }
                else if (field == "u" || field == "username")
                {
                    if (!string.IsNullOrEmpty(Usernames))
                    {
                        foreach (var (value, index) in Usernames.SplitByChar(':'))
                        {
                            var valueString = value.ToString();
                            if (!result.Contains(valueString)) result.Add(valueString);
                        }
                    }
                }
                else if (field == "n" || field == "name")
                {
                    if (!string.IsNullOrEmpty(Names))
                    {
                        foreach (var (value, index) in Names.SplitByChar(':'))
                        {
                            var valueString = value.ToString();
                            if (!result.Contains(valueString)) result.Add(valueString);
                        }
                    }
                }
                else if (field == "v" || field == "value")
                {
                    if (!string.IsNullOrEmpty(Values))
                    {
                        foreach (var (value, index) in Values.SplitByChar(':'))
                        {
                            var valueString = value.ToString();
                            if (!result.Contains(valueString)) result.Add(valueString);
                        }
                    }
                }
            }

            //Derive passwords from the number and date fields
            var derived = new List<string>();

            //Modify existing words if dates and numbers selected
            foreach (var field in fields)
            {
                if (field == "i" || field == "number")
                {
                    foreach (var (value, index) in Numbers.SplitByChar(':'))
                    {
                        foreach (var existing in result)
                        {
                            var combo = $"{existing}{value.ToString()}";
                            if (!result.Contains(combo) && !derived.Contains(combo)) derived.Add(combo);
                        }
                    }
                }
                else if (field == "d" || field == "date")
                {
                    foreach (var (value, index) in Dates.SplitByChar(':'))
                    {
                        foreach (var existing in result)
                        {
                            //Take last two chars if year
                            if (Dates.Length == 4)
                            {
                                var yeartd = $"{existing}{value.Slice(2).ToString()}";
                                if (!result.Contains(yeartd) && !derived.Contains(yeartd)) derived.Add(yeartd);
                            }

                            //Now add all 4 characters or any other version
                            var combo = $"{existing}{value.ToString()}";
                            if (!result.Contains(combo) && !derived.Contains(combo)) derived.Add(combo);
                        }
                    }
                }
            }

            result.AddRange(derived);
            return result;
        }

        public void SetValue(string value, string field)
        {
            if (value.Length > ValueLengthMax) return;

            if (value.Length == 0) return;

            if (field == "p" || field == "password")
            {
                AddPasswords(value);
            }
            else if(field == "u" || field == "username")
            {
                AddUsernames(value);
            }
            else if (field == "n" || field == "name")
            {
                AddNames(value);
            }
            else if (field == "d" || field == "date")
            {
                AddDates(value);
            }
            else if (field == "i" || field == "number")
            {
                AddNumbers(value);
            }
            else
            {
                AddValues(value);
            }
        }
    }

    public class EntitySerializer: BinaryObjectSerializer<Entity>
   {
         public override void Serialize(ref Entity value)
        {
            using (var memoryStream = new MemoryStream())
            {
                Serializer.Serialize(memoryStream, value);
                var byteArray = memoryStream.ToArray();
                var length = (Int32) byteArray.Length;

                //Write the length, then the bytes
                writer.Write(length);
                writer.Write(byteArray, 0, byteArray.Length);
            }
        }

        public override void Deserialize(out Entity value)
        {
            var length = reader.ReadInt32();
            var bytes = reader.ReadBytes(length);
            var buffer = new ReadOnlySpan<byte>(bytes);

            value = Serializer.Deserialize<Entity>(buffer);
        }       
    }
}


