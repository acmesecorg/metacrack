using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Malfoy
{
    public class StemPlugin : PluginBase
    {
        private static string _outputHashPath = "";
        private static string _outputWordPath = "";

        private static readonly object _lock = new object();

        private const int TaskCount = 16;

        public static async Task ProcessAsync(StemOptions options)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var fileEntries = Directory.GetFiles(currentDirectory, options.InputPath);

            if (fileEntries.Length == 0)
            {
                WriteMessage($"Lookup file(s) {options.InputPath} was not found.");
                return;
            }

            //Get names input (if any)
            var sourceFiles = new string[]{ };

            if (!string.IsNullOrEmpty(options.NamesPath)) sourceFiles = Directory.GetFiles(currentDirectory, options.NamesPath);

            if (sourceFiles.Length > 0)
            {
                if (sourceFiles.Length == 1) WriteMessage($"Using names source file {sourceFiles[0]}");
                if (sourceFiles.Length > 1) WriteMessage($"Using {sourceFiles.Length} names source files");
            }

            if (options.Hash > 0) WriteMessage($"Validating hash mode {options.Hash}.");

            WriteMessage($"Started at {DateTime.Now.ToShortTimeString()}.");

            //Load the firstnames or other items used for stemming into a hashset
            var lookups = new HashSet<string>();
            var lineCount = 0;

            var size = GetFileEntriesSize(sourceFiles);
            var progressTotal = 0L;

            foreach (var lookupPath in sourceFiles)
            {
                using (var reader = new StreamReader(lookupPath))
                {
                    while (!reader.EndOfStream)
                    {
                        lineCount++;

                        var line = reader.ReadLine();
                        progressTotal += line.Length + 1;

                        if (line.Length >= 3 && line.Length < 70) lookups.Add(line.ToLower());

                        //Update the percentage
                        WriteProgress("Loading names", progressTotal, size);
                    }
                }
            }


            size = GetFileEntriesSize(fileEntries);
            progressTotal = 0L;
            lineCount = 0;

            foreach (var filePath in fileEntries)
            {
                //Create a version based on the file size, so that the hash and dict are bound together
                var fileInfo = new FileInfo(filePath);
                var version = GetSerial(fileInfo, "s");

                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var filePathName = $"{currentDirectory}\\{fileName}";
                _outputHashPath = $"{filePathName}.{version}.hash";
                _outputWordPath = $"{filePathName}.{version}.word";

                //Check that there are no output files
                if (!CheckForFiles(new string[] { _outputHashPath, _outputWordPath }))
                {
                    WriteHighlight($"Skipping {filePathName}.");

                    progressTotal += fileInfo.Length;
                    continue;
                }

                var inputs = new List<string>();

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

                            inputs.Add($"{emailStem}:{splits[1]}");
                        }

                        lineCount++;
                        progressTotal += line.Length;

                        //Update the percentage
                        WriteProgress("Processing files", progressTotal, size);
                    }
                }

                var itemCount = (inputs.Count() / TaskCount) + 1;
                var splitLists = SplitList<string>(inputs, itemCount);
                var tasks = new List<Task>();

                StartProgress(inputs.Count());

                foreach (var splitList in splitLists)
                {
                    tasks.Add(Task.Run(() => DoStem(lookups, splitList)));
                }

                progressTotal = 0;

                WriteProgress("Running stem tasks", progressTotal, TaskCount);

                //Wait for tasks to complate
                while (tasks.Count > 0)
                {
                    var task = await Task.WhenAny(tasks);
                    tasks.Remove(task);
                }
            }

            WriteMessage($"Completed at {DateTime.Now.ToShortTimeString()}.");            
        }

        private static Task DoStem(HashSet<string> lookups, List<string> lines)
        {
            var hashes = new List<string>();
            var dicts = new List<string>();
            var count = 0;

            foreach (var line in lines)
            {
                var splits = line.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                count++;

                if (splits.Length == 2) //Will have to do some other option for more splits here
                {
                    var email = splits[0].ToLower();
                    var subsplits = email.Split('@');
                    var name = subsplits[0];
                    var finals = new HashSet<string>();

                    //Try split on . etc
                    var names = name.Split(new char[] { '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);

                    //Add splits by token 
                    foreach (var subname in names)
                    {
                        //Rule out initials
                        if (subname.Length > 1 && subname.Length < 70)
                        {
                            finals.Add(subname);
                            finals.Add(subname.ToLower());
                        }
                    }

                    //Remove any special characters and numbers at the end
                    //Regex expression is cached
                    var match = Regex.Match(name, "^([a-z]*)", RegexOptions.IgnoreCase);
                    if (match.Success) name = match.Groups[1].Value;

                    //Add more splits by lookup
                    foreach (var entry in lookups)
                    {
                        if (name.StartsWith(entry))
                        {
                            finals.Add(entry);
                            finals.Add(entry.ToLower());

                            var sub = name.Substring(entry.Length);
                            finals.Add(sub);
                            finals.Add(sub.ToLower());
                        }
                    }

                    foreach (var final in finals)
                    {
                        hashes.Add(splits[1]);
                        dicts.Add(final);
                    }                    
                }

                //Update progress every x lines
                if (count == 1000)
                {
                    count = 0;
                    AddToProgress("Running stem tasks", 1000);
                }
            }

            if (hashes.Count > 0)
            {
                lock (_lock)
                {
                    File.AppendAllLines(_outputHashPath, hashes);
                    File.AppendAllLines(_outputWordPath, dicts);
                }
            }

            return Task.CompletedTask;
        }
    }
}
