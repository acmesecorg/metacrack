using CommandLine;

namespace Malfoy
{
    [Verb("parse", HelpText = "Ranks a list of hash:plain text but the occurence of plain values.")]
    public class ParseOptions
    {
        [Value(0, Required = true, HelpText = "The path to the file(s) containing the hash:plains.")]
        public string InputPath { get; set; }

        [Option("type", Default = "delimited")]
        public string ParseType { get; set; }

        [Option("delimiter", Default = ",")]
        public string Delimiter { get; set; }

        [Option("columns")]
        public IEnumerable<string> Columns { get; set; }

        [Option("names")]
        public IEnumerable<string> Names { get; set; }

        [Option("deduplicate")]
        public bool Deduplicate { get; set; }

    }
}
