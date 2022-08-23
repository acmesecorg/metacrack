﻿using CommandLine;

namespace Metacrack.Plugins
{
    [Verb("sql", HelpText = "Parses a sql file into credential text file.")]
    public class SqlOptions
    {
        [Value(0, Required = true, MetaName = "InputPath", HelpText = "The input path to the .sql file(s).")]
        public string InputPath { get; set; }

        [Option("table", Default = "users")]
        public string Table { get; set; }

        [Option]
        public IEnumerable<string> Columns { get; set; }

        [Option("column-names")]
        public IEnumerable<string> ColumnNames { get; set; }

        [Option("meta")]
        public IEnumerable<string> MetaColumns { get; set; }

        [Option("meta-column-names")]
        public IEnumerable<string> MetaNames { get; set; }

        //Give options to only parse part of the file we are interested in and avoid bugs that may arise
        [Option("start", HelpText = "Starting line inclusive.")]
        public long Start { get; set; }

        [Option("end", HelpText = "Ending line inclusive")]
        public long End { get; set; }

        [Option("debug", HelpText = "Shows parse information on screen without processing file further.")]
        public bool Debug { get; set; }

        [Option("modes", HelpText = "Modifies processing depending on the mode chosen. (experimental)")]
        public IEnumerable<string> Modes { get; set; }
    }
}
