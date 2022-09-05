using CommandLine;

namespace Metacrack
{
    [Verb("map", HelpText = "Creates a hash list with a mapped associated wordlist.")]
    public class MapOptions
    {
        [Value(0, Required = true, MetaName = "InputPath", HelpText = "The path to the file(s) containing the input emails.")]
        public string InputPath { get; set; }

        [Value(1, Required = false, MetaName = "MapPath", Default ="", HelpText = "The path to the file(s) containing the items to map. Leave empty for a blank wordlist.")]
        public string MapPath { get; set; }

        [Option("hash", Default = -1)]
        public int Hash { get; set; }

        [Option("limit")]
        public int Limit { get; set; }

        [Option("no-email")]
        public bool NoEmail { get; set; }
    }
}
