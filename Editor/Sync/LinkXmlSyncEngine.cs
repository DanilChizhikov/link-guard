using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DTech.LinkGuard.Editor
{
    /// <summary>
    /// Adds link.xml entries for project code that the file does not cover yet: every namespace of
    /// every project assembly, including assemblies the file does not mention at all. Assemblies
    /// that are explicitly narrowed on the <c>&lt;assembly&gt;</c> element itself are left alone,
    /// and so are non-project assemblies unless they are opted in. Nothing is ever removed,
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
            IReadOnlyList<string> scopePatterns = null,
            bool includeExternalAssemblies = false)
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
            List<string> skipped = new List<string>();

            // Explicit scope patterns run first: a pattern matching an assembly name creates a
            // whole-assembly entry, which the project pass below then leaves alone.
            SyncScopePatterns(document.Root, source, coverages, scopePatterns, additions);
            SyncProjectAssemblies(document.Root, source, coverages, includeExternalAssemblies, additions, skipped);

            bool changed = additions.Any;
            string result = changed ? Serialize(document, xml) : xml;

            return LinkXmlSyncOutcome.Completed(
                result,
                changed,
                additions.Assemblies,
                additions.BuildNamespaceGroups(),
                additions.BuildTypeGroups(),
                skipped);
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

        private static void SyncProjectAssemblies(
            XElement linker,
            IProjectTypeSource source,
            Dictionary<string, AssemblyCoverage> coverages,
            bool includeExternalAssemblies,
            SyncAdditions additions,
            List<string> skipped)
        {
            foreach (string assemblyName in source.AssemblyNames)
            {
                if (!includeExternalAssemblies && !source.IsProjectAssembly(assemblyName))
                {
                    continue;
                }

                if (!source.TryGetNamespaces(assemblyName, out IReadOnlyList<NamespaceEntry> namespaces))
                {
                    continue;
                }

                // An <assembly> element without children preserves everything, so an assembly with
                // nothing to write must never get an entry.
                if (!namespaces.Any(n => n.Types.Count > 0))
                {
                    continue;
                }

                if (!coverages.TryGetValue(assemblyName, out AssemblyCoverage coverage))
                {
                    coverage = CreateAssemblyEntry(linker, assemblyName, preserveWholeAssembly: false, additions);
                    coverages.Add(assemblyName, coverage);
                }

                if (coverage.PreservesWholeAssembly)
                {
                    continue;
                }

                if (coverage.IsExplicitlyNarrowed)
                {
                    skipped.Add(assemblyName);
                    continue;
                }

                foreach (NamespaceEntry ns in namespaces)
                {
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

                if (!matched.Any(n => n.Types.Count > 0))
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
            additions.Assemblies.Add(assemblyName);

            return preserveWholeAssembly
                ? AssemblyCoverage.WholeAssembly(assemblyName, element)
                : AssemblyCoverage.Empty(assemblyName, element);
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

        /// <summary>
        /// What a link.xml already preserves for one assembly name. A name may appear on several
        /// <c>&lt;assembly&gt;</c> elements, so both the element attributes and the child entries of
        /// every duplicate are aggregated: <c>preserve="all"</c> anywhere wins, and an assembly only
        /// counts as narrowed when every duplicate is narrowed.
        /// </summary>
        private sealed class AssemblyCoverage
        {
            private readonly HashSet<string> _coveredNamespaces = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> _narrowedNamespaces = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> _listedTypes = new HashSet<string>(StringComparer.Ordinal);

            private bool _preservesWholeAssembly;
            private bool _allElementsNarrowed = true;
            private bool _hasAbsorbedElement;

            public string Name { get; }

            /// <summary>The element new entries are appended to; never an explicitly narrowed duplicate.</summary>
            public XElement Element { get; private set; }

            public bool PreservesWholeAssembly => _preservesWholeAssembly;

            public bool IsExplicitlyNarrowed =>
                _hasAbsorbedElement && _allElementsNarrowed && !_preservesWholeAssembly;

            private AssemblyCoverage(string name, XElement element)
            {
                Name = name;
                Element = element;
            }

            public static AssemblyCoverage FromElement(string name, XElement element)
            {
                AssemblyCoverage coverage = new AssemblyCoverage(name, element);

                coverage.Absorb(element);

                return coverage;
            }

            public static AssemblyCoverage Empty(string name, XElement element)
            {
                return new AssemblyCoverage(name, element);
            }

            public static AssemblyCoverage WholeAssembly(string name, XElement element)
            {
                return new AssemblyCoverage(name, element) { _preservesWholeAssembly = true };
            }

            public void Absorb(XElement element)
            {
                bool narrowed = AssemblyIsExplicitlyNarrowed(element);

                if (AssemblyPreservesAll(element))
                {
                    _preservesWholeAssembly = true;
                }

                if (!narrowed)
                {
                    if (!_hasAbsorbedElement || _allElementsNarrowed)
                    {
                        Element = element;
                    }

                    _allElementsNarrowed = false;
                }

                _hasAbsorbedElement = true;

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

                if (!preservesEverything)
                {
                    _narrowedNamespaces.Add(NamespaceOf(fullname));
                }
            }
        }
    }
}
