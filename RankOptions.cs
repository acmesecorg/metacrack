using CommandLine;

namespace Metacrack
{
    [Verb("rank", HelpText = "Ranks a list of hash:plain text by the occurence of plain values.")]
    public class RankOptions
    {
        [Value(0, Required = true, MetaName = "InputPath", HelpText = "The path to the file(s) containing the hash:plains.")]
        public string InputPath { get; set; }

        [Value(1, Required = false, MetaName = "OutputPath", HelpText = "The file to  write output to.")]
        public string OutputPath { get; set; }

        [Option("count", Default = "10")]
        public string Count { get; set; }

        [Option("keep", Default = "10")]
        public string Keep { get; set; }

        [Option("debug-mode")]
        public int DebugMode { get; set; }

        [Option("rule", HelpText = "Output as a rule file")]
        public bool Rules { get; set; }
    }
}
