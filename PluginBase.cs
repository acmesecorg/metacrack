using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Metacrack
{
    public struct HashInfo
    {
        public int Mode;
        public int Columns;
        public int Length;
        public bool IsHex;
        public string Prefix;

        public HashInfo(int mode, int columns, int length, bool isHex, string prefix = null)
        {
            Mode = mode;
            Columns = columns;
            Length = length;    
            IsHex = isHex;  
            Prefix = prefix;    
        }
    }

    public abstract class PluginBase
    {
        public static string[] Hex = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "a", "b", "c", "d", "e", "f" };

        //Cache this for better performance. Since it is static, we dont worry so much about disposal
        private static SHA1 _sha1;
        private static long _sharedProgressTotal;
        private static long _sharedProgress;

        private static MD5 _md5;

        public static List<List<string>> GetRules(string option)
        {
            string path;

            try
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), option);
            }
            catch (Exception ex)
            {
                WriteHighlight($"Exception getting rule path. {ex.Message}");
                return null;
            }

            try
            {
                var lines = File.ReadAllLines(path);
                return RulesEngine.ProcessRules(lines);
            }
            catch (Exception ex)
            {
                WriteHighlight($"Exception loading rules. {ex.Message}");
                return null;
            }
        }

        public static string IncrementFilename(string filenameNoExtension, string type)
        {
            var dottype = $".{type}";

            //Check if the type is already in the filename
            if (!filenameNoExtension.Contains(dottype)) return $"{filenameNoExtension}{dottype}";

            //Find the type and increment it by one
            var splits = filenameNoExtension.Split('.');
            var result = new List<string>();

            foreach (var split in splits)
            {
                //First instance
                if (split == type)
                {
                    result.Add($"{type}2");
                    continue;
                }

                if (split.StartsWith(type))
                {
                    var valueString = split.Substring(type.Length);
                    if (int.TryParse(valueString, out var value))
                    {
                        value++;
                        result.Add($"{type}{value}");
                        continue;
                    }
                }

                result.Add(split);
            }

            return string.Join(".", result);
        }

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

        public static bool ValidateHash(string hash, HashInfo info)
        {
            return ValidateHash(hash, info, 0);
        }

        public static bool ValidateHash(string hash, HashInfo info, int iteration)
        {
            //Unknown hash
            if (info.Length == 0) return true;

            //Validate length
            if (info.Length != hash.Length) return false;

            //Validate hex
            if (info.IsHex)
            {
                if (!IsHex(hash)) return false;
            }

            //Validate prefix
            if (info.Prefix != null && info.Prefix.Length > 0)
            {
                if (!hash.StartsWith(info.Prefix)) return false;
            }

            //Validate iterations
            if (iteration > 0)
            {
                if (info.Mode == 10000)
                {
                    var splits = hash.Split('$', StringSplitOptions.RemoveEmptyEntries);
                    if (splits[1] != iteration.ToString()) return false;
                }
                else if (info.Mode == 3200 || info.Mode == 25600)
                {
                    var splits = hash.Split('$', StringSplitOptions.RemoveEmptyEntries);
                    var iterationString = (iteration < 10) ? $"0{iteration}" : iteration.ToString();
                    if (splits[1] != iterationString) return false;
                }
            }

            return true;
        }

        public static bool ValidateSalt(string salt, HashInfo info)
        {
            if (info.Mode == 27200) return salt.Length == 40;
            return true;
        }

        public static HashInfo GetHashInfo(int mode)
        {
            //8743b52063cd84097a65d1633f5c74f5
            if (mode == 0) return new HashInfo(mode, 1, 32, true);
            if (mode == 11) return new HashInfo(mode, 2, 32, true);
            if (mode == 20) return new HashInfo(mode, 2, 32, true);

            //b89eaac7e61417341b710b727768294d0e6a277b
            if (mode == 100) return new HashInfo(mode, 2, 40, true);

            //$P$984478476IagS59wHZvyQMArzfx58u.
            if (mode == 400) return new HashInfo(mode, 1, 34, false);

            if (mode == 1400) return new HashInfo(mode, 1, 64, true);
            if (mode == 1410) return new HashInfo(mode, 2, 64, true);

            if (mode == 1800) return new HashInfo(mode, 1, 106, false);

            //b2771af9d6e8395c72254bbc379dd092:NqPyawIn
            if (mode == 2811) return new HashInfo(mode, 2, 32, true);

            //$2a$05$LhayLxezLhK1LhWvKxCyLOj0j1u.Kj0jZ0pEmm134uzrQlFvQJLF6
            if (mode == 3200) return new HashInfo(mode, 1, 60, false);

            if (mode == 2611) return new HashInfo(mode, 2, 32, true);

            //pbkdf2_sha256$20000$H0dPx8NeajVu$GiC4k5kqbbR9qWBlsRgDywNqC2vd9kqfk7zdorEnNas=
            if (mode == 10000) return new HashInfo(mode, 1, 77, false);

            if (mode == 25600) return new HashInfo(mode, 1, 60, false, "$2");

            if (mode == 27200) return new HashInfo(mode, 2, 40, true);

            //Return a default setting with zero length
            return new HashInfo(mode, 1, 0, false);
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

        public static byte[] FromHex(string hex)
        {
            if (hex.Length % 2 == 1)
                throw new Exception("The binary key cannot have an odd number of digits");

            byte[] arr = new byte[hex.Length >> 1];

            for (int i = 0; i < hex.Length >> 1; ++i)
            {
                arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            }

            return arr;
        }

        public static int GetHexVal(char hex)
        {
            int val = (int)hex;
            //For uppercase A-F letters:
            //return val - (val < 58 ? 48 : 55);
            //For lowercase a-f letters:
            //return val - (val < 58 ? 48 : 87);
            //Or the two combined, but a bit slower:
            return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
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

        public static void OptimizeFolder(string folder, string prefix)
        {
            WriteMessage($"Optimising buckets.");

            var progressTotal = 0;
            var files = Directory.GetFiles(folder, $"{prefix}-*");
            var count = files.Length;

            //TODO: use multiple tasks here to improve performance
            foreach (var sourceFile in files)
            {
                var fileInfo = new FileInfo(sourceFile);
                var fileName = Path.GetFileNameWithoutExtension(sourceFile);

                WriteProgress($"Optimizing {fileName}", progressTotal, count);

                var bucket = new List<string>();
                using (var reader = new StreamReader(sourceFile))
                {
                    while (!reader.EndOfStream)
                    {
                        bucket.Add(reader.ReadLine());
                    }
                }

                //Optimize this bucket by deduplicating and then sorting
                bucket = bucket.Distinct().OrderBy(q => q).ToList();

                File.Delete(sourceFile);
                File.AppendAllLines(sourceFile, bucket);

                progressTotal++;
            }
        }

        public static void StemEmail(string email, HashSet<string> lookups, HashSet<string> finals)
        {
            var subsplits = email.Split('@');
            var name = subsplits[0];

            //Add the whole name
            finals.Add(name);

            //Remove special characters, giving us alpha and numerics
            //Regex expression is cached
            var matches = Regex.Matches(name, "[a-z]+|[0-9]+", RegexOptions.IgnoreCase);

            //Split any text by list of lookup names
            //Using a hashset means we dont get repeats
            foreach (var match in matches)
            {
                var value = ((Match)match).Value;

                //We will let rules take care of single digit numbers
                //We are really more interested in special numbers and dates of birth etc here
                if (int.TryParse(value, out var number))
                {
                    if (number > 9) finals.Add(value);
                }
                else
                {
                    finals.Add(value);

                    //Split names now, because the email will be anonimised after this
                    //Try split single name eg bobjenkins into bob and jenkins
                    foreach (var entry in lookups)
                    {
                        //For comparison only, we use lower case. 
                        //Entries have already been lowered
                        if (value.ToLower().StartsWith(entry))
                        {
                            finals.Add(entry);

                            var other = value.Replace(entry, "");

                            if (other.Length > 1)
                            {
                                finals.Add(other);
                            }
                        }
                    }
                }
            }
        }

        public static string HashMd5(string input)
        {
            if (_md5 == null) _md5 = MD5.Create();

            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = _md5.ComputeHash(inputBytes);

            // Convert the byte array to hexadecimal string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("X2"));
            }
            return sb.ToString().ToLower();
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
