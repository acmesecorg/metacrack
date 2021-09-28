using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Malfoy
{
    public abstract class PluginBase
    {
        public static string[] Hex = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "a", "b", "c", "d", "e", "f" };

        //Cache this for better performance. Since it is static, we dont worry so much about disposal
        private static SHA1 _sha1;
        private static long _sharedProgressTotal;
        private static long _sharedProgress;

        public static bool ValidateEmail(string email, out string emailStem)
        {
            emailStem = null;

            if (email.Contains(':')) return false;

            var emailSplits = email.Split('@');
            if (emailSplits.Length != 2) return false;

            var domainSplits = emailSplits[1].Split('.');
            if (domainSplits.Length < 2) return false;

            //Now stem the email
            var nameSplits = emailSplits[0].Split('+', 2);

            emailStem = $"{nameSplits[0]}@{emailSplits[1]}";

            return true;
        }

        public static bool ValidateHash(string hash, int mode)
        {
            if (mode == 3200 && hash.Length != 60) return false;
            if (mode == 10000 && hash.Length != 77) return false;

            return true;
        }

        public static HashSet<String> GetTokens(string value)
        {
            var result = new HashSet<string>();

            //Add the value
            result.Add(value);

            //Add the lowercase of the value
            result.Add(value.ToLower());

            //Split on space, - etc
            var splits = value.Split(new char[] { ' ', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var split in splits)
            {
                result.Add(split);
                result.Add(split.ToLower());

                //Remove any special characters and numbers at the end
                var match = Regex.Match(split, "^([a-z]*)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var matchValue = match.Groups[1].Value;
                    if (matchValue.Length > 2)
                    {
                        result.Add(match.Groups[1].Value);
                        result.Add(match.Groups[1].Value.ToLower());
                    }
                }
            }

            return result;
        }

        public static string GetIdentifier(string email)
        {
            if (_sha1 == null) _sha1 = SHA1.Create();
            return GetIdentifier(_sha1.ComputeHash(Encoding.UTF8.GetBytes(email)));
        }

        public static string GetIdentifier(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length < 10) throw new ArgumentOutOfRangeException(nameof(bytes));

            var builder = new StringBuilder(20);
            for (var i=0; i<10; i++) builder.Append(bytes[i].ToString("x2"));

            return builder.ToString();
        }

        public static string GetSerial(FileInfo fileInfo, string prefix = "")
        {
            var numberBytes = BitConverter.GetBytes(fileInfo.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(numberBytes);

            //Remove any leading zeros (this is a bit clunky)
            var foos = new List<byte>(numberBytes);
            while (foos[0] == 0x00) foos.RemoveAt(0);

            numberBytes = foos.ToArray();

            var version = Convert.ToBase64String(numberBytes).Replace("=", "").Replace("/", "").Replace("+", "");
            return (version.Length > 6) ? $"{prefix}{version.ToLower().Substring(version.Length - 3, 3)}" : $"{prefix}{version.ToLower()}";
        }

        public static IEnumerable<List<T>> SplitList<T>(List<T> locations, int nSize = 256)
        {
            for (int i = 0; i < locations.Count; i += nSize)
            {
                yield return locations.GetRange(i, Math.Min(nSize, locations.Count - i));
            }
        }

        public static bool CheckForFiles(string[] paths)
        {
            return Common.CheckForFiles(paths);
        }

        public static long GetSizeOfEntries(string[] fileEntries)
        {
            return Common.GetFileEntriesSize(fileEntries);
        }

        public static string FormatSize(long bytes)
        {
            return Common.FormatFileSize(bytes);
        }

        public static void WriteError(string value)
        {
            ConsoleUtil.WriteMessage(value, ConsoleColor.Red);
        }

        public static void WriteHighlight(string value)
        {
            ConsoleUtil.WriteMessage(value, ConsoleColor.DarkYellow);
        }

        public static void WriteMessage(string value)
        {
            ConsoleUtil.WriteMessage(value);
        }

        public static void StartProgress(long total)
        {
            _sharedProgressTotal = total;
        }

        public static void AddToProgress(string text, long progress)
        {
            if (_sharedProgressTotal == 0) throw new ApplicationException("StartProgress total has not been set.");
            _sharedProgress += progress;

            ConsoleUtil.WriteProgress(text, _sharedProgress, _sharedProgressTotal);
        }

        public static void WriteProgress(string text, long progress, long total)
        {
            ConsoleUtil.WriteProgress(text, progress, total);
        }

        public static void WriteProgress(string text, int percent)
        {
            ConsoleUtil.WriteProgress(text, percent);
        }

        public static void CancelProgress()
        {
            ConsoleUtil.CancelProgress();
        }



    }
}
