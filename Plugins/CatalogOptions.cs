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
        public static readonly string[] ValidFields = { "p", "password", "u", "username", "n", "name", "d", "date", "i", "number", "v", "value" };

        [Value(0, Required = true, MetaName = "InputPath", HelpText = "The input path and subfolders to process files from.")]
        public string InputPath { get; set; }

        [Value(1, Required = false, Default = "meta.db", MetaName = "OutputPath", HelpText = "The output path to the new or existing database file.")]
        public string OutputPath { get; set; }

        [Option("tokenize", HelpText = "Attempts to turn each space seperated value into further sub values.")]
        public bool Tokenize { get; set; }

        [Option("stem-email", HelpText = "Use email as a source of values.")]
        public bool StemEmail { get; set; }

        [Option("stem-email-only", HelpText = "Use email as a source of values only.")]
        public bool StemEmailOnly { get; set; }

        [Option("stem-names")]
        public string NamesPath { get; set; }

        [Option('c', "columns")]
        public IEnumerable<string> Columns { get; set; }

        [Option('f', "fields")]
        public IEnumerable<string> Fields { get; set; }
    }
}
