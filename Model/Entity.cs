using System;
using SQLite;

namespace Metacrack.Model
{
    public class Entity
    {
        //Internal SqlLite RowId is a 64bit signed, so use this for best performance
        [PrimaryKey]
        public long RowId { get; set; }

        public string Passwords { get; set; }
        public string Usernames { get; set; }
        public string Names { get; set; }
        public string Dates { get; set; }
        public string Numbers { get; set; }
        public string Values { get; set; }

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

        public void SetValue(string value, string column)
        {
            if (string.IsNullOrEmpty(value)) return;

            SetValue(value.AsSpan(), column);
        }

        public void SetValue(ReadOnlySpan<char> value, string column)
        {
            if (value.Length == 0) return;

            if (column == "p" || column == "password")
            {
                AddPasswords(value);
            }
            else if(column == "u" || column == "username")
            {
                AddUsernames(value);
            }
            else if (column == "n" || column == "name")
            {
                AddNames(value);
            }
            else if (column == "d" || column == "date")
            {
                AddDates(value);
            }
            else if (column == "i" || column == "number")
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


