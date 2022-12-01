using System.Text.RegularExpressions;

namespace Metacrack
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

            if (options.Hash == -1 && !options.ValidateEmailOnly && string.IsNullOrEmpty(options.Regex))
            {
                WriteMessage("Specify a hash mode using the --hash option");
                return;
            }

            if (!string.IsNullOrEmpty(options.Regex) && options.Hash != -1)
            {
                WriteMessage("Specify either a mode or regex.");
                return;
            }

            if (options.Hash > - 1) WriteMessage($"Validating hash mode {options.Hash}");
            if (options.Iterations > 0) WriteMessage($"Validating iterations {options.Iterations}.");

            if (options.ValidateEmail && options.ValidateEmailOnly)
            {
                WriteMessage("Options--email and --email-only cannot both be specified.");
                return;
            }

            if (options.ValidateEmail) WriteMessage("Validating email");
            if (options.ValidateEmailOnly) WriteMessage("Validating email only");

            var regEx = default(Regex);

            //Validate the regex
            if (!string.IsNullOrEmpty(options.Regex))
            {
                WriteMessage($"Validating using supplied regex");
                try
                {
                    regEx = new Regex(options.Regex);
                }
                catch (Exception ex)
                {
                    WriteError(ex.Message);
                    return;
                }                
            }

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

                _outputValidPath =  $"{filePathName}.valid{fileInfo.Extension}";
                _outputInvalidPath = $"{filePathName}.invalid{fileInfo.Extension}";

                //Put the iterations in the output instead
                if (options.Iterations > 0) _outputValidPath = $"{filePathName}.{options.Iterations}{fileInfo.Extension}";

                //Check that there are no output files
                if (!CheckForFiles(new string[] { _outputValidPath }))
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
                        var line = reader.ReadLineAsEmailHash();
                        var emailValid = true;

                        if (options.ValidateEmail || options.ValidateEmailOnly) emailValid = ValidateEmail(line.Email, out var emailStem);

                        if (options.ValidateEmailOnly)
                        {
                            if (emailValid)
                            {
                                valid.Add(line.Text);
                            }
                            else
                            {
                                invalid.Add(line.Text);
                            }
                        }
                        else
                        {
                            var column = options.Column;

                            if (options.NoEmail) column--;

                            if (emailValid && line.SplitLength > column)
                            {
                                if (regEx != null)
                                {
                                    if (regEx.IsMatch(line.FullHash))
                                    {
                                        valid.Add(line.Text);
                                    }
                                    else
                                    {
                                        invalid.Add(line.Text);
                                    }
                                }
                                else
                                {
                                    if (ValidateHash(line.FullHash, line.HashPart, hashInfo, options.Iterations))
                                    {
                                        valid.Add(line.Text);
                                    }
                                    else
                                    {
                                        invalid.Add(line.Text);
                                    }
                                }
                            }
                            else
                            {
                                invalid.Add(line.Text);
                            }
                        }

                        lineCount++;
                        progressTotal = reader.BaseStream.Position;

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
