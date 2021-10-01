using CommandLine;

namespace Malfoy
{
    [Verb("parse", HelpText = "Ranks a list of hash:plain text but the occurence of plain values.")]
    public class ParseOptions
    {
        [Value(0, Required = true, HelpText = "The path to the file(s) containing the hash:plains.")]
        public string InputPath { get; set; }

        [Option("Type", Default = 0)]
        public int ParseType { get; set; }

        [Option("deduplicate")]
        public bool Deduplicate { get; set; }

    }
}
