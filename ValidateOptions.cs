using CommandLine;

namespace Metacrack
{
    [Verb("validate", HelpText = "Creates a hash list with a mapped associated wordlist.")]
    public class ValidateOptions
    {
        [Value(0, Required = true, MetaName = "InputPath", HelpText = "The path to the file(s) containing the input values.")]
        public string InputPath { get; set; }

        [Option("hash", Default = -1)]
        public int Hash { get; set; }

        [Option("iterations")]
        public int Iterations { get; set; }

        [Option("column", Default = 1)]
        public int Column { get; set; }

        [Option("email")]
        public bool ValidateEmail {  get; set; }

        [Option("email-only")]
        public bool ValidateEmailOnly { get; set; }
    }
}
