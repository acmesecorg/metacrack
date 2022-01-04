using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Metacrack
{
    internal class SessionException: Exception
    {
        public SessionException(string message):base(message) { }
    }

    internal class Session: IDisposable
    {
        private bool _isDisposing;

        public int Index { get; set; }
        public int Part { get; set; }
        public long Current { get; set; }

        public StreamWriter HashStream { get; set; }
        public StreamWriter WordStream { get; set; }

        public Session(int index)
        {
            Index = index;
        }

        public Session(int index, int part)
        {
            Index = index;
            Part = part;
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
    }

    public class SessionManager: IDisposable
    {
        private string _filename;        
        private int _partSize;

        private Session _session;
        private List<Session> _sessions;

        public SessionManager(string filename, int sessions, int partSize)
        {
            //Filename and session are fixed
            _filename = filename;

            //Part increases when partCurrent exceeds partSize (when partSize > 0)
            _partSize = partSize;

            if (sessions == 0)
            {
                _session = new Session(0, (partSize == 0) ? 0 : 1 );
            }
            else
            {
                _sessions = new List<Session>();
                for (var i=1; i<= sessions; i++) _sessions.Add(new Session(i, (partSize == 0) ? 0 : 1));
            }
        }

        public void AddWords(ReadOnlySpan<char> hash, List<string> words)
        {
            //If we just have one default session
            if (_session != null)
            {
                AddLines(_session, words.Count);

                foreach (var word in words)
                {
                    _session.HashStream.WriteLine(hash);
                    _session.WordStream.WriteLine(word);
                }

                return;
            }

            //If we are writing the words across sessions
            var count = 0;
            var session = _sessions[0];

            foreach (var word in words)
            {
                AddLines(session, 1);

                session.HashStream.WriteLine(hash);
                session.WordStream.WriteLine(word);

                //Write any remaining into the last session
                count++;
                if (count < _sessions.Count) session = _sessions[count];
            }
        }

        public void Dispose()
        {
            _session?.Dispose();
            if (_sessions != null)
            {
                foreach (var session in _sessions) session.Dispose();
            }
        }

        private void AddLines(Session session, int count)
        {
            //Only create files when we first add lines
            if (session.HashStream == null) NextFile(session);

            //Check if we need to manage parts
            if (_partSize > 0)
            {
                session.Current += count;
                if (session.Current > _partSize)
                {
                    session.Current = 0;
                    session.Part++;

                    NextFile(session);
                }
            }
        }

        private void NextFile(Session session)
        {
            //Save any current values
            session.HashStream?.Dispose();
            session.WordStream?.Dispose();

            //Build up new filename
            var builder = new StringBuilder();

            builder.Append(_filename);

            if (session.Index > 0)
            {
                builder.Append(".session");
                builder.Append(session.Index);
            }

            if (session.Part > 0)
            {
                builder.Append(".part");
                builder.Append(session.Part);
            }

            var fullFilename = builder.ToString();
            var currentDirectory = Directory.GetCurrentDirectory();

            var hashPath = Path.Combine(currentDirectory, $"{fullFilename}.hash");
            var wordPath = Path.Combine(currentDirectory, $"{fullFilename}.word");

            if (File.Exists(hashPath)) throw new SessionException($"File already exists: {$"{fullFilename}.hash"}");
            if (File.Exists(wordPath)) throw new SessionException($"File already exists: {$"{fullFilename}.word"}");

            session.HashStream = new StreamWriter(hashPath, true, Encoding.UTF8, 65536);
            session.WordStream = new StreamWriter(wordPath, true, Encoding.UTF8, 65536);
        }
    }
}
