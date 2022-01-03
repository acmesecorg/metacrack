using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Metacrack
{
    public class SortPlugin : PluginBase
    {
        public static void Process(SortOptions options)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var fileEntries = Directory.GetFiles(currentDirectory, options.InputPath);

            if (fileEntries.Length == 0)
            {
                WriteMessage($"File(s) {options.InputPath} not found.");
                return;
            }

            if (options.Deduplicate) WriteMessage("Using option -deduplicate");

            var size = GetFileEntriesSize(fileEntries);
            var progressTotal = 0L;

            foreach (var filePath in fileEntries)
            {
                var fileInfo = new FileInfo(filePath);

                //Create a version based on the file size, so that the hash and dict are bound together
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var filePathName = $"{currentDirectory}\\{fileName}";

                var outputPath = $"{filePathName}.temp.txt";

                //Check that there are no output files
                if (!CheckOverwrite(new string[] { outputPath}))
                {
                    WriteHighlight($"Skipping {filePathName}.");

                    progressTotal += fileInfo.Length;
                    continue;
                }

                var output = new List<string>();

                var lineCount = 0;

                //Loop through and check if each email contains items from the lookup, if so add them
                using (var reader = new StreamReader(filePath))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();

                        lineCount++;
                        progressTotal += line.Length + 2;

                        //We start from line 1 
                        output.Add(line);

                        //Update the percentage
                        if (lineCount % 1000 == 0) WriteProgress($"Reading {fileName}", progressTotal, size);
                    }
                }

                //Sort and optionally deduplicate
                

                if (options.Deduplicate)
                {
                    WriteMessage("Sorting and deduplicating ...");
                    File.AppendAllLines(outputPath, output.OrderBy(q => q).Distinct());
                }
                else
                {
                    WriteMessage("Sorting ...");
                    File.AppendAllLines(outputPath, output.OrderBy(q => q));
                }

                File.Delete(filePath);
                File.Move(outputPath, filePath); //rename temp to original filename
            }
        }
    }
}
