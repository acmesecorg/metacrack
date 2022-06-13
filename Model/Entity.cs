using System;
using System.Collections.Generic;
using System.Text;
using SQLite;

namespace Metacrack.Model
{
    public class Entity0 : Entity { }
    public class Entity1 : Entity { }
    public class Entity2 : Entity { }
    public class Entity3 : Entity { }
    public class Entity4 : Entity { }
    public class Entity5 : Entity { }
    public class Entity6 : Entity { }
    public class Entity7 : Entity { }
    public class Entity8 : Entity { }
    public class Entity9 : Entity { }
    public class EntityA : Entity { }
    public class EntityB : Entity { }
    public class EntityC : Entity { }
    public class EntityD : Entity { }
    public class EntityE : Entity { }
    public class EntityF : Entity { }

    public class Entity
    {
        private static int ValueLengthMax = 70;

        private static Type[] _types;

        static Entity()
        {
            _types = new Type[] {
                typeof(Entity0),
                typeof(Entity1),
                typeof(Entity2),
                typeof(Entity3),
                typeof(Entity4),
                typeof(Entity5),
                typeof(Entity6),
                typeof(Entity7),
                typeof(Entity8),
                typeof(Entity9),
                typeof(EntityA),
                typeof(EntityB),
                typeof(EntityC),
                typeof(EntityD),
                typeof(EntityE),
                typeof(EntityF),
            };
        }

        //Internal SqlLite RowId is a 64bit signed, so use this for best performance
        [PrimaryKey]
        public long RowId { get; set; }

        public string Passwords { get; set; }
        public string Usernames { get; set; }
        public string Names { get; set; }
        public string Dates { get; set; }
        public string Numbers { get; set; }
        public string Values { get; set; }

        public static Type[] GetTypes()
        {
            return _types;
        }

        public static Entity Create(char hex)
        {
            if (hex == '0') return new Entity0();
            if (hex == '1') return new Entity1();
            if (hex == '2') return new Entity2();
            if (hex == '3') return new Entity3();

            if (hex == '4') return new Entity4();
            if (hex == '5') return new Entity5();
            if (hex == '6') return new Entity6();
            if (hex == '7') return new Entity7();

            if (hex == '8') return new Entity8();
            if (hex == '9') return new Entity9();
            if (hex == 'A') return new EntityA();
            if (hex == 'B') return new EntityB();

            if (hex == 'C') return new EntityC();
            if (hex == 'D') return new EntityD();
            if (hex == 'E') return new EntityE();
            if (hex == 'F') return new EntityF();

            return new Entity0();
        }

        public void AddPasswords(string values)
        {
            if (string.IsNullOrEmpty(values)) return;
            AddPasswords(values.AsSpan());
        }

        public void AddPasswords(ReadOnlySpan<char> values)
        {
            if (values.Length == 0) return;

            if (string.IsNullOrEmpty(Passwords))
            {
                Passwords = values.ToString();
                return;
            }

            Passwords = Passwords.AsSpan().MergeWith(values, ':');
        }

        public void AddUsernames(string values)
        {
            if (string.IsNullOrEmpty(values)) return;
            AddUsernames(values.AsSpan());
        }

        public void AddUsernames(ReadOnlySpan<char> values)
        {
            if (values.Length == 0) return;

            if (string.IsNullOrEmpty(Usernames))
            {
                Usernames = values.ToString();
                return;
            }

            Usernames = Usernames.AsSpan().MergeWith(values, ':');
        }

        public void AddNames(string values)
        {
            if (string.IsNullOrEmpty(values)) return;
            AddNames(values.AsSpan());
        }

        public void AddNames(ReadOnlySpan<char> values)
        {
            if (values.Length == 0) return;

            if (string.IsNullOrEmpty(Names))
            {
                Names = values.ToString();
                return;
            }

            Names = Names.AsSpan().MergeWith(values, ':');
        }

        public void AddDates(string values)
        {
            if (string.IsNullOrEmpty(values)) return;
            AddDates(values.AsSpan());
        }

        public void AddDates(ReadOnlySpan<char> values)
        {
            if (values.Length == 0) return;

            if (string.IsNullOrEmpty(Dates))
            {
                Dates = values.ToString();
                return;
            }

            Dates = Dates.AsSpan().MergeWith(values, ':');
        }

        public void AddNumbers(string values)
        {
            if (string.IsNullOrEmpty(values)) return;
            AddNumbers(values.AsSpan());
        }

        public void AddNumbers(ReadOnlySpan<char> values)
        {
            if (values.Length == 0) return;

            if (string.IsNullOrEmpty(Numbers))
            {
                Numbers = values.ToString();
                return;
            }

            Numbers = Numbers.AsSpan().MergeWith(values, ':');
        }

        public void AddValues(string values)
        {
            if (string.IsNullOrEmpty(values)) return;
            AddValues(values.AsSpan());
        }

        public void AddValues(ReadOnlySpan<char> values)
        {
            if (values.Length == 0) return;

            Values = Values.AsSpan().MergeWith(values, ':');
        }

        public void SetValue(string value, string field)
        {
            if (string.IsNullOrEmpty(value)) return;

            SetValue(value.AsSpan(), field);
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

        public void SetValue(ReadOnlySpan<char> value, string field)
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
}


