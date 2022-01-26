using CommandLine;

namespace Metacrack
{
    //This is the default plugin
    [Verb("export", false, HelpText = "Creates a new list of founds and lefts from a list of hashes and solved hashes.")]
    public class ExportOptions
    {        
        [Value(0, Required = true, MetaName = "HashesPath", HelpText = "The file containing the original hashes.")]
        public string HashesPath { get; set; }

        [Value(1, Required = true, MetaName = "LookupPath", HelpText = "The search pattern for the hash lookup files.")]
        public string LookupPath { get; set; }

        [Option('f', "found")]
        public bool FoundMode { get; set; }

        [Option]
        public bool NoSalt { get; set; }

        [Option]
        public bool IgnoreSalt { get; set; }

        [Option]
        public bool Base64 { get; set; }

        [Option("remove", Default = "", HelpText = "The path to the file containing hashes to be removed.")]
        public string RemoveHashesPath { get; set; }

        [Option("remove-associated", Default = "", HelpText = "The path to the file containing associated words to be removed. Use in conjunction with --remove.")]
        public string RemoveWordsPath { get; set; }

        [Option("shuck", Default = "", HelpText = "The path to the word file that will be used to calculate shuck pairs to convert founds back to plains.")]
        public string ShuckPath { get; set; }

    }
}
