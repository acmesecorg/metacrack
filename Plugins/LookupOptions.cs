using CommandLine;

namespace Metacrack.Plugins
{
    [Verb("lookup", HelpText = "Lookup meta data for a list of email:hash pairs.")]
    public class LookupOptions
    {        
        [Value(0, Required = true, MetaName = "InputPath", HelpText = "The input path to process .txt files from.")]
        public string InputPath { get; set; }

        [Value(1, Required = false, Default = "meta.db", MetaName = "CatalogPath", HelpText = "The path to the source catalog to lookup data in.")]
        public string CatalogPath { get; set; }

        [Option('m', "hash-type", Default = -1)]
        public int HashType { get; set; }

        [Option('r', "rule", HelpText = "Removes duplicate words for a hash based on the rule provided")]
        public string RulePath { get; set; }

        [Option('f', "fields", HelpText = "The predefined fields to read values from. Valid values are: p password u username n name d date i number v value.")]
        public IEnumerable<string> Fields { get; set; }

        [Option('p', "part", Default = "0", HelpText = "Sets the number of lines in each part, if specified. Use suffix k to denote 000.")]
        public string Part { get; set; }

        [Option('s', "sessions", HelpText = "Sets the number of sessions. Guesses for a hash are each placed in a seperated file.")]
        public int Sessions { get; set; }

        [Option("hash-maximum", Default=20, HelpText = "Sets the maximum number of words per hash.")]
        public int HashMaximum { get; set; }


        //TODO: Option to change part size
        //TODO: Add options to derive passwords from numbers and dates
    }
}
