using CommandLine;

namespace Metacrack
{
    [Verb("sort", HelpText = "Sorts and optionally deduplicates a file.")]
    public class SortOptions
    {
        [Value(0, Required = true, MetaName = "InputPath", HelpText = "The path to the file(s) containing the data to be sorted")]
        public string InputPath { get; set; }

        [Option("deduplicate", HelpText = "Deduplicates the file")]
        public bool Deduplicate { get; set; }

    }
}
