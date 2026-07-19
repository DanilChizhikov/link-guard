using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DTech.LinkGuard.Editor.ProGuard
{
    internal static class JavaSourceTypeExtractor
    {
        private static readonly Regex _packageRegex =
            new Regex(@"(?m)^\s*package\s+([A-Za-z_$][\w$.]*)", RegexOptions.Compiled);

        private static readonly Regex _token = new Regex(
            @"\b(?:enum\s+class|class|interface|enum|object)\s+(?<type>[A-Za-z_$][\w$]*)" +
            @"|(?<open>\{)" +
            @"|(?<close>\})",
            RegexOptions.Compiled);

        public static IReadOnlyList<JavaSourceType> Extract(string source, out string package)
        {
            package = string.Empty;
            List<JavaSourceType> result = new List<JavaSourceType>();

            if (string.IsNullOrEmpty(source))
            {
                return result;
            }

            string cleaned = StripCommentsAndLiterals(source);

            Match packageMatch = _packageRegex.Match(cleaned);
            if (packageMatch.Success)
            {
                package = packageMatch.Groups[1].Value;
            }
            
            int depth = 0;
            Dictionary<string, int> indexByName = new Dictionary<string, int>(StringComparer.Ordinal);
            int lastTopLevelIndex = -1;

            foreach (Match match in _token.Matches(cleaned))
            {
                if (match.Groups["open"].Success)
                {
                    depth++;
                }
                else if (match.Groups["close"].Success)
                {
                    if (depth > 0)
                    {
                        depth--;
                    }
                }
                else if (depth == 0)
                {
                    string name = match.Groups["type"].Value;

                    if (indexByName.TryGetValue(name, out int existing))
                    {
                        lastTopLevelIndex = existing;
                        continue;
                    }

                    result.Add(new JavaSourceType(name, false));
                    lastTopLevelIndex = result.Count - 1;
                    indexByName[name] = lastTopLevelIndex;
                }
                else if (lastTopLevelIndex >= 0)
                {
                    JavaSourceType outer = result[lastTopLevelIndex];

                    if (!outer.HasInnerClasses)
                    {
                        result[lastTopLevelIndex] = new JavaSourceType(outer.SimpleName, true);
                    }
                }
            }

            return result;
        }

        private static string StripCommentsAndLiterals(string source)
        {
            StringBuilder builder = new StringBuilder(source.Length);
            int i = 0;
            int length = source.Length;

            while (i < length)
            {
                char c = source[i];
                char next = i + 1 < length ? source[i + 1] : '\0';

                if (c == '/' && next == '/')
                {
                    i += 2;
                    while (i < length && source[i] != '\n')
                    {
                        i++;
                    }

                    continue;
                }

                if (c == '/' && next == '*')
                {
                    // Kotlin allows nested block comments.
                    i += 2;
                    int commentDepth = 1;

                    while (i < length && commentDepth > 0)
                    {
                        if (source[i] == '/' && i + 1 < length && source[i + 1] == '*')
                        {
                            commentDepth++;
                            i += 2;
                            continue;
                        }

                        if (source[i] == '*' && i + 1 < length && source[i + 1] == '/')
                        {
                            commentDepth--;
                            i += 2;
                            continue;
                        }

                        i++;
                    }

                    builder.Append(' ');
                    continue;
                }

                if (c == '"' && next == '"' && i + 2 < length && source[i + 2] == '"')
                {
                    // Kotlin raw string: no escapes, terminated by """.
                    i += 3;
                    while (i + 2 < length
                        && !(source[i] == '"' && source[i + 1] == '"' && source[i + 2] == '"'))
                    {
                        i++;
                    }

                    i = Math.Min(i + 3, length);
                    builder.Append("\"\"");
                    continue;
                }

                if (c == '"')
                {
                    i++;
                    while (i < length && source[i] != '"' && source[i] != '\n')
                    {
                        if (source[i] == '\\' && i + 1 < length)
                        {
                            i++;
                        }

                        i++;
                    }

                    i++;
                    builder.Append("\"\"");
                    continue;
                }

                if (c == '\'')
                {
                    i++;
                    while (i < length && source[i] != '\'' && source[i] != '\n')
                    {
                        if (source[i] == '\\' && i + 1 < length)
                        {
                            i++;
                        }

                        i++;
                    }

                    i++;
                    builder.Append("' '");
                    continue;
                }

                builder.Append(c);
                i++;
            }

            return builder.ToString();
        }
    }
}
