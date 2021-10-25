using CommandLine;

namespace Metacrack
{
    [Verb("rank", HelpText = "Ranks a list of hash:plain text by the occurence of plain values.")]
    public class RankOptions
    {
        [Value(0, Required = true, HelpText = "The path to the file(s) containing the hash:plains.")]
        public string InputPath { get; set; }

        [Option("Count", Default = 10)]
        public int Count { get; set; }
    }
}
