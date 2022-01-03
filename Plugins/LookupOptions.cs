using CommandLine;
using System.Collections.Generic;

namespace Metacrack
{
    [Verb("lookup", HelpText = "Lookup meta data for a list of keys.")]
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

        [Option('s', "use-sessions", HelpText = "Enables the sessions feature. Guesses for a hash are each placed in a seperated file.")]
        public bool UseSessions { get; set; }

        [Option("max-sessions", Default = PluginBase.MaxSessionsDefault)]
        public int MaxSessions { get; set; }


        //TODO: Option to change part size
        //TODO: Add options to derive passwords from numbers and dates
    }
}
