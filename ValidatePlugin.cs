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
                WriteMessage($"Lookup file(s) {options.InputPath} not found");
                return;
            }

            if (options.Hash == -1 && !options.ValidateEmailOnly)
            {
                WriteMessage("Specify a hash mode using the --hash option");
                return;
            }

            WriteMessage($"Validating hash mode {options.Hash}");
            if (options.Iterations > 0) WriteMessage($"Validating iterations {options.Iterations}.");

            if (options.ValidateEmail && options.ValidateEmailOnly)
            {
                WriteMessage("Options--email and --email-only cannot both be specified.");
                return;
            }

            if (options.ValidateEmail) WriteMessage("Validating email");
            if (options.ValidateEmailOnly) WriteMessage("Validating email only");

            WriteMessage($"Started at {DateTime.Now.ToShortTimeString()}.");

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
                        var emailValid = true;

                        if (options.ValidateEmail || options.ValidateEmailOnly) emailValid = ValidateEmail(splits[0], out var emailStem);

                        if (options.ValidateEmailOnly)
                        {
                            if (emailValid)
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
                            if (emailValid && splits.Length > options.Column)
                            {
                                if (ValidateHash(splits[options.Column], hashInfo, options.Iterations))
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
                        }

                        lineCount++;
                        progressTotal += line.Length;

                        //Update the percentage
                        if (lineCount % 1000 == 0) WriteProgress($"Processing {fileName}", progressTotal, size);

                        //Dump the output every 1kk lines
                        if (lineCount % 1000000 == 0)
                        {
                            if (valid.Count > 0) File.AppendAllLines(_outputValidPath, valid);
                            if (invalid.Count > 0) File.AppendAllLines(_outputInvalidPath, invalid);

                            valid.Clear();
                            invalid.Clear();
                        }
                    }
                }

                if (valid.Count > 0) File.AppendAllLines(_outputValidPath, valid);
                if (invalid.Count > 0) File.AppendAllLines(_outputInvalidPath, invalid);
            }

            WriteMessage($"Completed at {DateTime.Now.ToShortTimeString()}.");            
        }
    }
}
