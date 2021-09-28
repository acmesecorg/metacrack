using CommandLine;
using System.Collections.Generic;

namespace Malfoy
{
    [Verb("lookup", HelpText = "Lookup meta data for a list of keys.")]
    public class LookupOptions
    {        
        [Value(0, Required = true, HelpText = "The input path to process .txt files from.")]
        public string InputPath { get; set; }

        [Value(1, Required = true, HelpText = "The source catalog to lookup data in.")]
        public string SourceFolder { get; set; }

        [Option("prefix", Default = "Password")]
        public string Prefix { get; set;}

        [Option("hash")]
        public int Hash { get; set; }

        [Option("tokenize", HelpText = "Attempts to turn each value into further sub values.")]
        public bool Tokenize { get; set; }
    }
}
