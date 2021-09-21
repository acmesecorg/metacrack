using Microsoft.SqlServer.TransactSql.ScriptDom;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Malfoy
{
    class Program
    {
        //private static string _directory = @"C:\Program Files\hashcat-6.2.2";
        //private static string _directory = @"D:\Breaches\Processing\Hashed\Glofox_RF\Data";
        //private static string _directory = @"D:\ClixSense_RF";
        //private static string _directory = "";
        //private static string _directory = @"D:\Processing";
        private static string _directory = @"D:\Breaches\Malfoy";

        static void Main(string[] args)
        {
            if (args == null || args.Length < 2)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Enter the name of hash file containing username:hash, and the lookup file containing hash:plain");
                Console.ResetColor();
                return;
            }

            //Check if we are parsing json 
            var jsonMode = Common.GetCommandLineArgument(args, -1, "j") != null;
            var sqlMode = Common.GetCommandLineArgument(args, -1, "s") != null;
            var s2mode = Common.GetCommandLineArgument(args, -1, "s2") != null;
            var hashMode = Common.GetCommandLineArgument(args, -1, "m") != null;

            var appendMode = Common.GetCommandLineArgument(args, -1, "a") != null;
            var catalogMode = Common.GetCommandLineArgument(args, -1, "c") != null;
            var lookupMode = Common.GetCommandLineArgument(args, -1, "l") != null;
            var stemMode = Common.GetCommandLineArgument(args, -1, "t") != null;
            var mapMode = Common.GetCommandLineArgument(args, -1, "map") != null;
            var countMode = Common.GetCommandLineArgument(args, -1, "count") != null;

            var ida = Common.GetCommandLineArgument(args, -1, "-ida") != null;

            //Get lookup file path
            var currentDirectory = Directory.GetCurrentDirectory();

            if (!string.IsNullOrEmpty(_directory))
            {
                currentDirectory = _directory;
                Console.WriteLine($"Using {currentDirectory} ...");
            }

            if (jsonMode)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"Detected json import mode.");
                Console.ResetColor();

                JsonImport.Process(currentDirectory, args);
                return;
            }

            if (sqlMode || s2mode)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"Detected SQL import mode.");
                Console.ResetColor();

                SqlImport.S2Mode = s2mode;
                SqlImport.Process(currentDirectory, args);
                return;
            }

            if (hashMode)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"Detected hash export mode.");
                Console.ResetColor();

                Hash.Ida = ida;
                Hash.Process(currentDirectory, args);
                return;
            }

            if (appendMode)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"Detected add mode.");
                Console.ResetColor();

                Append.Process(currentDirectory, args);
                return;
            }

            if (catalogMode)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"Detected Catalog mode.");
                Console.ResetColor();

                Catalog.Process(currentDirectory, args);
                return;
            }

            if (lookupMode)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"Detected lookup mode.");
                Console.ResetColor();

                Lookup.Process(currentDirectory, args);
                return;
            }

            if (stemMode)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"Detected stem mode.");
                Console.ResetColor();

                Stem.Process(currentDirectory, args).GetAwaiter().GetResult();
                return;
            }

            if (mapMode)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"Detected map mode.");
                Console.ResetColor();

                Map.Process(currentDirectory, args);
                return;
            }

            if (countMode)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"Detected count mode.");
                Console.ResetColor();

                Count.Process(currentDirectory, args);
                return;
            }

            //Default is export mode
            Export.Ida = ida;
            Export.Process(currentDirectory, args);
        }        
    }
}
