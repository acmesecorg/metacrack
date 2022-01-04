using System;
using System.IO;
using System.Text;

namespace Metacrack
{
    public class SessionException: Exception
    {
        public SessionException(string message):base(message) { }
    }

    public class Session: IDisposable
    {
        private bool _isDisposing;
        private int _session;
        private string _filename;
        private int _part;
        private int _partSize;
        private long _partCurrent;

        public StreamWriter HashStream { get; private set; }
        public StreamWriter WordStream { get; private set; }

        public Session(string filename, int session, int partSize)
        {
            //Filename and session are fixed
            _filename = filename;
            _session = session;

            //Part increases when partCurrent exceeeds partSize (when partSize > 0)
            _partSize = partSize;
            if (_partSize > 0) _part = 1;
        }

        public void AddingLines(int count)
        {
            //Only create files when we first add lines
            if (HashStream == null) NextFile();

            //Check if we need to manage parts
            if (_partSize > 0)
            {
                _partCurrent += count;
                if (_partCurrent > _partSize)
                {
                    _partCurrent = 0;
                    _part++;

                    NextFile();
                }
            }
        }

        public void Dispose()
        {
            if (!_isDisposing)
            {
                _isDisposing = true;
                HashStream?.Dispose();
                WordStream?.Dispose();
            }
        }

        private void NextFile()
        {
            //Save any current values
            HashStream?.Dispose();
            WordStream?.Dispose();

            //Build up new filename
            var builder = new StringBuilder();

            builder.Append(_filename);

            if (_session > 0)
            {
                builder.Append(".session");
                builder.Append(_session);
            }

            if (_part > 0)
            {
                builder.Append(".part");
                builder.Append(_part);
            }

            var fullFilename = builder.ToString();
            var currentDirectory = Directory.GetCurrentDirectory();

            var hashPath = Path.Combine(currentDirectory, $"{fullFilename}.hash");
            var wordPath = Path.Combine(currentDirectory, $"{fullFilename}.word");

            if (File.Exists(hashPath)) throw new SessionException($"File already exists: {$"{fullFilename}.hash"}");
            if (File.Exists(wordPath)) throw new SessionException($"File already exists: {$"{fullFilename}.word"}");

            HashStream = new StreamWriter(hashPath, true, Encoding.UTF8, 65536);
            WordStream = new StreamWriter(wordPath, true, Encoding.UTF8, 65536);
        }
    }
}
