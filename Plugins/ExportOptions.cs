using CommandLine;

namespace Metacrack
{
    //This is the default plugin
    [Verb("export", false, HelpText = "Creates a new list of founds, plains and lefts from a list of hashes and hashcat output.")]
    public class ExportOptions
    {        
        [Value(0, Required = true, MetaName = "HashesPath", HelpText = "The file containing the original hashes.")]
        public string HashesPath { get; set; }

        [Value(1, Required = true, MetaName = "LookupPath", HelpText = "Path to one or more hashcat output files.")]
        public string LookupPath { get; set; }

        [Option]
        public bool NoSalt { get; set; }

        [Option]
        public bool IgnoreSalt { get; set; }

        [Option("remove-hash", Default = "", HelpText = "The path to the file containing hashes to be removed.")]
        public string RemoveHashesPath { get; set; }

        [Option("remove-word", Default = "", HelpText = "The path to the file containing associated words to be removed. Use in conjunction with --remove.")]
        public string RemoveWordsPath { get; set; }
    }
}
