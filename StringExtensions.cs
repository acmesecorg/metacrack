using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

//Supress SHA1Managed warning for performance reasons
#pragma warning disable SYSLIB0021 //Type or member is obsolete

public static class StringExtensions
{
    private static SHA1Managed _sha1;

    //Create any static objects in advance, since methods are designed to be called frequently
    static StringExtensions()
    {
        _sha1 = new SHA1Managed();
    }

    public static long ToRowId(this string value)
    {
        //md5 might be a better option, since the number of bytesin the hash aligns perfectly with the return type
        //But a managed md5 implementation is not available natively and sha1managed has been shown to be fast
        var bytes = _sha1.ComputeHash(Encoding.UTF8.GetBytes(value));
        return BitConverter.ToInt64(bytes, 0);
    }

    public static (long Id, char Char) ToRowCharId(this string value)
    {
        var bytes = _sha1.ComputeHash(Encoding.UTF8.GetBytes(value));
        return (BitConverter.ToInt64(bytes, 0), Convert.ToHexString(bytes,0,1)[0]);
    }

    public static string RemoveSpecialCharacters(this string str)
    {
        return RemoveSpecialCharacters(str.AsSpan()).ToString();
    }

    public static ReadOnlySpan<char> RemoveSpecialCharacters(this ReadOnlySpan<char> str)
    {
        Span<char> buffer = new char[str.Length];
        int idx = 0;

        foreach (char c in str)
        {
            if (char.IsLetterOrDigit(c))
            {
                buffer[idx] = c;
                idx++;
            }
        }

        return buffer.Slice(0, idx);
    }

    public static string MergeWith(this string existing, string additions)
    {
        if (string.IsNullOrEmpty(existing)) return additions;
        if (string.IsNullOrEmpty(additions)) return existing;

        return MergeWith(existing.AsSpan(), additions.AsSpan(), ':');
    }

    //Ensure empty checks are done further up the call stack for best performance
    //TODO: add limit checks so that eg we have max 20 values
    public static string MergeWith(this ReadOnlySpan<char> existing, ReadOnlySpan<char> additions, char seperator)
    {
        //Create maximum length
        Span<char> buffer = stackalloc char[existing.Length + additions.Length + 1];
        var position = existing.Length;

        //Copy existing values
        existing.CopyTo(buffer.Slice(0));

        //Loop through additions and see if they exist in existing, if not then add them
        foreach (ReadOnlySpan<char> value in additions.SplitByChar(seperator))
        {
            if (existing.IndexOf(value) == -1)
            {
                //Add seperator if there are existing values
                if (position > 0) buffer[position++] = seperator;

                //Move the position in current and add
                value.CopyTo(buffer.Slice(position));

                //Increase position by length of value just added
                position += value.Length;
            }
        }

        //Do an allocation from the buffer, since the buffer cannot be passed out of this function anyway
        return buffer.Slice(0, position).ToString();
    }

    //https://www.meziantou.net/split-a-string-into-lines-without-allocation.htm
    public static CharSplitEnumerator SplitByChar(this string str, char seperator)
    {
        // CharSplitEnumerator is a struct so there is no allocation here
        return new CharSplitEnumerator(str.AsSpan(), seperator);
    }

    public static CharSplitEnumerator SplitByChar(this ReadOnlySpan<char> str, char seperator)
    {
        // CharSplitEnumerator is a struct so there is no allocation here
        return new CharSplitEnumerator(str, seperator);
    }

    // Must be a ref struct as it contains a ReadOnlySpan<char>
    public ref struct CharSplitEnumerator
    {
        private ReadOnlySpan<char> _str;
        private char _seperator;
        private int _index;

        public CharSplitEntry Current { get; private set; }

        public CharSplitEnumerator(ReadOnlySpan<char> str, char seperator)
        {
            _str = str;
            _seperator = seperator;
            _index = 0;

            Current = default;
        }

        // Needed to be compatible with the foreach operator
        public CharSplitEnumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            var span = _str;
            if (span.Length == 0) return false; // Reach the end of the string

            var position = span.IndexOf(_seperator);
            if (position == -1) // The string is composed of only one item
            {
                _str = ReadOnlySpan<char>.Empty; // The remaining string is an empty string
                Current = new CharSplitEntry(span, _index);
                _index++;

                return true;
            }

            Current = new CharSplitEntry(span.Slice(0, position), _index);
            _index++;

            _str = span.Slice(position + 1);
            return true;
        }

        public int MoveToEnd()
        {
            while (MoveNext()) ;
            return _index;
        }
    }

    public readonly ref struct CharSplitEntry
    {
        public ReadOnlySpan<char> Value { get; }
        public int Index { get; }

        public CharSplitEntry(ReadOnlySpan<char> value, int index)
        {
            Value = value;
            Index = index;
        }

        // This method allow to deconstruct the type, so you can write any of the following code
        // foreach (var entry in str.SplitLines()) { _ = entry.Line; }
        // foreach (var (line, endOfLine) in str.SplitLines()) { _ = line; }
        // https://docs.microsoft.com/en-us/dotnet/csharp/deconstruct?WT.mc_id=DT-MVP-5003978#deconstructing-user-defined-types
        public void Deconstruct(out ReadOnlySpan<char> value, out int index)
        {
            value = Value;
            index = Index;
        }

        // This method allow to implicitly cast the type into a ReadOnlySpan<char>, so you can write the following code
        // foreach (ReadOnlySpan<char> entry in str.SplitLines())
        public static implicit operator ReadOnlySpan<char>(CharSplitEntry entry) => entry.Value;
    }
}