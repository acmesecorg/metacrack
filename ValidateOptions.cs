using CommandLine;

namespace Malfoy
{
    [Verb("validate", HelpText = "Creates a hash list with a mapped associated wordlist.")]
    public class ValidateOptions
    {
        [Value(0, Required = true, HelpText = "The path to the file(s) containing the input values.")]
        public string InputPath { get; set; }

        [Option("hash", Required = true)]
        public int Hash { get; set; }

        [Option("iterations")]
        public int Iterations { get; set; }

        [Option("Column", Default = 1)]
        public int Column { get; set; }
    }
}
