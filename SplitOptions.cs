using CommandLine;

namespace Metacrack
{
    [Verb("split", HelpText = "Splits a file by line count into multiple files based on the value provided.")]
    public class SplitOptions
    {
        [Value(0, Required = true, MetaName = "InputPath", HelpText = "The path to the file(s) to be split.")]
        public string InputPath { get; set; }

        [Value(1, Required = true, MetaName = "Count", HelpText = "The number of lines each file should contain.")]
        public string CountString { get; set; }
    }
}
