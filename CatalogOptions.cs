using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Malfoy
{
    [Verb("catalog", HelpText = "Add contents of files to a data catalog.")]
    public class CatalogOptions
    {        
        [Value(0, Required = true, HelpText = "The input folder and subfolders to process .txt files from.")]
        public string InputFolder { get; set; }

        [Value(1, Required = true, HelpText = "The output folder to append data to or create new files in.")]
        public string OutputFolder { get; set; }

        [Option("prefix", Default = "Password")]
        public string Prefix { get; set;}

        [Option("optimize", HelpText = "Removes duplicates and sorts each data file after data is added.")]
        public bool Optimize { get; set; }

        [Option("tokenize", HelpText = "Attempts to turn each value into further sub values.")]
        public bool Tokenize { get; set; }

        [Option]
        public IEnumerable<string> Columns { get; set; }
    }
}
