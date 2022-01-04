using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Metacrack.Model;

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
        public static readonly string[] ValidFields = { "p", "password", "u", "username", "n", "name", "d", "date", "i", "number", "v", "value" };
        public static readonly string[] Hex = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "a", "b", "c", "d", "e", "f" };
        public const int MaxSessionsDefault = 20;

        //Cache this for better performance. Since it is static, we dont worry so much about disposal
        private static SHA1 _sha1;
        private static long _sharedProgressTotal;
        private static long _sharedProgress;

        public static List<List<string>> GetRules(string path)
        {
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

        public static bool TryParse(string value, out int number)
        {
            number = 0;
            value = value.Replace("kk", "000000");
            value = value.Replace("k", "000");

            if (!int.TryParse(value, out var count)) return false;

            return true;
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

        public static bool ValidateEmail(ReadOnlySpan<char> email, out string emailStem)
        {
            //Assign this value so that we can return false
            emailStem = null;
            if (email.Contains(':')) return false;

            var emailStemBuilder = new StringBuilder();
            var emailSplits = email.SplitByChar('@');
            var valid = false;

            foreach (var (emailSplit, index) in emailSplits)
            {
                //Remove any +
                if (index == 0)
                {
                    var nameSplits = emailSplit.SplitByChar('+');

                    //Check for first entry
                    if (!nameSplits.MoveNext()) return false;

                    emailStemBuilder.Append(nameSplits.Current);
                }

                //Validate domain splits
                else if (index == 1)
                {
                    var domainSplits = emailSplit.SplitByChar('.');
                    if (domainSplits.MoveToEnd() != 2) return false;

                    //Append domain
                    emailStemBuilder.Append("@");
                    emailStemBuilder.Append(emailSplit);

                    //Flag as valid value
                    valid = true;
                }
                else
                {
                    return false;
                }
            }

            //Make sure valid flag was set in index 1
            if (!valid) return false;

            emailStem = emailStemBuilder.ToString();

            return true;
        }

        public static bool ValidateHash(ReadOnlySpan<char> hash, HashInfo info)
        {
            return ValidateHash(hash, info, "0");
        }

        public static bool ValidateHash(ReadOnlySpan<char> hash, HashInfo info, string iteration)
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
            if (iteration != "0")
            {
                if (info.Mode == 10000)
                {
                    var splits = hash.SplitByChar('$');
                    foreach (var (split, index) in splits)
                    {
                        if (index == 1 && split != iteration) return false;
                    }
                }
                else if (info.Mode == 3200 || info.Mode == 25600)
                {
                    var splits = hash.SplitByChar('$');

                    foreach (var (split, index) in splits)
                    {
                        if (index == 1)
                        {
                            //Compare, skipping any initial zeros eg compare 01 to 1
                            for (var i=0; i<split.Length; i++)
                            {
                                if (split.Length == iteration.Length + i)
                                {
                                    if (split.Slice(i) != iteration) return false;
                                }
                            }
                        }
                    }
                }
            }

            return true;
        }

        public static bool ValidateSalt(ReadOnlySpan<char> salt, HashInfo info)
        {
            if (info.Mode == 27200) return salt.Length == 40;
            return true;
        }

        public static HashInfo GetHashInfo(int mode)
        {
            //8743b52063cd84097a65d1633f5c74f5
            if (mode == 0) return new HashInfo(mode, 1, 32, true);

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

        public static bool IsHex(ReadOnlySpan<char> chars)
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

        public static bool CheckOverwrite(string[] paths)
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

        public static void StemEmail(string email, HashSet<string> lookups, Entity entity)
        {
            var subsplits = email.SplitByChar('@');
            if (!subsplits.MoveNext()) return;

            var name = subsplits.Current.Value;
            
            //Add the whole name as a value
            entity.AddValues(name);

            //Remove special characters, giving us alpha and numerics
            //Regex expression is cached
            var value = name.RemoveSpecialCharacters();

            //We will let rules take care of single digit numbers
            //We are really more interested in special numbers and dates of birth etc here
            if (int.TryParse(value, out var number))
            {
                if (number > 9) entity.AddNumbers(value);
            }
            else
            {
                var finals = new StringBuilder();
                    
                //Split names now, because the email will be anonimised after this
                //Try split single name eg bobjenkins into bob and jenkins
                foreach (var entry in lookups)
                {
                    //For comparison only, we ignore case
                    if (value.StartsWith(entry, StringComparison.OrdinalIgnoreCase))
                    {
                        if (finals.Length > 0) finals.Append(':');
                        finals.Append(entry);

                        var other = value.Slice(entry.Length);

                        if (other.Length > 1)
                        {
                            if (int.TryParse(other, out var number2))
                            {
                                if (number2 > 9) entity.AddNumbers(other);
                            }
                            else
                            {
                                finals.Append(':');
                                finals.Append(other);
                            }
                        }
                    }
                }

                entity.AddNames(finals.ToString());
            }
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
