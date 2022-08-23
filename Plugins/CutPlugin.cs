namespace Metacrack.Plugins
{
    public class CutPlugin : PluginBase
    {
        public static void Process(CutOptions options)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var fileEntries = Directory.GetFiles(currentDirectory, options.InputPath);

            TryParse(options.Start, out int start);
            TryParse(options.Start, out int end);

            if (fileEntries.Length == 0)
            {
                WriteMessage($"File(s) {options.InputPath} not found.");
                return;
            }

            if (start >= end )
            {
                WriteMessage("End value must be greater than start");
                return;
            }

            WriteMessage($"Cutting new file {options.OutputPath} from line {start} to line {end}");

            var size = GetFileEntriesSize(fileEntries);
            var progressTotal = 0L;

            foreach (var filePath in fileEntries)
            {
                var output = new List<string>();

                var fileInfo = new FileInfo(filePath);
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var filePathName = $"{currentDirectory}\\{fileName}";
                var outputPath = $"{currentDirectory}\\{options.OutputPath}";

                //Check that there are no output files
                if (!CheckForFiles(new string[] { outputPath }))
                {
                    WriteHighlight($"Skipping {filePathName}.");

                    progressTotal += fileInfo.Length;
                    continue;
                }

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
                        if (lineCount >= start && lineCount <= end) output.Add(line);

                        //Update the percentage
                        if (lineCount % 1000 == 0) WriteProgress($"Reading {fileName}", progressTotal, size);

                        //Write out buffer
                        if (output.Count > 1000000)
                        {
                            File.AppendAllLines(outputPath, output);

                            output.Clear();
                        }

                        //Break out early
                        if (lineCount > end) break;
                    }
                }

                //Check if we must deduplicate
                File.AppendAllLines(outputPath, output);
            }
        }
    }
}
