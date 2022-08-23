using CommandLine;

namespace Metacrack.Plugins
{
    [Verb("cut", HelpText = "Outputs a new file by cutting from the start to the end line numbers provided.")]
    public class CutOptions
    {
        [Value(0, Required = true, MetaName = "InputPath", HelpText = "The path to the file(s) containing the data to be cut")]
        public string InputPath { get; set; }

        [Value(1, Required = true, MetaName = "OutputPath", HelpText = "The path to the output file.")]
        public string OutputPath { get; set; }

        [Value(2, Required = true, MetaName = "Start", HelpText = "Starting line inclusive.")]
        public string Start { get; set; }

        [Value(3, Required = true, MetaName = "End", HelpText = "Ending line inclusive")]
        public string End { get; set; }

    }
}
