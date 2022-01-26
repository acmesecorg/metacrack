namespace Metacrack
{
    public class HashPlugin : PluginBase
    {
        public static void Process(HashOptions options)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var fileEntries = Directory.GetFiles(currentDirectory, options.InputPath);

            if (fileEntries.Length == 0)
            {
                WriteMessage($"Hash file(s) {options.InputPath} not found.");
                return;
            }

            var size = GetFileEntriesSize(fileEntries);
            var progressTotal = 0L;
            var updateMod = 1000;

            if (size > 100000000) updateMod = 10000;

            foreach (var filePath in fileEntries)
            {
                //Create a version based on the file size, so that the hash and dict are bound together
                var fileInfo = new FileInfo(filePath);
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var filePathName = $"{currentDirectory}\\{fileName}";

                var lineCount = 0;
                var output = new List<string>();

                //Loop through and check each hash
                using (var reader = new StreamReader(filePath))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();

                        lineCount++;
                        progressTotal += line.Length + 2;

                        output.Add(HashMd5(line));

                        //Update the percentage every 1k or so. 
                        //This is very slow, so optimise this
                        if (lineCount % updateMod == 0) WriteProgress($"Processing {fileInfo.Name}", progressTotal, size);
                    }
                }

                //Write final block
                if (output.Count() > 0) File.AppendAllLines($"{filePathName}.{options.Hash}{fileInfo.Extension}", output);
            }
            
        }
    }
}
