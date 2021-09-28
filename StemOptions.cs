using CommandLine;

namespace Malfoy
{
    [Verb("stem", HelpText = "Creates an associated hash and wordlist by stemming user email information.")]
    public class StemOptions
    {        
        [Value(0, Required = true, HelpText = "The path to the file(s) containing the input emails.")]
        public string InputPath { get; set; }

        [Option('n', "names")]
        public string NamesPath { get; set; }

        [Option("hash")]
        public int Hash { get; set; }
    }
}
