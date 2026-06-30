#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;

namespace Neo.ConsolePlus
{
    internal static class NeoCommandParser
    {
        public static string[] SplitArguments(string input)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(input))
                return result.ToArray();

            StringBuilder current = new StringBuilder();
            bool insideQuotes = false;
            bool tokenStarted = false;
            bool quotedToken = false;
            char quoteChar = '\0';
            int objectDepth = 0;
            int arrayDepth = 0;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                bool escaped = i > 0 && input[i - 1] == '\\';
                bool insideJson = objectDepth > 0 || arrayDepth > 0;

                if (!escaped)
                {
                    if (c == '"' || c == '\'')
                    {
                        if (!insideQuotes)
                        {
                            insideQuotes = true;
                            quoteChar = c;
                            tokenStarted = true;
                            quotedToken = !insideJson && current.Length == 0;

                            if (insideJson)
                                current.Append(c);

                            continue;
                        }

                        if (quoteChar == c)
                        {
                            insideQuotes = false;
                            quoteChar = '\0';

                            if (insideJson)
                                current.Append(c);

                            continue;
                        }
                    }

                    if (!insideQuotes)
                    {
                        if (c == '{')
                        {
                            objectDepth++;
                            tokenStarted = true;
                        }
                        else if (c == '}' && objectDepth > 0)
                        {
                            objectDepth--;
                        }
                        else if (c == '[')
                        {
                            arrayDepth++;
                            tokenStarted = true;
                        }
                        else if (c == ']' && arrayDepth > 0)
                        {
                            arrayDepth--;
                        }
                    }
                }

                if (!insideQuotes && objectDepth == 0 && arrayDepth == 0 && char.IsWhiteSpace(c))
                {
                    AddCurrent(result, current, ref tokenStarted, ref quotedToken);
                    continue;
                }

                current.Append(c);
                tokenStarted = true;
            }

            AddCurrent(result, current, ref tokenStarted, ref quotedToken);
            return result.ToArray();
        }

        private static void AddCurrent(List<string> result, StringBuilder current, ref bool tokenStarted, ref bool quotedToken)
        {
            if (!tokenStarted)
                return;

            if (current.Length > 0 || quotedToken)
                result.Add(current.ToString());

            current.Length = 0;
            tokenStarted = false;
            quotedToken = false;
        }
    }
}
#endif
