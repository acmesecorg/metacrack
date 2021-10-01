namespace Malfoy
{
    public class ParsePlugin : PluginBase
    {
        public static void Process(ParseOptions options)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var fileEntries = Directory.GetFiles(currentDirectory, options.InputPath);

            if (fileEntries.Length == 0)
            {
                WriteMessage($"File(s) {options.InputPath} not found.");
                return;
            }

            var size = GetFileEntriesSize(fileEntries);
            var progressTotal = 0L;
            var output = new List<string>();
            var notparsed = new List<string>();

            foreach (var filePath in fileEntries)
            {
                var fileInfo = new FileInfo(filePath);
                var dict = new Dictionary<string, int>();

                //Create a version based on the file size, so that the hash and dict are bound together
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var filePathName = $"{currentDirectory}\\{fileName}";

                var outputPath = $"{filePathName}.parse.txt";
                var outputNotParsedPath = $"{filePathName}.notparsed.txt";

                //Check that there are no output files
                if (!CheckForFiles(new string[] { outputPath, outputNotParsedPath }))
                {
                    WriteHighlight($"Skipping {filePathName}.");

                    progressTotal += fileInfo.Length;
                    continue;
                }

                //Loop through and check if each email contains items from the lookup, if so add them
                using (var reader = new StreamReader(filePath))
                {
                    var email = "";

                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();

                        progressTotal += line.Length + 1;

                        //Password must follow line after email, or everythign is reset
                        if (line.StartsWith("Username:"))
                        {
                            var token = line.Split("Username:", StringSplitOptions.TrimEntries)[1];
                            if (ValidateEmail(token, out var emailSteam))
                            {
                                email = emailSteam;
                            }
                            else
                            {
                                notparsed.Add(line);
                            }
                        }
                        else if (line.StartsWith("Password:") && email != "")
                        {
                            var password = line.Split("Password:", StringSplitOptions.TrimEntries)[1];

                            output.Add($"{email}:{password}");
                            email = "";
                        }
                        else
                        {
                            email = "";
                            notparsed.Add(line);
                        }

                        //Update the percentage
                        WriteProgress("Parsing files", progressTotal, size);
                    }
                }

                //Check if we must deduplicate
                if (options.Deduplicate)
                {
                    File.AppendAllLines(outputPath, output.Distinct());
                }
                else
                {
                    File.AppendAllLines(outputPath, output);
                }

                File.AppendAllLines(outputNotParsedPath, notparsed);
            }
        }
    }
}
