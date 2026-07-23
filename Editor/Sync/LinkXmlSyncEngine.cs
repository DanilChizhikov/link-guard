using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DTech.LinkGuard.Editor
{
    /// <summary>
    /// Adds link.xml entries for project code that the file does not cover yet. The scope is
    /// inferred from the file itself: a namespace that already has whole-type entries is treated
    /// as tracked, so types added to it later must stay preserved. Nothing is ever removed,
    /// rewritten, or narrowed.
    /// </summary>
    internal static class LinkXmlSyncEngine
    {
        private const string LinkerElement = "linker";
        private const string AssemblyElement = "assembly";
        private const string TypeElement = "type";
        private const string NamespaceElement = "namespace";
        private const string MethodElement = "method";
        private const string FieldElement = "field";
        private const string PropertyElement = "property";
        private const string EventElement = "event";
        private const string FullnameAttribute = "fullname";
        private const string PreserveAttribute = "preserve";
        private const string PreserveAllValue = "all";
        private const string WildcardSuffix = ".*";
        private const string IndentUnit = "    ";

        public static LinkXmlSyncOutcome Sync(
            string xml,
            IProjectTypeSource source,
            IReadOnlyList<string> scopePatterns = null)
        {
            if (source == null)
            {
                return LinkXmlSyncOutcome.Failed("No project type source was supplied.", xml);
            }

            XDocument document;

            try
            {
                document = XDocument.Parse(xml ?? string.Empty, LoadOptions.PreserveWhitespace);
            }
            catch (Exception ex)
            {
                return LinkXmlSyncOutcome.Failed($"Failed to parse link.xml: {ex.Message}", xml);
            }

            if (document.Root == null || document.Root.Name.LocalName != LinkerElement)
            {
                return LinkXmlSyncOutcome.Failed("Root element is not <linker>.", xml);
            }

            Dictionary<string, AssemblyCoverage> coverages = CollectCoverages(document.Root);
            SyncAdditions additions = new SyncAdditions();

            SyncTrackedNamespaces(source, coverages, additions);
            SyncScopePatterns(document.Root, source, coverages, scopePatterns, additions);

            List<string> untracked = CollectUntrackedAssemblies(source, coverages);

            bool changed = additions.Any;
            string result = changed ? Serialize(document, xml) : xml;

            return LinkXmlSyncOutcome.Completed(
                result,
                changed,
                additions.Assemblies,
                additions.BuildNamespaceGroups(),
                additions.BuildTypeGroups(),
                untracked);
        }

        private static Dictionary<string, AssemblyCoverage> CollectCoverages(XElement linker)
        {
            Dictionary<string, AssemblyCoverage> coverages =
                new Dictionary<string, AssemblyCoverage>(StringComparer.Ordinal);

            foreach (XElement element in linker.Elements()
                .Where(e => string.Equals(e.Name.LocalName, AssemblyElement, StringComparison.Ordinal)))
            {
                string name = element.Attribute(FullnameAttribute)?.Value;

                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (coverages.TryGetValue(name, out AssemblyCoverage existing))
                {
                    existing.Absorb(element);
                    continue;
                }

                coverages.Add(name, AssemblyCoverage.FromElement(name, element));
            }

            return coverages;
        }

        private static void SyncTrackedNamespaces(
            IProjectTypeSource source,
            Dictionary<string, AssemblyCoverage> coverages,
            SyncAdditions additions)
        {
            foreach (AssemblyCoverage coverage in coverages.Values.ToList())
            {
                if (coverage.PreservesWholeAssembly || coverage.IsExplicitlyNarrowed)
                {
                    continue;
                }

                if (!source.TryGetNamespaces(coverage.Name, out IReadOnlyList<NamespaceEntry> namespaces))
                {
                    continue;
                }

                foreach (NamespaceEntry ns in namespaces)
                {
                    if (!coverage.IsTracked(ns.Fullname))
                    {
                        continue;
                    }

                    EnsureNamespaceCovered(coverage, ns, additions, force: false);
                }
            }
        }

        private static void SyncScopePatterns(
            XElement linker,
            IProjectTypeSource source,
            Dictionary<string, AssemblyCoverage> coverages,
            IReadOnlyList<string> scopePatterns,
            SyncAdditions additions)
        {
            List<Regex> matchers = BuildMatchers(scopePatterns);

            if (matchers.Count == 0)
            {
                return;
            }

            foreach (string assemblyName in source.AssemblyNames)
            {
                if (!source.TryGetNamespaces(assemblyName, out IReadOnlyList<NamespaceEntry> namespaces))
                {
                    continue;
                }

                bool assemblyMatches = Matches(matchers, assemblyName);

                List<NamespaceEntry> matched = assemblyMatches
                    ? namespaces.ToList()
                    : namespaces
                        .Where(n => !string.IsNullOrEmpty(n.Fullname) && Matches(matchers, n.Fullname))
                        .ToList();

                if (matched.Count == 0)
                {
                    continue;
                }

                if (!coverages.TryGetValue(assemblyName, out AssemblyCoverage coverage))
                {
                    coverage = CreateAssemblyEntry(linker, assemblyName, assemblyMatches, additions);
                    coverages.Add(assemblyName, coverage);

                    if (coverage.PreservesWholeAssembly)
                    {
                        continue;
                    }
                }

                if (coverage.PreservesWholeAssembly || coverage.IsExplicitlyNarrowed)
                {
                    continue;
                }

                foreach (NamespaceEntry ns in matched)
                {
                    EnsureNamespaceCovered(coverage, ns, additions, force: true);
                }
            }
        }

        private static AssemblyCoverage CreateAssemblyEntry(
            XElement linker,
            string assemblyName,
            bool preserveWholeAssembly,
            SyncAdditions additions)
        {
            XElement element = new XElement(AssemblyElement, new XAttribute(FullnameAttribute, assemblyName));

            if (preserveWholeAssembly)
            {
                element.Add(new XAttribute(PreserveAttribute, PreserveAllValue));
            }

            AppendChild(linker, element);

            if (!preserveWholeAssembly)
            {
                return AssemblyCoverage.Empty(assemblyName, element);
            }

            additions.Assemblies.Add(assemblyName);

            return AssemblyCoverage.WholeAssembly(assemblyName, element);
        }

        private static void EnsureNamespaceCovered(
            AssemblyCoverage coverage,
            NamespaceEntry ns,
            SyncAdditions additions,
            bool force)
        {
            if (coverage.IsCovered(ns.Fullname))
            {
                return;
            }

            List<string> missing = ns.Types
                .Where(t => !coverage.IsTypeListed(t))
                .Select(t => t.LinkerFullname)
                .ToList();

            if (!force && missing.Count == 0)
            {
                return;
            }

            bool collapse = !string.IsNullOrEmpty(ns.Fullname)
                && (force || !coverage.HasNarrowedTypes(ns.Fullname));

            if (collapse)
            {
                AppendChild(coverage.Element, new XElement(
                    NamespaceElement,
                    new XAttribute(FullnameAttribute, ns.Fullname),
                    new XAttribute(PreserveAttribute, PreserveAllValue)));

                coverage.MarkNamespaceCovered(ns.Fullname);
                additions.AddNamespace(coverage.Name, ns.Fullname);
                return;
            }

            foreach (string typeName in missing)
            {
                AppendChild(coverage.Element, new XElement(
                    TypeElement,
                    new XAttribute(FullnameAttribute, typeName),
                    new XAttribute(PreserveAttribute, PreserveAllValue)));

                coverage.MarkTypeListed(typeName);
                additions.AddType(coverage.Name, typeName);
            }
        }

        private static List<string> CollectUntrackedAssemblies(
            IProjectTypeSource source,
            Dictionary<string, AssemblyCoverage> coverages)
        {
            List<string> untracked = new List<string>();

            foreach (string assemblyName in source.AssemblyNames)
            {
                if (coverages.ContainsKey(assemblyName) || !source.IsProjectAssembly(assemblyName))
                {
                    continue;
                }

                untracked.Add(assemblyName);
            }

            return untracked;
        }

        private static List<Regex> BuildMatchers(IReadOnlyList<string> scopePatterns)
        {
            List<Regex> matchers = new List<Regex>();

            if (scopePatterns == null)
            {
                return matchers;
            }

            foreach (string pattern in scopePatterns)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    continue;
                }

                string expression = Regex.Escape(pattern.Trim())
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".");

                matchers.Add(new Regex($"^{expression}$", RegexOptions.CultureInvariant));
            }

            return matchers;
        }

        private static bool Matches(List<Regex> matchers, string value)
        {
            return !string.IsNullOrEmpty(value) && matchers.Any(m => m.IsMatch(value));
        }

        private static void AppendChild(XElement parent, XElement child)
        {
            string childIndent = GetChildIndent(parent);

            if (parent.LastNode is XText trailing && string.IsNullOrWhiteSpace(trailing.Value))
            {
                trailing.AddBeforeSelf(new XText(childIndent));
                trailing.AddBeforeSelf(child);
                return;
            }

            parent.Add(new XText(childIndent));
            parent.Add(child);
            parent.Add(new XText(GetIndent(parent)));
        }

        private static string GetChildIndent(XElement parent)
        {
            foreach (XNode node in parent.Nodes())
            {
                if (node is XText text
                    && string.IsNullOrWhiteSpace(text.Value)
                    && text.Value.IndexOf('\n') >= 0)
                {
                    return text.Value;
                }
            }

            return GetIndent(parent) + IndentUnit;
        }

        private static string GetIndent(XElement element)
        {
            if (element.PreviousNode is XText text && string.IsNullOrWhiteSpace(text.Value))
            {
                int newline = text.Value.LastIndexOf('\n');

                if (newline >= 0)
                {
                    return text.Value.Substring(newline);
                }
            }

            return element.Parent == null ? "\n" : "\n" + IndentUnit;
        }

        private static string Serialize(XDocument document, string originalXml)
        {
            string body = document.ToString(SaveOptions.DisableFormatting);

            if (document.Declaration != null)
            {
                body = document.Declaration + "\n" + body.TrimStart('\r', '\n');
            }

            return body.TrimEnd('\r', '\n') + GetTrailingNewline(originalXml);
        }

        private static string GetTrailingNewline(string xml)
        {
            if (string.IsNullOrEmpty(xml))
            {
                return string.Empty;
            }

            if (xml.EndsWith("\r\n", StringComparison.Ordinal))
            {
                return "\r\n";
            }

            return xml.EndsWith("\n", StringComparison.Ordinal) ? "\n" : string.Empty;
        }

        private static bool AssemblyPreservesAll(XElement element)
        {
            string preserve = element.Attribute(PreserveAttribute)?.Value;

            if (!string.IsNullOrEmpty(preserve))
            {
                return IsAll(preserve);
            }

            return !element.Elements().Any();
        }

        private static bool AssemblyIsExplicitlyNarrowed(XElement element)
        {
            string preserve = element.Attribute(PreserveAttribute)?.Value;

            return !string.IsNullOrEmpty(preserve) && !IsAll(preserve) && !element.Elements().Any();
        }

        private static bool PreservesEverything(XElement element)
        {
            string preserve = element.Attribute(PreserveAttribute)?.Value;

            if (!string.IsNullOrEmpty(preserve))
            {
                return IsAll(preserve);
            }

            return !element.Elements().Any(e => IsMemberElement(e.Name.LocalName));
        }

        private static bool IsMemberElement(string localName)
        {
            return string.Equals(localName, MethodElement, StringComparison.Ordinal)
                || string.Equals(localName, FieldElement, StringComparison.Ordinal)
                || string.Equals(localName, PropertyElement, StringComparison.Ordinal)
                || string.Equals(localName, EventElement, StringComparison.Ordinal);
        }

        private static bool IsAll(string preserve)
        {
            return string.Equals(preserve, PreserveAllValue, StringComparison.OrdinalIgnoreCase);
        }

        private static string NamespaceOf(string typeFullname)
        {
            int slashIndex = typeFullname.IndexOf('/');
            string rootType = slashIndex < 0 ? typeFullname : typeFullname.Substring(0, slashIndex);
            int dotIndex = rootType.LastIndexOf('.');

            return dotIndex < 0 ? string.Empty : rootType.Substring(0, dotIndex);
        }

        private sealed class SyncAdditions
        {
            private readonly Dictionary<string, List<string>> _namespaces =
                new Dictionary<string, List<string>>(StringComparer.Ordinal);

            private readonly Dictionary<string, List<string>> _types =
                new Dictionary<string, List<string>>(StringComparer.Ordinal);

            public List<string> Assemblies { get; } = new List<string>();

            public bool Any => Assemblies.Count > 0 || _namespaces.Count > 0 || _types.Count > 0;

            public void AddNamespace(string assemblyName, string namespaceName)
            {
                Add(_namespaces, assemblyName, namespaceName);
            }

            public void AddType(string assemblyName, string typeName)
            {
                Add(_types, assemblyName, typeName);
            }

            public IReadOnlyList<LinkXmlSyncEntryGroup> BuildNamespaceGroups()
            {
                return Build(_namespaces);
            }

            public IReadOnlyList<LinkXmlSyncEntryGroup> BuildTypeGroups()
            {
                return Build(_types);
            }

            private static void Add(Dictionary<string, List<string>> target, string assemblyName, string value)
            {
                if (!target.TryGetValue(assemblyName, out List<string> names))
                {
                    names = new List<string>();
                    target.Add(assemblyName, names);
                }

                names.Add(value);
            }

            private static IReadOnlyList<LinkXmlSyncEntryGroup> Build(Dictionary<string, List<string>> source)
            {
                return source
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new LinkXmlSyncEntryGroup(pair.Key, pair.Value))
                    .ToList();
            }
        }

        private sealed class AssemblyCoverage
        {
            private readonly HashSet<string> _coveredNamespaces = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> _trackedNamespaces = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> _narrowedNamespaces = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> _listedTypes = new HashSet<string>(StringComparer.Ordinal);

            public string Name { get; }
            public XElement Element { get; }
            public bool PreservesWholeAssembly { get; }
            public bool IsExplicitlyNarrowed { get; }

            private AssemblyCoverage(
                string name,
                XElement element,
                bool preservesWholeAssembly,
                bool isExplicitlyNarrowed)
            {
                Name = name;
                Element = element;
                PreservesWholeAssembly = preservesWholeAssembly;
                IsExplicitlyNarrowed = isExplicitlyNarrowed;
            }

            public static AssemblyCoverage FromElement(string name, XElement element)
            {
                AssemblyCoverage coverage = new AssemblyCoverage(
                    name,
                    element,
                    AssemblyPreservesAll(element),
                    AssemblyIsExplicitlyNarrowed(element));

                coverage.Absorb(element);

                return coverage;
            }

            public static AssemblyCoverage Empty(string name, XElement element)
            {
                return new AssemblyCoverage(name, element, false, false);
            }

            public static AssemblyCoverage WholeAssembly(string name, XElement element)
            {
                return new AssemblyCoverage(name, element, true, false);
            }

            public void Absorb(XElement element)
            {
                foreach (XElement child in element.Elements())
                {
                    string localName = child.Name.LocalName;

                    if (string.Equals(localName, NamespaceElement, StringComparison.Ordinal))
                    {
                        AbsorbNamespace(child);
                        continue;
                    }

                    if (string.Equals(localName, TypeElement, StringComparison.Ordinal))
                    {
                        AbsorbType(child);
                    }
                }
            }

            public bool IsCovered(string namespaceName)
            {
                return _coveredNamespaces.Contains(namespaceName ?? string.Empty);
            }

            public bool IsTracked(string namespaceName)
            {
                return _trackedNamespaces.Contains(namespaceName ?? string.Empty);
            }

            public bool HasNarrowedTypes(string namespaceName)
            {
                return _narrowedNamespaces.Contains(namespaceName ?? string.Empty);
            }

            public bool IsTypeListed(TypeEntry type)
            {
                return _listedTypes.Contains(type.LinkerFullname) || _listedTypes.Contains(type.Fullname);
            }

            public void MarkNamespaceCovered(string namespaceName)
            {
                _coveredNamespaces.Add(namespaceName ?? string.Empty);
            }

            public void MarkTypeListed(string typeFullname)
            {
                _listedTypes.Add(typeFullname);
            }

            private void AbsorbNamespace(XElement element)
            {
                string fullname = element.Attribute(FullnameAttribute)?.Value;

                if (string.IsNullOrEmpty(fullname) || !PreservesEverything(element))
                {
                    return;
                }

                _coveredNamespaces.Add(fullname);
            }

            private void AbsorbType(XElement element)
            {
                string fullname = element.Attribute(FullnameAttribute)?.Value;

                if (string.IsNullOrEmpty(fullname))
                {
                    return;
                }

                bool preservesEverything = PreservesEverything(element);

                if (fullname.EndsWith(WildcardSuffix, StringComparison.Ordinal))
                {
                    if (preservesEverything)
                    {
                        _coveredNamespaces.Add(fullname.Substring(0, fullname.Length - WildcardSuffix.Length));
                    }

                    return;
                }

                if (fullname.IndexOf('*') >= 0)
                {
                    return;
                }

                _listedTypes.Add(fullname);
                string namespaceName = NamespaceOf(fullname);

                if (preservesEverything)
                {
                    _trackedNamespaces.Add(namespaceName);
                    return;
                }

                _narrowedNamespaces.Add(namespaceName);
            }
        }
    }
}
