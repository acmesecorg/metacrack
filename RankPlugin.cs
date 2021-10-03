namespace Malfoy
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

            foreach (var filePath in fileEntries)
            {
                var fileInfo = new FileInfo(filePath);
                var dict = new Dictionary<string, int>();

                //Loop through and check if each email contains items from the lookup, if so add them
                using (var reader = new StreamReader(filePath))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
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
                }

                //Now sort the dictionary and write out the results
                var sorted = dict.OrderByDescending(x => x.Value).Take(options.Count);
                var total = dict.Keys.Count();

                WriteMessage($"Results for: {fileInfo.Name} ({total} entries)");

                //Loop through and count longest word
                var longest = 0;
                var longestValue = 0;

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
            }
        }
    }
}
