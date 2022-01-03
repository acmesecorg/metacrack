using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Metacrack.Plugins
{
    [Verb("catalog", HelpText = "Add contents of files to a catalog.")]
    public class CatalogOptions
    {
        [Value(0, Required = true, MetaName = "InputPath", HelpText = "The input path and subfolders to process files from.")]
        public string InputPath { get; set; }

        [Value(1, Required = false, Default = "meta.db", MetaName = "OutputPath", HelpText = "The output path to the new or existing database file.")]
        public string OutputPath { get; set; }

        [Option('t', "tokenize", HelpText = "Turns space seperated text into sub values.")]
        public bool Tokenize { get; set; }

        [Option("stem-email", HelpText = "Use email as a source of values.")]
        public bool StemEmail { get; set; }

        [Option("stem-email-only", HelpText = "Use email as a source of values only.")]
        public bool StemEmailOnly { get; set; }

        [Option('n', "names", HelpText = "Path to the file containing names to use in email stemming process when using *steam-email* or *stem-email-only* options.")]
        public string NamesPath { get; set; }

        [Option('c', "columns", HelpText = "The ordinals (positions) in the input file to map to fields where email is always at position 0.")]
        public IEnumerable<string> Columns { get; set; }

        [Option('f', "fields", HelpText = "The predefined fields to write values to. Each column should have a matching field. Valid values are: " +
            "p password u username n name d date i number v value.")]
        public IEnumerable<string> Fields { get; set; }
    }
}
