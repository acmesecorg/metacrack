using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Malfoy
{
    public static class Common
    {
        public static bool IsCommonHash(string value)
        {
            var length = value.Length;
            if (length != 32 && length != 40 ) return false;
            return IsHash(value, length);
        }

        public static bool IsHash(string value, int length)
        {
            if (value.Length != length) return false;
            
            var chars = value.ToCharArray();
            bool isHex;

            foreach (var c in chars)
            {
                isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!isHex) return false;
            }
            return true;
        }

        public static bool CheckForFiles(string[] paths)
        {
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    var fileInfo = new FileInfo(path);

                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"Existing file {fileInfo.Name} would be overwritten.");
                    Console.ResetColor();

                    return false;
                }
            }

            return true;
        }

        public static string GetCommandLineArgument(string[] args, int index, string name)
        {
            name = name.ToLower();

            //Just return by position if no switches
            if (index > -1)
            {
                if (index >= args.Length) return null; //index of of bounds for values provided, return not found
                var result = args[index];
                if (result.StartsWith(@"-")) return null; //If result is a switch then not found

                return result;
            }

            //Find the matching switch parameter, an optionally the next value
            for (int i = 0; i < args.Length; i++)
            {
                var lower = args[i].ToLower();
                if (lower == @"-" + name && i < args.Length)
                {
                    //Last argument
                    if (i + 1 > args.Length - 1) return ""; //return default value

                    //check for value, or another switch
                    var result = args[i + 1];
                    if (result.StartsWith(@"-")) return ""; //return default value
                    return result;
                }
            }

            return null; //not found
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

        public static string FormatFileSize(long bytes)
        {
            var unit = 1024;
            if (bytes < unit) { return $"{bytes} B"; }

            var exp = (int)(Math.Log(bytes) / Math.Log(unit));
            return $"{bytes / Math.Pow(unit, exp):F2} {("KMGTPE")[exp - 1]}B";
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
            return $"{prefix}{version.ToLower().Substring(version.Length - 3, 3)}";
        }
    }
}
