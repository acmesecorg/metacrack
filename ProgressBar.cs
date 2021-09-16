using System;
using System.Text;
using System.Threading;

namespace Malfoy
{
    /// <summary>
    /// An ASCII progress bar
    /// </summary>
    public class ProgressBar : IDisposable
    {
        private const int _blockCount = 10;
        private readonly TimeSpan _animationInterval = TimeSpan.FromSeconds(1.0 / 4);
        private const string _animation = @"|/-\";

        private readonly Timer _timer;

        private double _currentProgress = 0;
        private string _currentText = string.Empty;
        private bool _disposed = false;
        private int _animationIndex = 0;
        private bool _paused;

        public ProgressBar(bool isOutputRedirected)
        {
            _timer = new Timer(TimerHandler);

            // A progress bar is only for temporary display in a console window.
            // If the console output is redirected to a file, draw nothing.
            // Otherwise, we'll end up with a lot of garbage in the target file.
            if (!isOutputRedirected)
            {
                ResetTimer();
            }
        }

        public void Report(double value)
        {
            try
            {
                // Make sure value is in [0..1] range
                value = Math.Max(0, Math.Min(1, value));
                Interlocked.Exchange(ref _currentProgress, value);
            }
            catch
            {

            }
        }

        public void Pause()
        {
            UpdateText("");
            _paused = true;
        }

        public void Resume()
        {
            _paused = false;
        }

        public void WriteLine(string text)
        {
            UpdateText("");
            _paused = true;
            Console.WriteLine(text);
            _paused = false;
        }

        private void TimerHandler(object state)
        {
            if (_disposed || _paused) return;

            lock (_timer)
            {
                int progressBlockCount = (int)(_currentProgress * _blockCount);
                int percent = (int)(_currentProgress * 100);
                string text = string.Format("[{0}{1}] {2,3}% {3}",
                    new string('#', progressBlockCount), new string('-', _blockCount - progressBlockCount),
                    percent,
                    _animation[_animationIndex++ % _animation.Length]);

                UpdateText(text);
                ResetTimer();
            }
        }

        public void UpdateText(string text)
        {
            try
            {
                // Get length of common portion
                int commonPrefixLength = 0;
                int commonLength = Math.Min(_currentText.Length, text.Length);
                while (commonPrefixLength < commonLength && text[commonPrefixLength] == _currentText[commonPrefixLength])
                {
                    commonPrefixLength++;
                }

                // Backtrack to the first differing character
                StringBuilder outputBuilder = new StringBuilder();
                outputBuilder.Append('\b', _currentText.Length - commonPrefixLength);

                // Output new suffix
                outputBuilder.Append(text.Substring(commonPrefixLength));

                // If the new text is shorter than the old one: delete overlapping characters
                int overlapCount = _currentText.Length - text.Length;
                if (overlapCount > 0)
                {
                    outputBuilder.Append(' ', overlapCount);
                    outputBuilder.Append('\b', overlapCount);
                }

                Console.Write(outputBuilder);
                _currentText = text;
            }
            catch
            {

            }
        }

        private void ResetTimer()
        {
            if (!_disposed) _timer.Change(_animationInterval, TimeSpan.FromMilliseconds(-1));
        }

        public void Dispose()
        {
            if (_disposed) return;

            lock (_timer)
            {
                UpdateText(string.Empty);
                _timer.Dispose();

                _disposed = true;
            }
        }
    }
}
