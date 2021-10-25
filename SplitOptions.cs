using CommandLine;

namespace Metacrack
{
    [Verb("split", HelpText = "Creates a hash list with a mapped associated wordlist.")]
    public class SplitOptions
    {
        [Value(0, Required = true, MetaName = "InputPath", HelpText = "The path to the file(s) to be split.")]
        public string InputPath { get; set; }

        [Value(1, Required = true, MetaName = "Count", HelpText = "The number of lines each file should contain.")]
        public string CountString { get; set; }
    }
}
