using CommandLine;

namespace Metacrack
{
    [Verb("parse", HelpText = "Parses a file by the type provided into new parsed and unparsed files.")]
    public class ParseOptions
    {
        [Value(0, Required = true, MetaName = "InputPath", HelpText = "The path to the file(s) containing the hash:plains.")]
        public string InputPath { get; set; }

        [Option("type", Default = "delimited")]
        public string ParseType { get; set; }

        [Option("delimiter", Default = ",")]
        public string Delimiter { get; set; }

        [Option("columns")]
        public IEnumerable<string> Columns { get; set; }

        [Option("date-columns")]
        public IEnumerable<string> DateColumns { get; set; }

        [Option("names")]
        public IEnumerable<string> Names { get; set; }

        [Option("deduplicate")]
        public bool Deduplicate { get; set; }

    }
}
