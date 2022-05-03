using CommandLine;

namespace Metacrack
{
    [Verb("hash", HelpText = "Hashes the contents of a file line by line.")]
    public class HashOptions
    {
        [Value(0, Required = true, MetaName = "InputPath", HelpText = "The path to the file(s) to be split.")]
        public string InputPath { get; set; }

        [Option("hash", Default = 0)]
        public int Hash { get; set; }
    }
}
