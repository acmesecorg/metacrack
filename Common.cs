using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Malfoy
{
    public static class Common
    {
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
    }
}
