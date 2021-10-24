using CommandLine;

namespace Metacrack
{
    [Verb("sort", HelpText = "Ranks a list of hash:plain text but the occurence of plain values.")]
    public class SortOptions
    {
        [Value(0, Required = true, HelpText = "The path to the file(s) containing the data to be sorted")]
        public string InputPath { get; set; }

        [Option("deduplicate", HelpText = "Deduplicates the file")]
        public bool Deduplicate { get; set; }

    }
}
