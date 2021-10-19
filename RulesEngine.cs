using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Malfoy
{
    public class TokenException: Exception
    {
        public TokenException(string message): base(message){}    
    }

    public class RuleException : Exception
    {
        public RuleException(string message) : base(message) { }
    }

    public static class RulesEngine
    {
        public static readonly List<string> SingleTokens = new() { ":", "l", "u", "c", "C", "t", "r", "d", "f", "{", "}", "[", "]", "q", "k", "K" };
        public static readonly List<string> DoubleTokens = new() { "$", "^", "p", "T", "D", "'", "@", "z", "Z", "L", "R", "+", "-", ".", ",", "y", "Y" };
        public static readonly List<string> TripleTokens = new() { "x", "O", "i", "o", "s", "*"};

        //Filters a list for duplicates given a list of words and a set of rules
        public static List<string> FilterByRules(IEnumerable<string> input, List<List<string>> rules)
        {
            //1. Remove any duplicates from the values
            var values = input.Distinct().ToList(); 

            //2. Create all permutations of all values
            var dict = new Dictionary<string, List<string>>();

            foreach (var value in values)
            {
                var perms = new HashSet<string>();

                foreach (var rule in rules)
                {
                    var word = ProcessRuleTokens(value, rule);
                    perms.Add(word);
                }

                dict.Add(value, perms.ToList());
            }

            //3. Keep looping until no more values are removed
            var valuesCount = values.Count();
            var lastCount = valuesCount + 1;

            while (lastCount > valuesCount)
            {
                //If valuesCount doesnt change in this iteration, break out of the while
                lastCount = valuesCount;

                //Loop through each perms list and count how many are in the values
                var counts = new Dictionary<string, int>();

                foreach (var value in dict.Keys)
                {
                    counts[value] = -1; //Because self will be counted too

                    foreach (var perm in dict[value])
                    {
                        if (values.Contains(perm)) counts[value] = counts[value]+ 1;
                    }
                }

                //Find the counts with the most matches               
                var found = "";
                var count = 0;

                foreach (var value in counts.Keys)
                {
                    if (counts[value] > count)
                    {
                        found = value;
                        count = counts[value];
                    }
                }

                //Remove the values in the dict matching the value with the highest count
                if (count > 0)
                {
                    foreach (var perm in dict[found])
                    {
                        if (perm != found)
                        {
                            //Remove from dict and values
                            values.Remove(perm);
                            dict.Remove(perm);
                        }
                    }

                    //Set new valuesCount
                    valuesCount = values.Count;
                }
            }


            return values;
        }


        //Get a list of rules and return a collection of tokens
        public static List<List<string>> ProcessRules(string[] rules)
        {
            var results = new List<List<string>>();

            foreach (var rule in rules)
            {
                var tokens = TokenizeRule(rule);
                if (tokens.Count > 0) results.Add(tokens);
            }

            return results;
        }

        //Process a rule made of one or more tokens
        public static string ProcessRuleTokens(string word, List<string> tokens)
        {
            //Now apply the rule tokens to the word
            foreach (var token in tokens)
            {
                word = ProcessToken(word, token);
            }

            return word;
        }

        //Turn a rule line into a list of tokens
        public static List<string> TokenizeRule(string rule)
        {
            var tokens = new List<string>();
       
            //Remove all white space
            rule = Regex.Replace(rule, @"\s+", "");

            //Check for comments, return empty tokens
            if (rule.StartsWith("##")) return tokens;

            while (rule.Length > 0)
            {
                var found = "";

                //Loop through each token type and add to the output
                if (rule.Length > 2)
                {
                    foreach (var token in TripleTokens)
                    {
                        if (rule.StartsWith(token))
                        {
                            found = rule.Substring(0, 3);
                            break;
                        }
                    }
                }

                if (rule.Length > 1 && found == "")
                {
                    foreach (var token in DoubleTokens)
                    {
                        if (rule.StartsWith(token))
                        {
                            found = found = rule.Substring(0, 2);
                            break;
                        }
                    }
                }
                
                if (found == "")
                {
                    foreach (var token in SingleTokens)
                    {
                        if (rule.StartsWith(token))
                        {
                            found = found = rule.Substring(0, 1);
                            break;
                        }
                    }
                }

                if (found == "") throw new RuleException($"Could not process remaining rule {rule}, token {tokens.Count}");

                //Add token and remove from front of rule
                tokens.Add(found);
                rule = rule.Substring(found.Length);
            }

            return tokens;
        }

        public static string ProcessToken(string word, string token)
        {
            if (string.IsNullOrEmpty(word)) return "";

            if (token == ":") return word;
            if (token == "l") return word.ToLowerInvariant();
            if (token == "u") return word.ToUpperInvariant();
            if (token == "c") return $"{word[0].ToUpperInvariant()}{word[1..].ToLowerInvariant()}";
            if (token == "C") return $"{word[0].ToLowerInvariant()}{word[1..].ToUpperInvariant()}";
            if (token == "t") return word.Select(c => c.ToggleCase()).ToStringFromEnumerable();
            if (token == "r") return word.Reverse();
            if (token == "d") return $"{word}{word}";
            if (token == "f") return $"{word}{word.Reverse()}";
            if (token == "{") return $"{word[1..]}{word[0]}";
            if (token == "}") return $"{word[^1..]}{word[0..^1]}";
            if (token == "[") return $"{word[1..]}";
            if (token == "]") return $"{word[0..^1]}";
            if (token == "q") return word.DuplicateAll();
            
            //Swap first two characters
            if (token == "k")
            {
                if (word.Length < 2) return "";
                return $"{word[1]}{word[0]}{word[2..]}";
            }

            //Swap last two characters
            if (token == "K")
            {
                if (word.Length < 2) return "";
                return $"{word[0..^2]}{word[^1]}{word[^2]}";
            }

            //Rules of more than one character with N 
            if (token.Length > 1)
            { 
                var rulePrefix = token.Substring(0, 1);

                if (rulePrefix == "$") return $"{word}{token[1]}";
                if (rulePrefix == "^") return $"{token[1]}{word}";

                if (rulePrefix == "p")
                {
                    var n = token[1].HexToInt();
                    return word.Repeat(n);
                }

                //Toggle at N
                if (rulePrefix == "T")
                {
                    var n = token[1].HexToInt();
                    if (n >= word.Length) return word;
                    if (n == 0) return $"{word[0].ToggleCase()}{word[1..]}";

                    return $"{word[..(n)]}{word[n].ToggleCase()}{word[(n + 1)..]}";
                }

                //Delete at N
                if (rulePrefix == "D")
                {
                    var n = token[1].HexToInt();
                    if (n >= word.Length) return word;
                    if (n == 0) return $"{word[1..]}";

                    return $"{word[..(n)]}{word[(n + 1)..]}";
                }

                //Truncate at N
                if (rulePrefix == "'")
                {
                    var n = token[1].HexToInt();
                    if (n >= word.Length) return word;

                    return $"{word[..(n)]}";
                }

                //Purge all instances of X
                if (rulePrefix == "@") return word.Replace(token[1].ToString(), "");

                //Duplicate first N times
                if (rulePrefix == "z")
                {
                    var n = token[1].HexToInt();

                    return $"{word[0].Repeat(n-1)}{word}";
                }

                //Duplicate last N times
                if (rulePrefix == "Z")
                {
                    var n = token[1].HexToInt();

                    return $"{word}{word[^1].Repeat(n - 1)}";
                }

                //Bitwise shift left at N
                if (rulePrefix == "L")
                {
                    var n = token[1].HexToInt();

                    if (n >= word.Length) return word;

                    var shift = word[n].ShiftLeft();

                    return $"{word[..(n)]}{shift}{word[(n + 1)..]}";
                }

                //Bitwise shift left at N
                if (rulePrefix == "R")
                {
                    var n = token[1].HexToInt();

                    if (n >= word.Length) return word;

                    return $"{word[..(n)]}{word[n].ShiftRight()}{word[(n + 1)..]}";
                }

                //Ascii increment  at N
                if (rulePrefix == "+")
                {
                    var n = token[1].HexToInt();

                    if (n >= word.Length) return word;

                    return $"{word[..(n)]}{word[n].AsciiIncrement()}{word[(n + 1)..]}";
                }

                //Ascii decrement  at N
                if (rulePrefix == "-")
                {
                    var n = token[1].HexToInt();

                    if (n >= word.Length) return word;

                    return $"{word[..(n)]}{word[n].AsciiDecrement()}{word[(n + 1)..]}";
                }

                //Replace at N with value at N + 1
                if (rulePrefix == ".")
                {
                    var n = token[1].HexToInt();

                    if (n - 1 >= word.Length) return word;

                    return $"{word[..(n)]}{word[n+1]}{word[(n + 1)..]}";
                }

                //Replace at N with value at N - 1
                if (rulePrefix == ",")
                {
                    var n = token[1].HexToInt();

                    if (n == 0 || word.Length == 1) return word;

                    return $"{word[..(n)]}{word[n - 1]}{word[(n + 1)..]}";
                }

                //Duplicate first N
                if (rulePrefix == "y")
                {
                    var n = token[1].HexToInt();

                    if (n > word.Length) return word;

                    return $"{word[..n]}{word}";
                }

                //Duplicate last N
                if (rulePrefix == "Y")
                {
                    var n = token[1].HexToInt();

                    if (n > word.Length) return word;

                    return $"{word}{word[^n..]}";
                }

                //Rules longer than 2 chars
                if (token.Length > 2)
                {
                    //Extract range xNM
                    if (rulePrefix == "x")
                    {
                        var n = token[1].HexToInt();
                        var m = int.Parse(token[2].ToString(), NumberStyles.HexNumber); //0-9 and a-f

                        if (n > word.Length) return "";
                        return word.Substring(n, m);
                    }

                    //Delete M chars starting from N ONM
                    if (rulePrefix == "O")
                    {
                        var n = token[1].HexToInt();
                        var m = token[2].HexToInt();

                        if (n >= word.Length) return "";
                        if (n + m >= word.Length) return $"{word[..n]}";

                        return $"{word[..n]}{word[(n + m)..]}";
                    }

                    //Insert character X at position N
                    if (rulePrefix == "i")
                    {
                        var n = token[1].HexToInt();

                        if (n > word.Length) return word;
                        return $"{word[..n]}{token[2]}{word[(n)..]}";
                    }

                    //Overwrite character X at position N
                    if (rulePrefix == "o")
                    {
                        var n = token[1].HexToInt();

                        if (n >= word.Length) return word;
                        return $"{word[..(n)]}{token[2]}{word[(n+1)..]}";
                    }

                    //Replace X with Y
                    if (rulePrefix == "s") return word.Replace(token[1], token[2]);

                    //Swap N at M
                    if (rulePrefix == "*")
                    {
                        var n = token[1].HexToInt();
                        var m = token[2].HexToInt();

                        if (n >= word.Length || m >= word.Length) return word;

                        var chars = word.ToArray();

                        chars[n] = word[m];
                        chars[m] = word[n];

                        return chars.ToStringFromEnumerable();
                    }

                }
            }

            throw new TokenException($"Could not process rule {token}");
        }
    }

    public static class RulesEngineExtensions
    {
        public static char ToggleCase(this char value)
        {
            if (!char.IsLetter(value)) return value;
            return char.IsUpper(value) ? char.ToLowerInvariant(value) : char.ToUpperInvariant(value);
        }

        public static string ToStringFromEnumerable(this IEnumerable<char> charSequence)
        {
            return new string(charSequence.ToArray());
        }

        public static string Reverse(this string s)
        {
            char[] charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }

        public static string Repeat(this string s, int n)
        {
            var builder = new StringBuilder();

            for (var i = 0; i <= n; i++) builder.Append(s);

            return builder.ToString();
        }

        public static string Repeat(this char c, int n)
        {
            var builder = new StringBuilder();

            for (var i = 0; i <= n; i++) builder.Append(c);

            return builder.ToString();
        }

        public static char ToUpperInvariant(this char c)
        {
            return char.ToUpperInvariant(c);
        }

        public static char ToLowerInvariant(this char c)
        {
            return char.ToLowerInvariant(c);
        }

        public static int HexToInt(this char c)
        {
            return int.Parse(c.ToString(), NumberStyles.HexNumber);
        }

        public static char AsciiIncrement(this char c)
        {
            var x = (byte)c;
            x++;
            return (char)x;
        }

        public static char AsciiDecrement(this char c)
        {
            var x = (byte)c;
            x--;
            return (char)x;
        }

        //https://stackoverflow.com/questions/737781/left-bit-shifting-255-as-a-byte
        public static char ShiftLeft(this char c)
        {
            return (char)((c << 1) & 0xFF);
        }

        public static char ShiftRight(this char c)
        {
            return (char)((c >> 1) & 0xFF);
        }

        public static string DuplicateAll(this string s)
        {
            var builder = new StringBuilder();

            for (var i = 0; i < s.Length; i++) builder.Append(s[i], 2);

            return builder.ToString();
        }
    }
}
