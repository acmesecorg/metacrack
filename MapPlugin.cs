using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Metacrack
{
    public class MapPlugin : PluginBase
    {
        private static string _outputHashPath = "";
        private static string _outputWordPath = "";

        public static void Process(MapOptions options)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var fileEntries = Directory.GetFiles(currentDirectory, options.InputPath);

            if (fileEntries.Length == 0)
            {
                WriteMessage($"Lookup file(s) {options.InputPath} not found.");
                return;
            }

            var mapEntries = new string[] { };
            var version = "map";

            if (options.MapPath == "")
            {
                if (options.Limit > 0)
                {
                    WriteMessage($"Cannot use --limit with no map file.");
                    return;
                }
                else
                {
                    WriteMessage("No map path specified. Creating blank worldlist.");
                    version = "blank";
                }
            }
            else
            {
                mapEntries = Directory.GetFiles(currentDirectory, options.MapPath);

                if (mapEntries.Length == 0)
                {
                    WriteMessage($"Map file(s) {options.MapPath} was not found.");
                    return;
                }
            }

            if (options.Hash > 0) WriteMessage($"Validating hash mode {options.Hash}.");

            WriteMessage($"Started at {DateTime.Now.ToShortTimeString()}.");

            //Load map file entries
            var map = new List<string>();

            WriteMessage("Loading map files.");

            foreach (var mapEntry in mapEntries)
            {
                map.AddRange(File.ReadAllLines(mapEntry));
            }

            //Take the first n items
            if (options.Limit > 0) map = map.Take(options.Limit).ToList();

            //Cater for blank scenario ie no map entries
            if (map.Count == 0) map.Add("");

            var size = GetFileEntriesSize(fileEntries);
            var progressTotal = 0L;
            var lineCount = 0;
            var hashInfo = GetHashInfo(options.Hash);

            foreach (var filePath in fileEntries)
            {
                //Create a version based on the file size, so that the hash and dict are bound together
                var fileInfo = new FileInfo(filePath);
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var filePathName = $"{currentDirectory}\\{fileName}";

                _outputHashPath = $"{filePathName}.{version}.hash";
                _outputWordPath = $"{filePathName}.{version}.word";

                //Check that there are no output files
                if (!CheckForFiles(new string[] { _outputHashPath, _outputWordPath }))
                {
                    WriteHighlight($"Skipping {filePathName}.");

                    progressTotal += fileInfo.Length;
                    continue;
                }

                var hashes = new List<string>();
                var words = new List<string>();

                //Loop through and check if each email contains items from the lookup, if so add them
                using (var reader = new StreamReader(filePath))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        var splits = line.Split(':');

                        if (splits.Length == 2 || splits.Length == 3)
                        {
                            if (!ValidateEmail(splits[0], out var emailStem)) continue;
                            if (!ValidateHash(splits[1], hashInfo)) continue;

                            var hash = (splits.Length == 2) ? splits[1] : $"{splits[1]}:{splits[2]}";

                            //Loop through the map and add hash and word pair
                            foreach (var word in map)
                            {
                                hashes.Add(hash);
                                words.Add(word);
                            }
                        }

                        lineCount++;
                        progressTotal += line.Length;

                        //Update the percentage
                        if (lineCount % 1000 == 0) WriteProgress($"Processing {fileName}", progressTotal, size);
                    }
                }

                File.AppendAllLines(_outputHashPath, hashes);
                File.AppendAllLines(_outputWordPath, words);
            }

            WriteMessage($"Completed at {DateTime.Now.ToShortTimeString()}.");            
        }
    }
}
