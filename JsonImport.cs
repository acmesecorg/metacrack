using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Malfoy
{
    public static class JsonImport
    {
        public static void Process(string currentDirectory, string[] args)
        {
            var arg = args[0];

            //Get user hashes / json input path
            var jsonFileEntries = Directory.GetFiles(currentDirectory, arg);

            if (jsonFileEntries.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No Json input file(s) not found.");
                Console.ResetColor();
                return;
            }

            var size = Common.GetFileEntriesSize(jsonFileEntries);
            var progressTotal = 0L;
            var lineCount = 0L;

            var output = new List<string>();

            using (var progress = new ProgressBar(false))
            {
                foreach (var jsonPath in jsonFileEntries)
                {
                    var fileName = Path.GetFileNameWithoutExtension(jsonPath);
                    var filePathName = $"{currentDirectory}\\{fileName}";

                    var outputPath = $"{filePathName}-output.txt";

                    progress.UpdateText("");

                    //Check that there are no output files
                    if (!Common.CheckForFiles(new string[] { outputPath }))
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine($"Skipping {filePathName}.");
                        Console.ResetColor();

                        continue;
                    }

                    output.Clear();
                    lineCount = 0;

                    using (var reader = new StreamReader(jsonPath))
                    {
                        JObject jObject = null;

                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();

                            lineCount++;
                            progressTotal += line.Length;

                            try
                            {
                                jObject = JObject.Parse(line);
                            }
                            catch (Exception)
                            {
                                Console.ForegroundColor = ConsoleColor.DarkYellow;
                                Console.WriteLine($"Json parse exception for line {lineCount} in {fileName}. Skipping line.");
                                Console.ResetColor();

                                continue;
                            }

                            var email = "";
                            var hash = "";

                            if (jObject.ContainsKey("email")) email = jObject.Value<string>("email");
                            if (jObject.ContainsKey("Email")) email = jObject.Value<string>("Email");
                            if (jObject.ContainsKey("e")) email = jObject.Value<string>("e");
                            if (jObject.ContainsKey("password")) hash = jObject.Value<string>("password");
                            if (jObject.ContainsKey("Password")) hash = jObject.Value<string>("Password");
                            if (jObject.ContainsKey("h")) hash = jObject.Value<string>("h");

                            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(hash))
                            {
                                //Console.ForegroundColor = ConsoleColor.DarkYellow;
                                //Console.WriteLine($"Skipping line {line}.");
                                //Console.ResetColor();
                                continue;
                            }

                            //Lower if a hash
                            if (hash.ToLower() != hash && Common.IsCommonHash(hash)) hash = hash.ToLower();

                            output.Add($"{email.ToLower()}:{hash}");

                            //Update the percentage
                            progress.Report((double)progressTotal / size);
                        }
                    }

                    //Write out file
                    progress.WriteLine($"Writing {output.Count()} lines to {fileName}-output.txt");
                    File.AppendAllLines(outputPath, output);
                }
            }
        }
    }
}
