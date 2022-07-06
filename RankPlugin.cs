using System.Text;

namespace Metacrack
{
    public class RankPlugin : PluginBase
    {
        public static void Process(RankOptions options)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var fileEntries = Directory.GetFiles(currentDirectory, options.InputPath);

            if (fileEntries.Length == 0)
            {
                WriteMessage($"Lookup file(s) {options.InputPath} not found.");
                return;
            }

            if (options.OutputPath.Length > 0 && File.Exists(options.OutputPath))
            {
                WriteError($"File {options.OutputPath} already exists.");
                return;
            }

            //Parse out any special modes
            if (options.Rules) WriteMessage($"Outputting rule file.");

            var size = GetFileEntriesSize(fileEntries);
            var progressTotal = 0L;
            var lineCount = 0L;

            foreach (var filePath in fileEntries)
            {
                var fileInfo = new FileInfo(filePath);
                var dict = new Dictionary<string, int>();

                //Loop through and check if each email contains items from the lookup, if so add them
                using (var reader = new StreamReader(filePath))
                {
                    while (!reader.EndOfStream)
                    {
                        lineCount++;

                        var line = reader.ReadLine();
                        progressTotal += line.Length + 1;

                        if (options.DebugMode == 1)
                        {
                            if (dict.ContainsKey(line))
                            {
                                dict[line]++;
                            }
                            else
                            {
                                dict[line] = 1;
                            }
                        }
                        else if (options.DebugMode == 4)
                        {
                            //Deal with ::: where : is a valid value
                            var splits = line.Split(':');
                            var rule = "";
                            if (splits.Length == 4)
                            {
                                rule = ":";
                            }
                            else if(splits.Length == 3)
                            {
                                rule = splits[1];
                            }

                            if (rule != "")
                            {
                                if (dict.ContainsKey(rule))
                                {
                                    dict[rule]++;
                                }
                                else
                                {
                                    dict[rule] = 1;
                                }
                            }
                        }
                        else
                        { 
                            var splits = line.Split(':');

                            if (splits.Length == 2)
                            {
                                var key = splits[1];
                                if (dict.ContainsKey(key))
                                {
                                    dict[key]++;
                                }
                                else
                                {
                                    dict[key] = 1;
                                }
                            }
                        }

                        if (lineCount % 1000 == 0) WriteProgress($"Calculating", progressTotal, size);
                    }
                }

                TryParse(options.Count, out int count);
                TryParse(options.Keep, out int keep);

                var longest = 0;
                var longestValue = 0;
                var total = 0L;

                //Now sort the dictionary and write out the results
                if (options.DebugMode > 0)
                {
                    total = dict.Keys.Count();
                    WriteMessage($"Writing out rules {fileInfo.Name} ({total} entries)");

                    var lines = new List<string>();
                    var sorted = dict.OrderByDescending(x => x.Value).Take(count);
                    
                    WriteMessage($"Results for: {fileInfo.Name} ({total} entries)");

                    //Loop through and count longest word
                    foreach (var pair in sorted)
                    {
                        if (pair.Key.Length > longest) longest = pair.Key.Length;
                        if (pair.Value.ToString().Length > longestValue) longestValue = pair.Value.ToString().Length;
                    }

                    //Add an extra space so that our output aligns in the console with one space
                    longest++;

                    foreach (var pair in sorted)
                    {
                        var percent = (int)((double)pair.Value / total * 100);

                        WriteMessage($"{pair.Key}{new string(' ', longest - pair.Key.Length + longestValue - pair.Value.ToString().Length)}{pair.Value} ({percent}%)");
                    }

                    //Write out to file
                    sorted = dict.OrderByDescending(x => x.Value);

                    foreach (var pair in sorted)
                    {
                        if (pair.Value >= keep) lines.Add(pair.Key);                       
                    }

                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var filePathName = $"{currentDirectory}\\{fileName}";

                    if (lines.Count > 0)
                    {
                        File.AppendAllLines($"{filePathName}.{options.Keep}.rule", lines);
                        WriteMessage($"Wrote out {lines.Count} rules to {filePathName}.{keep}.rule");
                    }
                    
                    //Write out keys
                    if (options.OutputPath != null)
                    {
                        var outputPath = Path.Combine(currentDirectory, options.OutputPath);
                        File.AppendAllLines(outputPath, dict.Keys);
                        WriteMessage($"Wrote out {dict.Keys.Count} values to {options.OutputPath}");
                    }
                }
                else
                {
                    var sorted = dict.OrderByDescending(x => x.Value).Take(count);
                    total = dict.Keys.Count();

                    WriteMessage($"Results for: {fileInfo.Name} ({total} entries)");

                    //Loop through and count longest word
                    foreach (var pair in sorted)
                    {
                        if (pair.Key.Length > longest) longest = pair.Key.Length;
                        if (pair.Value.ToString().Length > longestValue) longestValue = pair.Value.ToString().Length;
                    }

                    //Add an extra space so that our output aligns in the console with one space
                    longest++;

                    if (count < 1000)
                    {
                        foreach (var pair in sorted)
                        {
                            var percent = (int)((double)pair.Value / total * 100);

                            WriteMessage($"{pair.Key}{new string(' ', longest - pair.Key.Length + longestValue - pair.Value.ToString().Length)}{pair.Value} ({percent}%)");
                        }
                    }

                    //Write out keys
                    if (options.OutputPath != null)
                    {
                        var outputPath = Path.Combine(currentDirectory, options.OutputPath);
                        var output = new List<string>();

                        //Loop through each word and write it out as a rule
                        foreach (var pair in sorted)
                        {
                            if (options.Rules)
                            {
                                var builder = new StringBuilder();
                                foreach (var k in pair.Key)
                                {
                                    builder.Append('$');
                                    builder.Append(k);
                                }

                                output.Add(builder.ToString());
                            }
                            else
                            {
                                var percent = (int)((double)pair.Value / total * 100);
                                output.Add($"{pair.Key}{new string(' ', longest - pair.Key.Length + longestValue - pair.Value.ToString().Length)}{pair.Value} ({percent}%)");
                            }

                            if (output.Count == count) break;
                        }

                        File.AppendAllLines(outputPath, output);
                        WriteMessage($"Wrote out {output.Count} {((options.Rules) ? "rules": "values")} to {options.OutputPath}");                       
                    }
                }
            }
        }
    }
}
