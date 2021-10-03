using CommandLine;

namespace Malfoy
{
    [Verb("split", HelpText = "Creates a hash list with a mapped associated wordlist.")]
    public class SplitOptions
    {
        [Value(0, Required = true, HelpText = "The path to the file(s) to be split.")]
        public string InputPath { get; set; }

        [Value(1, Required = true, HelpText = "The amount of lines each file will contain.")]
        public string CountString { get; set; }
    }
}
