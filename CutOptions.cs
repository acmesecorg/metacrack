using CommandLine;

namespace Malfoy
{
    [Verb("cut", HelpText = "Ranks a list of hash:plain text but the occurence of plain values.")]
    public class CutOptions
    {
        [Value(0, Required = true, HelpText = "The path to the file(s) containing the data to be cut")]
        public string InputPath { get; set; }

        [Value(1, Required = true, HelpText = "The path to the output file.")]
        public string OutputPath { get; set; }

        [Value(2, Required = true, HelpText = "Starting line inclusive.")]
        public long Start { get; set; }

        [Value(3, Required = true, HelpText = "Ending line inclusive")]
        public long End { get; set; }

    }
}
