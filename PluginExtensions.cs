using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Metacrack
{
    public static class PluginExtensions
    {
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
