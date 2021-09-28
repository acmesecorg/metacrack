using CommandLine;

namespace Malfoy
{
    [Verb("blank", HelpText = "Creates a hash list with a blank associated wordlist.")]
    public class BlankOptions
    {
        [Value(0, Required = true, HelpText = "The path to the file(s) containing the input emails.")]
        public string InputPath { get; set; }

        [Option("hash")]
        public int Hash { get; set; }
    }
}
