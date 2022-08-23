using CommandLine;

namespace Metacrack.Plugins
{
    //This is the default plugin
    [Verb("export", false, HelpText = "Creates a new list of founds and lefts from a list of hashes and solved hashes.")]
    public class ExportOptions
    {
        [Value(0, Required = true, MetaName = "HashesPath", HelpText = "The file containing the original hashes.")]
        public string HashesPath { get; set; }

        [Value(1, Required = true, MetaName = "OutputPath", HelpText = "The file containing the hashcat output.")]
        public string OutputPath { get; set; }

        [Option("ignore-salt", HelpText = "Do not compare salts when matching hashes. Default is false.")]
        public bool IgnoreSalt { get; set; }

        [Option]
        public bool Base64 { get; set; }

        [Option("remove-hash", Default = "", HelpText = "The path to the file containing hashes to be removed.")]
        public string RemoveHashesPath { get; set; }

        [Option("remove-word", Default = "", HelpText = "The path to the file containing associated words to be removed. Use in conjunction with --remove.")]
        public string RemoveWordsPath { get; set; }

        [Option("shuck", Default = "", HelpText = "The path to the word file that will be used to calculate shuck pairs to convert founds back to plains.")]
        public string ShuckPath { get; set; }

    }
}
