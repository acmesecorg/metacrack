using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Metacrack
{
    public static class PluginExtensions
    {
        public static (string Hash, string Plain, int Length) ReadLineAsHashPlain(this StreamReader reader, bool ignoreSalt = false)
        {
            var line = reader.ReadLine();
            var splits = line.Split(':');
            var splitsLength = splits.Length;

            if (splitsLength < 2) return (null, null, 0);

            var fullHash = splits[0].ToLower();

            //Throw away any hash identifier eg MD5 ABCXXX
            if (fullHash.Contains(' ')) fullHash = fullHash.Split(' ')[1];

            //Salts can have the : character.
            //So we have to take the first split and the last split as the hash and password and use everything in between
            if (splitsLength > 2)
            {
                var i = 1;
                var builder = new StringBuilder(fullHash);

                while (i < splitsLength - 1)
                {
                    builder.Append(':');
                    builder.Append(splits[i]);
                    i++;
                }

                if (!ignoreSalt) fullHash = builder.ToString();

                return (fullHash, splits[splitsLength - 1], line.Length);
            }
            else
            {
                return (fullHash, splits[1], line.Length);
            }
        }

        public static (string Email, string FullHash, string Text, string HashPart, string Salt) ReadLineAsEmailHash(this StreamReader reader, bool noSalt = false, bool base64 = false)
        {
            var line = reader.ReadLine();
            var splits = line.Split(':');
            var splitsLength = splits.Length;

            if (splitsLength < 2) return (null, null, line, null, null);

            var email = splits[0];
            var fullHash = splits[1];
            var hashPart = splits[1];
            var salt = default(String);

            //Append the rest of the splits as the salt, adding back the : that was lost
            if (splits.Length > 2 && !noSalt)
            {
                var builder = new StringBuilder(fullHash);
                var saltBuilder = new StringBuilder();

                var i = 2;
                while (i < splitsLength)
                {
                    builder.Append(':');
                    builder.Append(splits[i]);

                    if (i>2) saltBuilder.Append(':');
                    saltBuilder.Append(splits[i]);

                    i++;
                }

                fullHash = builder.ToString();
                salt = saltBuilder.ToString();
            }

            //Check hash for base64 encoding
            if (base64)
            {
                if (fullHash.EndsWith("="))
                {
                    var bytes = Convert.FromBase64String(fullHash);
                    fullHash = BitConverter.ToString(bytes).Replace("-", "").ToLower();
                }
            }

            return (email, fullHash, line, hashPart, salt);
        }

        public static void RemoveLowest(this Dictionary<string, int> value)
        {
            var low = int.MaxValue;
            foreach (var de in value)
            {
                if (de.Value < low) low = de.Value;
            }

            //Now loop through again and remove all keys that have a low value
            var keys = new List<string>();
            foreach (var de in value)
            {
                if (de.Value == low) keys.Add(de.Key);
            }

            //Finally remove
            foreach (var key in keys) value.Remove(key);
        }

        public static async Task<List<string>> ReadLinesAsync(this StreamReader reader, int count)
        {
            var result = new List<string>();
            var i = 1;

            while (!reader.EndOfStream && i < count)
            {
                result.Add(reader.ReadLine());
                i++;
            }

            //Perform the last read asyncronously to yield some control
            if (!reader.EndOfStream) result.Add(await reader.ReadLineAsync());

            return result;
        }

        public static string ReplaceFirst(this string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0) return text;
            
            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }
    }
}
