using CommandLine;

namespace Malfoy
{
    //This is the default plugin
    [Verb("export", true, HelpText = "Cretaes a new list of founds and lefts from a list of hashes and solved hashes.")]
    public class ExportOptions
    {        
        [Value(0, Required = true, HelpText = "The file containing the original hashes.")]
        public string HashesPath { get; set; }

        [Value(1, Required = true, HelpText = "The search pattern for the hash lookup files.")]
        public string LookupPath { get; set; }

        [Option('f', "found")]
        public bool FoundMode { get; set; }

        [Option]
        public bool NoSalt { get; set; }

        [Option]
        public bool IgnoreSalt { get; set; }

        [Option]
        public bool Base64 { get; set; }

        [Option("remove")]
        public string RemoveHashesPath { get; set; }

        [Option("remove-associated")]
        public string RemoveWordsPath { get; set; }
    }
}
