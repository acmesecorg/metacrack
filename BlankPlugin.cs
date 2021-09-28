namespace Malfoy
{
    public class BlankPlugin : PluginBase
    {
        private static string _outputHashPath = "";
        private static string _outputWordPath = "";

        private static readonly object _lock = new object();

        private const int TaskCount = 16;

        public static void Process(BlankOptions options)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var fileEntries = Directory.GetFiles(currentDirectory, options.InputPath);

            if (fileEntries.Length == 0)
            {
                WriteMessage($"Lookup file(s) {options.InputPath} was not found.");
                return;
            }

            if (options.Hash > 0) WriteMessage($"Validating hash mode {options.Hash}.");

            WriteMessage($"Started at {DateTime.Now.ToShortTimeString()}.");

            var size = Common.GetFileEntriesSize(fileEntries);
            var progressTotal = 0L;
            var lineCount = 0;

            foreach (var filePath in fileEntries)
            {
                //Create a version based on the file size, so that the hash and dict are bound together
                var fileInfo = new FileInfo(filePath);
                var version = GetSerial(fileInfo, "b");

                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var filePathName = $"{currentDirectory}\\{fileName}";
                _outputHashPath = $"{filePathName}.{version}.hash";
                _outputWordPath = $"{filePathName}.{version}.word";

                //Check that there are no output files
                if (!Common.CheckForFiles(new string[] { _outputHashPath, _outputWordPath }))
                {
                    WriteHighlight($"Skipping {filePathName}.");

                    progressTotal += fileInfo.Length;
                    continue;
                }

                var inputs = new List<string>();
                var blanks = new List<string>();

                //Loop through and check if each email contains items from the lookup, if so add them
                using (var reader = new StreamReader(filePath))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        var splits = line.Split(':');

                        if (splits.Length == 2)
                        {
                            if (!ValidateEmail(splits[0], out var emailStem)) continue;
                            if (!ValidateHash(splits[1], options.Hash)) continue;

                            inputs.Add(splits[1]);
                            blanks.Add("");
                        }

                        lineCount++;
                        progressTotal += line.Length;

                        //Update the percentage
                        WriteProgress("Processing files", progressTotal, size);
                    }
                }

                File.AppendAllLines(_outputHashPath, inputs);
                File.AppendAllLines(_outputWordPath, blanks);
            }

            WriteMessage($"Completed at {DateTime.Now.ToShortTimeString()}.");            
        }
    }
}
