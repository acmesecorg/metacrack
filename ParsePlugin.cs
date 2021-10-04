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

            var names = new List<string>();
            
            if (options.ParseType == "log" && options.Names.Count() == 2)
            {
                foreach (var name in options.Names) names.Add($"{name}:");
            }
            else
            {
                names.Add("Username:");
                names.Add("Password:");
            }
            
            if (options.ParseType == "log") WriteMessage($"Using names {String.Join(",", names)}.");

            var size = GetFileEntriesSize(fileEntries);
            var progressTotal = 0L;

            //Determine columns;
            int[] columns = (options.Columns.Count() == 0) ? new int[] { 1 } : Array.ConvertAll(options.Columns.ToArray(), s => int.Parse(s));
            int maxColumn = columns.Max();

            foreach (var filePath in fileEntries)
            {
                var output = new List<string>();
                var notparsed = new List<string>();

                var fileInfo = new FileInfo(filePath);
                var dict = new Dictionary<string, int>();

                //Create a version based on the file size, so that the hash and dict are bound together
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var filePathName = $"{currentDirectory}\\{fileName}";

                var outputPath = $"{filePathName}.parse.txt";
                var outputNotParsedPath = $"{filePathName}.noparse.txt";

                //Check that there are no output files
                if (!CheckForFiles(new string[] { outputPath, outputNotParsedPath }))
                {
                    WriteHighlight($"Skipping {filePathName}.");

                    progressTotal += fileInfo.Length;
                    continue;
                }

                var lineCount = 0;

                //Loop through and check if each email contains items from the lookup, if so add them
                using (var reader = new StreamReader(filePath))
                {
                    var email = "";

                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();

                        lineCount++;
                        progressTotal += line.Length + 2;

                        if (options.ParseType.ToLower() == "log")
                        {
                            //Password must follow line after email, or everythign is reset
                            if (line.StartsWith(names[0]))
                            {
                                var token = line.Split(names[0], StringSplitOptions.TrimEntries)[1];
                                if (ValidateEmail(token, out var emailSteam))
                                {
                                    email = emailSteam;
                                }
                                else
                                {
                                    notparsed.Add(line);
                                }
                            }
                            else if (line.StartsWith(names[1]) && email != "")
                            {
                                var password = line.Split(names[1], StringSplitOptions.TrimEntries)[1];

                                output.Add($"{email}:{password}");
                                email = "";
                            }
                            else
                            {
                                email = "";
                                notparsed.Add(line);
                            }
                        }
                        //Split by delimiter
                        else if (options.ParseType == "delimited")
                        {
                            var splits = line.Split(options.Delimiter, StringSplitOptions.TrimEntries);
                            var values = new List<string>();

                            if (splits.Length > maxColumn)
                            {
                                foreach (var column in columns)
                                {
                                    values.Add(splits[column]);
                                }
                                output.Add(String.Join(":", values));
                            }
                            else
                            {
                                notparsed.Add(line);
                            }
                        }

                        //Update the percentage
                        if (lineCount % 1000 == 0) WriteProgress($"Parsing {fileName}", progressTotal, size);
                    }
                }

                //Check if we must deduplicate
                File.AppendAllLines(outputPath, options.Deduplicate ? output.Distinct() : output);
                File.AppendAllLines(outputNotParsedPath, notparsed);
            }
        }
    }
}
