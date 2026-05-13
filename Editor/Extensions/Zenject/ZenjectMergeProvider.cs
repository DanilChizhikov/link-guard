#if LINKGUARD_ZENJECT_ENABLED
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using UnityEditor;

namespace DTech.LinkGuard.Editor.Zenject
{
    internal sealed class ZenjectMergeProvider : ILinkXmlMergeProvider
    {
        private const string DialogTitle = "Link XML Generator (Zenject)";

        public string Id => "zenject";
        public string ButtonLabel => "Merge from Zenject Installers";
        public string Tooltip => "Scan Zenject contexts and add reachable installer + bound types to link.xml.";

        public LinkXmlProviderResult Provide()
        {
            ZenjectScanResult scan;

            try
            {
                scan = Run();
            }
            catch (Exception ex)
            {
                return LinkXmlProviderResult.Failure($"Zenject scan failed: {ex.Message}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (scan.LinkEntries.Count == 0)
            {
                return new LinkXmlProviderResult(string.Empty, scan.Report, scan.Warnings, true);
            }

            string xml = BuildLinkerXml(scan.LinkEntries);
            return new LinkXmlProviderResult(xml, scan.Report, scan.Warnings, true);
        }

        internal static ZenjectScanResult Run()
        {
            void Progress(string message, float progress) =>
                EditorUtility.DisplayProgressBar(DialogTitle, message, progress);

            ZenjectRootedSet rooted = ZenjectContextScanner.ScanRoots(Progress);
            ZenjectReachableInstallers reachable = ZenjectInstallerReachability.Expand(rooted.InstallerTypes, Progress);
            HashSet<Assembly> reachableAssemblies = CollectAssemblies(reachable.InstallerTypes);

            Progress("Extracting bindings...", 0.7f);
            List<string> warnings = new List<string>();
            warnings.AddRange(rooted.Warnings);
            warnings.AddRange(reachable.Warnings);

            ZenjectExtractionResult extraction = ZenjectIlBindingExtractor.Extract(reachable.InstallerTypes, warnings);

            Progress("Scanning [Inject] members...", 0.85f);
            HashSet<Type> injectTypes = ZenjectInjectAttributeScanner.Scan(reachableAssemblies, warnings);

            Progress("Building link.xml entries...", 0.95f);

            HashSet<TypeIdentifier> entries = new HashSet<TypeIdentifier>();

            foreach (Type installer in reachable.InstallerTypes)
            {
                AddRuntimeType(entries, installer);
            }

            foreach (TypeIdentifier bound in extraction.BoundTypes)
            {
                if (bound != null && !bound.IsGenericParameter)
                {
                    entries.Add(bound);
                }
            }

            foreach (Type inject in injectTypes)
            {
                AddRuntimeType(entries, inject);
            }

            string report = BuildReport(rooted.InstallerTypes.Count, reachable.InstallerTypes.Count,
                extraction.BoundTypes.Count, injectTypes.Count, entries.Count, warnings.Count);

            return new ZenjectScanResult(entries, warnings, report);
        }

        private static HashSet<Assembly> CollectAssemblies(IEnumerable<Type> types)
        {
            HashSet<Assembly> assemblies = new HashSet<Assembly>();

            foreach (Type type in types)
            {
                if (type?.Assembly != null)
                {
                    assemblies.Add(type.Assembly);
                }
            }

            return assemblies;
        }

        private static void AddRuntimeType(HashSet<TypeIdentifier> entries, Type type)
        {
            if (type == null)
            {
                return;
            }

            entries.Add(new TypeIdentifierFromRuntime(type).Identifier);
        }

        private static string BuildLinkerXml(IReadOnlyCollection<TypeIdentifier> entries)
        {
            XDocument document = new XDocument(new XElement("linker"));
            XElement linker = document.Root;

            Dictionary<string, XElement> assemblies = new Dictionary<string, XElement>(StringComparer.Ordinal);

            foreach (TypeIdentifier id in entries)
            {
                if (id == null || string.IsNullOrEmpty(id.AssemblyName) || string.IsNullOrEmpty(id.TypeFullname))
                {
                    continue;
                }

                if (!assemblies.TryGetValue(id.AssemblyName, out XElement assemblyElement))
                {
                    assemblyElement = new XElement("assembly", new XAttribute("fullname", id.AssemblyName));
                    assemblies.Add(id.AssemblyName, assemblyElement);
                    linker.Add(assemblyElement);
                }

                bool exists = false;
                foreach (XElement child in assemblyElement.Elements("type"))
                {
                    if (string.Equals(child.Attribute("fullname")?.Value, id.TypeFullname, StringComparison.Ordinal))
                    {
                        exists = true;
                        break;
                    }
                }

                if (exists)
                {
                    continue;
                }

                assemblyElement.Add(new XElement(
                    "type",
                    new XAttribute("fullname", id.TypeFullname),
                    new XAttribute("preserve", "all")));
            }

            return LinkXmlBuilder.Serialize(document);
        }

        private static string BuildReport(
            int rootedInstallers,
            int reachableInstallers,
            int boundTypes,
            int supplementaryInjectTypes,
            int totalEntries,
            int warningCount)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Zenject merge:");
            builder.AppendLine($"  Rooted installers: {rootedInstallers}");
            builder.AppendLine($"  Reachable installers (incl. transitive Install<T>): {reachableInstallers}");
            builder.AppendLine($"  Bound types from IL analysis: {boundTypes}");
            builder.AppendLine($"  [Inject] supplementary types: {supplementaryInjectTypes}");
            builder.AppendLine($"  Total preserved entries: {totalEntries}");
            builder.AppendLine($"  Warnings: {warningCount}");
            builder.AppendLine($"Output pending: press Generate link.xml to write {LinkXmlWriter.DefaultPath}");

            return builder.ToString();
        }
    }

    internal sealed class ZenjectScanResult
    {
        public IReadOnlyCollection<TypeIdentifier> LinkEntries { get; }
        public IReadOnlyList<string> Warnings { get; }
        public string Report { get; }

        public ZenjectScanResult(IReadOnlyCollection<TypeIdentifier> linkEntries, IReadOnlyList<string> warnings, string report)
        {
            LinkEntries = linkEntries ?? Array.Empty<TypeIdentifier>();
            Warnings = warnings ?? Array.Empty<string>();
            Report = report ?? string.Empty;
        }
    }

    internal sealed class TypeIdentifierFromRuntime
    {
        public TypeIdentifier Identifier { get; }

        public TypeIdentifierFromRuntime(Type type)
        {
            string assemblyName = type.Assembly.GetName().Name;
            string fullname = type.FullName?.Replace('+', '/') ?? type.Name;

            int genericMark = fullname.IndexOf('[');
            if (genericMark >= 0)
            {
                fullname = fullname.Substring(0, genericMark);
            }

            Identifier = TypeIdentifier.From(assemblyName, fullname);
        }
    }
}
#endif
