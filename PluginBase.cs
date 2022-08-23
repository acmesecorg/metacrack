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
        public int MaxLength;
        public bool IsHex;
        public string Prefix;

        public HashInfo(int mode, int columns, int length, bool isHex, string prefix = null)
        {
            Mode = mode;
            Columns = columns;
            Length = length;    
            IsHex = isHex;  
            Prefix = prefix;

            MaxLength = length;
        }

        public HashInfo(int mode, int columns, int length, int maxlength, bool isHex, string prefix = null)
        {
            Mode = mode;
            Columns = columns;
            Length = length;
            MaxLength = maxlength;
            IsHex = isHex;
            Prefix = prefix;            
        }
    }

    public abstract class PluginBase
    {
        public static readonly string[] ValidFields = { "p", "password", "u", "username", "n", "name", "d", "date", "i", "number", "v", "value" };
        public static readonly char[] Hex = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };
        public const int MaxSessionsDefault = 20;

        private static long _sharedProgressTotal;
        private static long _sharedProgress;

        private static MD5 _md5;

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

        public static string CreateTempFolder()
        {
            var current = Directory.GetCurrentDirectory();
            var options = new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive };

            //Get all folders starting with temp
            var count = 0;
            var path = $"{current}\\Temp";

            while (Directory.Exists(path))
            {
                path = Path.Combine(current, $"{current}\\Temp{count}");
                count++;
            }

            Directory.CreateDirectory(path);

            return path;
        }

        public static void CombineFiles(string path, string inputPattern, string outputFilePath)
        {
            var inputFilePaths = Directory.GetFiles(path, inputPattern);

            using (var outputStream = File.Create(outputFilePath))
            {
                foreach (var inputFilePath in inputFilePaths)
                {
                    using (var inputStream = File.OpenRead(inputFilePath))
                    {
                        // Buffer size can be passed as the second argument.
                        inputStream.CopyTo(outputStream);
                    }
                }
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
                    if (domainSplits.MoveToEnd() < 2) return false;

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
            if (hash.Length < info.Length) return false;
            if (hash.Length > info.MaxLength) return false;

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
            //0      8743b52063cd84097a65d1633f5c74f5  MD5
            //70     2303b15bfa48c74a74758135a0df1201  md5(utf16le($pass))
            if (mode == 0 || mode == 70) return new HashInfo(mode, 1, 32, true);

            //10     01dfae6e5d4d90d9892622325959afbe:7050461         md5($pass.$salt)
            //20     f0fda58630310a6dd91a7d8f0a4ceda2:4225637426      md5($salt.$pass)
            //30     b31d032cfdcf47a399990a71e43c5d2a:144816          md5(utf16le($pass).$salt)
            //40     d63d0e21fdc05f618d55ef306c54af82:13288442151473  md5($salt.utf16le($pass))
            //50     fc741db0a2968c39d9c2a5cc75b05370:1234            HMAC-MD5 (key = $pass)
            //60     bfd280436f45fa38eaacac3b00518f29:1234            HMAC-MD5 (key = $salt)
            if (mode == 10 || mode == 20 || mode == 30 || mode == 40 || mode == 50 || mode == 60) return new HashInfo(mode, 2, 32, true);

            //100    b89eaac7e61417341b710b727768294d0e6a277b  SHA1
            //170  	 b9798556b741befdbddcbf640d1dd59d19b1e193  sha1(utf16le($pass))
            //300    fcf7c1b8749cf99d88e5f34271d636178fb5d130  MySQL4.1/MySQL5
            if (mode == 100 || mode == 170 || mode == 300) return new HashInfo(mode, 1, 40, true);

            //110    2fc5a684737ce1bf7b3b239df432416e0dd07357:2014           sha1($pass.$salt)
            //120    cac35ec206d868b7d7cb0b55f31d9425b075082b:5363620024     sha1($salt.$pass)
            //130    c57f6ac1b71f45a07dbd91a59fa47c23abcd87c2:631225         sha1(utf16le($pass).$salt)
            //140    5db61e4cd8776c7969cfd62456da639a4c87683a:8763434884872  sha1($salt.utf16le($pass))
            //150    c898896f3f70f61bc3fb19bef222aa860e5ea717:1234           HMAC-SHA1 (key = $pass)
            //160    d89c92b4400b15c39e462a8caa939ab40c3aeeea:1234           HMAC-SHA1 (key = $salt)
            if (mode == 110 || mode == 120 || mode == 130 || mode == 140 || mode == 150 || mode == 160) return new HashInfo(mode, 2, 40, true);

            //124    sha1$00003$2c5a9069be618cf209f7b21167d6e8cdd2dce76    
            if (mode == 124) return new HashInfo(mode, 1, 51, false);

            //400    $P$984478476IagS59wHZvyQMArzfx58u.  phpass, WordPress (MD5), Joomla(MD5)
            if (mode == 400) return new HashInfo(mode, 1, 34, false);

            //1400   127e6fbfe24a750e72930c220a8e138275656b8e5d8f48a98c3c92df2caba935  SHA2-256
            //1470   9e9283e633f4a7a42d3abc93701155be8afe5660da24c8758e7d3533e2f2dc82  sha256(utf16le($pass))
            if (mode == 1400 || mode == 1470) return new HashInfo(mode, 1, 64, true);

            //1410   c73d08de890479518ed60cf670d17faa26a4a71f995c1dcc978165399401a6c4:53743528  	sha256($pass.$salt)
            //1420   eb368a2dfd38b405f014118c7d9747fcc97f4f0ee75c05963cd9da6ee65ef498:560407001617  sha256($salt.$pass)
            //1430   4cc8eb60476c33edac52b5a7548c2c50ef0f9e31ce656c6f4b213f901bc87421:890128        sha256(utf16le($pass).$salt)
            //1440   a4bd99e1e0aba51814e81388badb23ecc560312c4324b2018ea76393ea1caca9:12345678      sha256($salt.utf16le($pass))
            //1450   abaf88d66bf2334a4a8b207cc61a96fb46c3e38e882e6f6f886742f688b8588c:1234          HMAC-SHA256 (key = $pass)
            //1460   8efbef4cec28f228fa948daaf4893ac3638fbae81358ff9020be1d7a9a509fc6:1234          HMAC-SHA256 (key = $salt)
            if (mode == 1410 || mode == 1420 || mode == 1430 || mode == 1440 || mode == 1450 || mode == 1460) return new HashInfo(mode, 2, 64, true);

            //1700   82a9dda829eb7f8ffe9fbe49e45d47d2dad96 .... 83c6840f10e8246b9db54a4859b7ccd0123d86e5872c1e5082f             sha5125
            if (mode == 1700 || mode == 1770) return new HashInfo(mode, 1, 128, true);

            //1710   e5c3ede3e49fb86592fb03f471c35ba13e8d5 .... 9a8fdafb635fa2223c24e5558fd9313e8995019dcbec1fb5841:6352283260  sha512($salt.$pass)
            if (mode == 1710 || mode == 1720 || mode == 1730 || mode == 1740 || mode == 1750 || mode == 1760) return new HashInfo(mode, 2, 128, true);

            //1800	 $6$52450745$k5ka2p8bFuSmoVT1tzOyyuaREkkKBcCNqoDKzYiJL9RaE8yMnPgh2XzzF0NDrUhgrcLwg78xs1w5pJiypEdFX/  sha512crypt $6$, SHA512 (Unix)
            if (mode == 1800) return new HashInfo(mode, 1, 106, false);

            //2811   b2771af9d6e8395c72254bbc379dd092:NqPyawIn  
            if (mode == 2811) return new HashInfo(mode, 2, 32, true);

            //3200   $2a$05$LhayLxezLhK1LhWvKxCyLOj0j1u.Kj0jZ0pEmm134uzrQlFvQJLF6  bcrypt $2*$, Blowfish (Unix)
            if (mode == 3200) return new HashInfo(mode, 1, 60, false);

            if (mode == 2611) return new HashInfo(mode, 2, 32, true);

            //2711   dfd92028c0e948a4a33eaf3924277b12:llu(Q\q~%ur!H$z9W4B)z$opBO#&^^
            if (mode == 2711) return new HashInfo(mode, 2, 32, true);

            //10000  pbkdf2_sha256$20000$H0dPx8NeajVu$GiC4k5kqbbR9qWBlsRgDywNqC2vd9kqfk7zdorEnNas=  Django (PBKDF2-SHA256)
            if (mode == 10000) return new HashInfo(mode, 1, 77, 79, false);

            //10800 | SHA2 - 384
            //17500 | SHA3 - 384
            //17900 | Keccak - 384
            //10870 | sha384(utf16le($pass))
            if (mode == 10800 || mode == 10870 || mode == 17500 || mode == 17900) return new HashInfo(mode, 1, 96, true);

            //10810 | sha384($pass.$salt)
            //10820 | sha384($salt.$pass)
            //10840 | sha384($salt.utf16le($pass))
            //10830 | sha384(utf16le($pass).$salt)
            if (mode == 10810 || mode == 10820 || mode == 10840 || mode == 10830) return new HashInfo(mode, 2, 96, true);

            //25600  $2a$05$/VT2Xs2dMd8GJKfrXhjYP.DkTjOVrY12yDN7/6I8ZV0q/1lEohLru  bcrypt(md5($pass))/bcryptmd5
            if (mode == 25600) return new HashInfo(mode, 1, 60, false, "$2");

            //27200  3999d08db95797891ec77f07223ca81bf43e1be2:5dcc47b04c49d3c8e1b9e4ec367fddeed21b7b85  Ruby on Rails Restful Auth (one round, no sitekey)
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

        public static IEnumerable<List<T>> SplitList<T>(List<T> locations, int nSize = 256)
        {
            for (int i = 0; i < locations.Count; i += nSize)
            {
                yield return locations.GetRange(i, Math.Min(nSize, locations.Count - i));
            }
        }

        public static bool CheckForFiles(string[] paths)
        {
            return CheckOverwrite(paths);
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
            entity.AddValues(name.ToString());

            //Remove special characters, giving us alpha and numerics
            //Regex expression is cached
            var value = name.RemoveSpecialCharacters();

            //We will let rules take care of single digit numbers
            //We are really more interested in special numbers and dates of birth etc here
            if (int.TryParse(value, out var number))
            {
                if (number > 9) entity.AddNumbers(value.ToString());
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
                                if (number2 > 9) entity.AddNumbers(other.ToString());
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
