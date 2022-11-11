using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Metacrack
{
    [Verb("catalog", HelpText = "Add contents of files to a data catalog.")]
    public class CatalogOptions
    {        
        [Value(0, Required = true, MetaName = "InputPath", HelpText = "The input path and subfolders to process files from.")]
        public string InputPath { get; set; }

        [Value(1, Required = true, MetaName = "OutputFolder", HelpText = "The output folder to append data to or create new files in.")]
        public string OutputFolder { get; set; }

        [Option("prefix", Default = "Password")]
        public string Prefix { get; set;}

        [Option("no-optimize", HelpText = "Prevents optimisation from running after data is added.")]
        public bool NoOptimize { get; set; }

        [Option("tokenize", HelpText = "Attempts to turn each value into further sub values.")]
        public bool Tokenize { get; set; }

        [Option("stem-email", HelpText = "Use email as a source of values.")]
        public bool StemEmail { get; set; }

        [Option("stem-domain", HelpText = "Use email domain as a source of values.")]
        public bool StemDomain { get; set; }

        [Option("stem-email-only", HelpText = "Use email as a source of values only.")]
        public bool StemEmailOnly { get; set; }

        [Option("email-only", HelpText = "Use email exactly as a value only.")]
        public bool EmailOnly { get; set; }

        [Option("stem-names")]
        public string NamesPath { get; set; }

        [Option]
        public IEnumerable<string> Columns { get; set; }

        [Option("xref", HelpText = "Calculate x-references.")]
        public bool XReference { get; set; }

        [Option("xref-only", HelpText = "Skip other processing and only calculate x references in output folder.")]
        public bool XReferenceOnly { get; set; }
    }
}
