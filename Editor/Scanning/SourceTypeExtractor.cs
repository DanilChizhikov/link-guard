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
            Stack<TypeScope> typeStack = new Stack<TypeScope>();
            TypeScope? pendingType = null;

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
                    TypeName typeName = BuildTypeName(cleaned, match);
                    types.Add(BuildType(ComposeNamespace(fileScoped, nsStack), typeStack, typeName));

                    int openIndex = FindDeclarationOpen(cleaned, match.Index + match.Length);
                    pendingType = openIndex >= 0
                        ? new TypeScope(typeName, 0, openIndex)
                        : null;
                }
                else if (match.Groups["open"].Success)
                {
                    depth++;

                    if (pendingType.HasValue && pendingType.Value.OpenIndex == match.Index)
                    {
                        TypeScope scope = pendingType.Value.WithDepth(depth);
                        typeStack.Push(scope);
                        pendingType = null;
                    }
                }
                else if (match.Groups["close"].Success)
                {
                    if (typeStack.Count > 0 && typeStack.Peek().Depth == depth)
                    {
                        typeStack.Pop();
                    }

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

        private static TypeEntry BuildType(string namespaceName, Stack<TypeScope> typeStack, TypeName typeName)
        {
            string typeFullname = ComposeTypeName(typeStack, typeName, "+");
            string linkerTypeFullname = ComposeTypeName(typeStack, typeName, "/");
            string fullname = string.IsNullOrEmpty(namespaceName)
                ? typeFullname
                : namespaceName + "." + typeFullname;
            string linkerFullname = string.IsNullOrEmpty(namespaceName)
                ? linkerTypeFullname
                : namespaceName + "." + linkerTypeFullname;
            string displayName = string.IsNullOrEmpty(namespaceName)
                ? linkerFullname
                : linkerFullname.Substring(namespaceName.Length + 1);

            return new TypeEntry(namespaceName ?? string.Empty, fullname, linkerFullname, displayName);
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

        private static string ComposeTypeName(Stack<TypeScope> typeStack, TypeName typeName, string separator)
        {
            if (typeStack.Count == 0)
            {
                return typeName.LinkerName;
            }

            string[] segments = new string[typeStack.Count + 1];
            int index = typeStack.Count - 1;

            foreach (TypeScope scope in typeStack)
            {
                segments[index--] = scope.Name.LinkerName;
            }

            segments[segments.Length - 1] = typeName.LinkerName;

            return string.Join(separator, segments);
        }

        private static TypeName BuildTypeName(string source, Match match)
        {
            string name = match.Groups["type"].Value;
            int arity = GetGenericArity(source, match.Index + match.Length);
            string linkerName = arity > 0 ? $"{name}`{arity}" : name;

            return new TypeName(linkerName);
        }

        private static int GetGenericArity(string source, int start)
        {
            int i = SkipWhitespace(source, start);

            if (i >= source.Length || source[i] != '<')
            {
                return 0;
            }

            int arity = 1;
            int depth = 0;

            while (i < source.Length)
            {
                char c = source[i];

                if (c == '<')
                {
                    depth++;
                }
                else if (c == '>')
                {
                    depth--;

                    if (depth == 0)
                    {
                        return arity;
                    }
                }
                else if (c == ',' && depth == 1)
                {
                    arity++;
                }

                i++;
            }

            return 0;
        }

        private static int FindDeclarationOpen(string source, int start)
        {
            int angleDepth = 0;
            int parenDepth = 0;
            int bracketDepth = 0;

            for (int i = start; i < source.Length; i++)
            {
                char c = source[i];

                if (c == '<')
                {
                    angleDepth++;
                }
                else if (c == '>' && angleDepth > 0)
                {
                    angleDepth--;
                }
                else if (c == '(')
                {
                    parenDepth++;
                }
                else if (c == ')' && parenDepth > 0)
                {
                    parenDepth--;
                }
                else if (c == '[')
                {
                    bracketDepth++;
                }
                else if (c == ']' && bracketDepth > 0)
                {
                    bracketDepth--;
                }
                else if (angleDepth == 0 && parenDepth == 0 && bracketDepth == 0)
                {
                    if (c == '{')
                    {
                        return i;
                    }

                    if (c == ';')
                    {
                        return -1;
                    }
                }
            }

            return -1;
        }

        private static int SkipWhitespace(string source, int start)
        {
            int i = start;

            while (i < source.Length && char.IsWhiteSpace(source[i]))
            {
                i++;
            }

            return i;
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
            public string Name { get; }
            public int Depth { get; }

            public NamespaceScope(string name, int depth)
            {
                Name = name;
                Depth = depth;
            }
        }

        private readonly struct TypeName
        {
            public string LinkerName { get; }

            public TypeName(string linkerName)
            {
                LinkerName = linkerName;
            }
        }

        private readonly struct TypeScope
        {
            public TypeName Name { get; }
            public int Depth { get; }
            public int OpenIndex { get; }

            public TypeScope(TypeName name, int depth, int openIndex)
            {
                Name = name;
                Depth = depth;
                OpenIndex = openIndex;
            }

            public TypeScope WithDepth(int depth)
            {
                return new TypeScope(Name, depth, OpenIndex);
            }
        }
    }
}