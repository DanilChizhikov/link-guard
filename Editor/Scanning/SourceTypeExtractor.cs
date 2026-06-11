using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DTech.LinkGuard.Editor
{
    internal static class SourceTypeExtractor
    {
        private static readonly Regex _token = new Regex(
            @"\bnamespace\s+(?<nsfile>[A-Za-z_][\w.]*)\s*;" +
            @"|\bnamespace\s+(?<nsblock>[A-Za-z_][\w.]*)\s*\{" +
            @"|\b(?:record\s+(?:struct\s+|class\s+)?|class\s+|struct\s+|interface\s+|enum\s+)(?<type>[A-Za-z_]\w*)" +
            @"|(?<open>\{)" +
            @"|(?<close>\})",
            RegexOptions.Compiled);
        
        public static List<TypeEntry> CollectFromFiles(IEnumerable<string> filePaths)
        {
            Dictionary<string, TypeEntry> byLinkerName = new Dictionary<string, TypeEntry>(StringComparer.Ordinal);

            if (filePaths == null)
            {
                return new List<TypeEntry>();
            }

            foreach (string path in filePaths)
            {
                string source;

                try
                {
                    source = File.ReadAllText(path);
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (TypeEntry entry in ExtractFromSource(source))
                {
                    byLinkerName[entry.LinkerFullname] = entry;
                }
            }

            return new List<TypeEntry>(byLinkerName.Values);
        }
        
        public static List<TypeEntry> ExtractFromSource(string source)
        {
            List<TypeEntry> types = new List<TypeEntry>();

            if (string.IsNullOrEmpty(source))
            {
                return types;
            }

            string cleaned = StripCommentsAndLiterals(source);

            int depth = 0;
            string fileScoped = null;
            Stack<NamespaceScope> nsStack = new Stack<NamespaceScope>();

            foreach (Match match in _token.Matches(cleaned))
            {
                if (match.Groups["nsfile"].Success)
                {
                    fileScoped = match.Groups["nsfile"].Value;
                }
                else if (match.Groups["nsblock"].Success)
                {
                    depth++;
                    nsStack.Push(new NamespaceScope(match.Groups["nsblock"].Value, depth));
                }
                else if (match.Groups["type"].Success)
                {
                    int namespaceDepth = nsStack.Count > 0 ? nsStack.Peek().Depth : 0;
                    if (depth == namespaceDepth)
                    {
                        types.Add(BuildType(ComposeNamespace(fileScoped, nsStack), match.Groups["type"].Value));
                    }
                }
                else if (match.Groups["open"].Success)
                {
                    depth++;
                }
                else if (match.Groups["close"].Success)
                {
                    if (nsStack.Count > 0 && nsStack.Peek().Depth == depth)
                    {
                        nsStack.Pop();
                    }

                    if (depth > 0)
                    {
                        depth--;
                    }
                }
            }

            return types;
        }

        private static TypeEntry BuildType(string namespaceName, string typeName)
        {
            string fullname = string.IsNullOrEmpty(namespaceName)
                ? typeName
                : namespaceName + "." + typeName;

            return new TypeEntry(namespaceName ?? string.Empty, fullname, fullname, typeName);
        }

        private static string ComposeNamespace(string fileScoped, Stack<NamespaceScope> nsStack)
        {
            if (nsStack.Count == 0)
            {
                return fileScoped ?? string.Empty;
            }

            string[] segments = new string[nsStack.Count];
            int index = nsStack.Count - 1;

            foreach (NamespaceScope scope in nsStack)
            {
                segments[index--] = scope.Name;
            }

            string blockNamespace = string.Join(".", segments);

            return string.IsNullOrEmpty(fileScoped)
                ? blockNamespace
                : fileScoped + "." + blockNamespace;
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
                    i += 2;
                    while (i < length && !(source[i] == '*' && i + 1 < length && source[i + 1] == '/'))
                    {
                        i++;
                    }

                    i += 2;
                    builder.Append(' ');

                    continue;
                }

                if (c == '@' && next == '"')
                {
                    i += 2;
                    while (i < length)
                    {
                        if (source[i] == '"')
                        {
                            if (i + 1 < length && source[i + 1] == '"')
                            {
                                i += 2;
                                continue;
                            }

                            i++;

                            break;
                        }

                        i++;
                    }

                    builder.Append("\"\"");

                    continue;
                }

                if (c == '"')
                {
                    i++;
                    while (i < length && source[i] != '"')
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
                    while (i < length && source[i] != '\'')
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

        private readonly struct NamespaceScope
        {
            public NamespaceScope(string name, int depth)
            {
                Name = name;
                Depth = depth;
            }

            public string Name { get; }
            public int Depth { get; }
        }
    }
}
