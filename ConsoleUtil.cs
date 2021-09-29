using System.Text;

namespace Malfoy
{
    public static class ConsoleUtil
    {
        private static string _lastProgressText = "";
        private static string _lastProgressPercent = "";

        public static void WriteMessage(string value, ConsoleColor color)
        {
            if (_lastProgressText != "") CancelProgress();

            Console.ForegroundColor = color;
            Console.WriteLine(value);
            Console.ResetColor();
        }

        public static void WriteMessage(string value)
        {
            if (_lastProgressText != "") CancelProgress();

            Console.WriteLine(value);
        }

        public static void WriteProgress(string text, long progress, long total)
        {
            WriteProgress(text, (int)((double)progress / total * 100));
        }

        public static void WriteProgress(string text, int percent)
        {
            if (text == null) throw new ArgumentNullException("text");

            Console.CursorVisible = false;

            //Validate the text stays on one line
            if (text.Length + 5 > Console.WindowWidth) text = text.Substring(0, Console.WindowWidth - 5);

            //Check if this is the first time we are writing this text
            if (text != _lastProgressText)
            {
                //Reset the cursor and overwrite with spaces
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write(new string(' ', _lastProgressText.Length + _lastProgressPercent.Length));
                Console.SetCursorPosition(0, Console.CursorTop);

                //Write new text
                Console.Write(text);

                _lastProgressPercent = "";
            }

            //Check if progress text has changed
            var percentString = $" ({percent}%)";
            if (percentString != _lastProgressPercent)
            {
                //Remove old percent e.g. message (12%) with backspace characters
                var builder = new StringBuilder();

                builder.Append('\b', _lastProgressPercent.Length);
                builder.Append(percentString);

                //Write new percent value
                Console.Write(builder.ToString());
            }

            _lastProgressText = text;
            _lastProgressPercent = percentString;            
        }

        public static void CancelProgress()
        {
            Console.Write(new string('\b',Console.CursorLeft));
            Console.SetCursorPosition(0, Console.CursorTop);

            _lastProgressText = "";
            _lastProgressPercent = "";

            Console.CursorVisible = true;
        }
    }
}
