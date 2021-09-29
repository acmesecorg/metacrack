namespace Malfoy
{
    public class ValidatePlugin : PluginBase
    {
        private static string _outputValidPath = "";
        private static string _outputInvalidPath = "";

        public static void Process(ValidateOptions options)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var fileEntries = Directory.GetFiles(currentDirectory, options.InputPath);

            if (fileEntries.Length == 0)
            {
                WriteMessage($"Lookup file(s) {options.InputPath} not found.");
                return;
            }

            WriteMessage($"Validating hash mode {options.Hash}.");

            WriteMessage($"Started at {DateTime.Now.ToShortTimeString()}.");

            var size = GetFileEntriesSize(fileEntries);
            var progressTotal = 0L;
            var lineCount = 0;

            foreach (var filePath in fileEntries)
            {
                //Create a version based on the file size, so that the hash and dict are bound together
                var fileInfo = new FileInfo(filePath);
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var filePathName = $"{currentDirectory}\\{fileName}";

                _outputValidPath = $"{filePathName}.valid{fileInfo.Extension}";
                _outputInvalidPath = $"{filePathName}.invalid{fileInfo.Extension}";

                //Check that there are no output files
                if (!CheckForFiles(new string[] { _outputValidPath, _outputInvalidPath }))
                {
                    WriteHighlight($"Skipping {filePathName}.");

                    progressTotal += fileInfo.Length;
                    continue;
                }

                var valid = new List<string>();
                var invalid = new List<string>();

                //Loop through and check each hash
                using (var reader = new StreamReader(filePath))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        var splits = line.Split(':');

                        if (splits.Length > options.Column)
                        {
                            if (ValidateHash(splits[options.Column], options.Hash, options.Iterations))
                            {
                                valid.Add(line);
                            }
                            else
                            {
                                invalid.Add(line);
                            }
                        }
                        else
                        {
                            invalid.Add(line);
                        }

                        lineCount++;
                        progressTotal += line.Length;

                        //Update the percentage
                        WriteProgress("Processing files", progressTotal, size);
                    }
                }

                File.AppendAllLines(_outputValidPath, valid);
                File.AppendAllLines(_outputInvalidPath, invalid);
            }

            WriteMessage($"Completed at {DateTime.Now.ToShortTimeString()}.");            
        }
    }
}
