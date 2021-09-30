using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

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
            return ValidateHash(hash, mode, 0);
        }

        public static bool ValidateHash(string hash, int mode, int iteration)
        {
            if (mode == -1) return true;

            //Validate length
            if (mode == 0 && hash.Length != 32) return false;
            if (mode == 100 && hash.Length != 40) return false;
            if (mode == 400 && hash.Length != 34) return false;
            if (mode == 3200 && hash.Length != 60) return false;
            if (mode == 10000 && hash.Length != 77) return false;

            //Validate hex
            if (mode == 0 || mode == 100)
            {
                if (!IsHex(hash)) return false;
            }

            //Validate prefix
            if (mode == 400)
            {
                if (!hash.StartsWith("$P$B")) return false;
            }

            //Validate iterations
            if (iteration > 0)
            {
                if (mode == 3200)
                {
                    var splits = hash.Split('$', StringSplitOptions.RemoveEmptyEntries);
                    if (splits[1] != iteration.ToString()) return false;
                }
            }

            return true;
        }

        public static bool IsHex(IEnumerable<char> chars)
        {
            foreach (var c in chars)
            {
                var isHex = ((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));

                if (!isHex) return false;
            }
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
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    var fileInfo = new FileInfo(path);

                    WriteHighlight($"Existing file {fileInfo.Name} would be overwritten.");

                    return false;
                }
            }

            return true;
        }

        public static long GetFileEntriesSize(string[] fileEntries)
        {
            var size = 0L;

            foreach (var lookupPath in fileEntries)
            {
                var fileInfo = new FileInfo(lookupPath);
                size += fileInfo.Length;
            }

            return size;
        }

        public static string FormatSize(long bytes)
        {
            var unit = 1024;
            if (bytes < unit) { return $"{bytes} B"; }

            var exp = (int)(Math.Log(bytes) / Math.Log(unit));
            return $"{bytes / Math.Pow(unit, exp):F2} {("KMGTPE")[exp - 1]}B";
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
