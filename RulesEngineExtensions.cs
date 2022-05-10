using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Metacrack
{
    public static class RulesEngineExtensions
    {
        public static char ToggleCase(this char value)
        {
            if (!char.IsLetter(value)) return value;
            return char.IsUpper(value) ? char.ToLowerInvariant(value) : char.ToUpperInvariant(value);
        }

        public static string ToStringFromEnumerable(this IEnumerable<char> charSequence)
        {
            return new string(charSequence.ToArray());
        }

        public static string Reverse(this string s)
        {
            char[] charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }

        public static string Repeat(this string s, int n)
        {
            var builder = new StringBuilder();

            for (var i = 0; i <= n; i++) builder.Append(s);

            return builder.ToString();
        }

        public static string Repeat(this char c, int n)
        {
            var builder = new StringBuilder();

            for (var i = 0; i <= n; i++) builder.Append(c);

            return builder.ToString();
        }

        public static char ToUpperInvariant(this char c)
        {
            return char.ToUpperInvariant(c);
        }

        public static char ToLowerInvariant(this char c)
        {
            return char.ToLowerInvariant(c);
        }

        public static int HexToInt(this char c)
        {
            return int.Parse(c.ToString(), NumberStyles.HexNumber);
        }

        public static char AsciiIncrement(this char c)
        {
            var x = (byte)c;
            x++;
            return (char)x;
        }

        public static char AsciiDecrement(this char c)
        {
            var x = (byte)c;
            x--;
            return (char)x;
        }

        //https://stackoverflow.com/questions/737781/left-bit-shifting-255-as-a-byte
        public static char ShiftLeft(this char c)
        {
            return (char)((c << 1) & 0xFF);
        }

        public static char ShiftRight(this char c)
        {
            return (char)((c >> 1) & 0xFF);
        }

        public static string DuplicateAll(this string s)
        {
            var builder = new StringBuilder();

            for (var i = 0; i < s.Length; i++) builder.Append(s[i], 2);

            return builder.ToString();
        }
    }
}
