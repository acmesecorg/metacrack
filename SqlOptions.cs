using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Metacrack
{
    [Verb("sql", HelpText = "Parses a sql file into credential text file.")]
    public class SqlOptions
    {
        [Option("table", Default = "`users`")]
        public string Table { get; set; }

        [Option]
        public IEnumerable<string> Columns { get; set; }

        [Option("meta")]
        public IEnumerable<string> MetaColumns { get; set; }

        //Give options to only parse part of the file we are interested in and avoid bugs that may arise
        [Option("Start", HelpText = "Starting line inclusive.")]
        public long Start { get; set; }

        [Option("End", HelpText = "Ending line inclusive")]
        public long End { get; set; }

        [Option("debug", HelpText = "Shows parse information on screen without processing file further.")]
        public bool Debug { get; set; }

        [Value(0, Required = true, MetaName = "InputPath", HelpText = "The input path to the .sql file(s).")]
        public string InputPath { get; set; }
    }
}
