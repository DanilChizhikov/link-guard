using System;
using System.Collections.Generic;
using System.Linq;

namespace DTech.LinkGuard.Editor
{
    /// <summary>
    /// <see cref="IProjectTypeSource"/> backed by <see cref="AssemblyScanner"/> results. Entries
    /// and types that only exist because they were imported from a link.xml are dropped, so the
    /// source describes real code and nothing else.
    /// </summary>
    internal sealed class ScannedProjectTypeSource : IProjectTypeSource
    {
        private readonly Dictionary<string, AssemblyEntry> _byName;
        private readonly Dictionary<string, IReadOnlyList<NamespaceEntry>> _namespacesByName;

        public IReadOnlyList<string> AssemblyNames { get; }

        public ScannedProjectTypeSource(IReadOnlyList<AssemblyEntry> entries)
        {
            IReadOnlyList<AssemblyEntry> source = entries ?? Array.Empty<AssemblyEntry>();

            _byName = source
                .Where(e => e != null && !string.IsNullOrEmpty(e.Name) && e.Source != AssemblySource.LinkXml)
                .GroupBy(e => e.Name, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            _namespacesByName = _byName.ToDictionary(
                pair => pair.Key,
                pair => CollectRealNamespaces(pair.Value),
                StringComparer.Ordinal);

            AssemblyNames = _byName.Keys.OrderBy(n => n, StringComparer.Ordinal).ToList();
        }

        private static IReadOnlyList<NamespaceEntry> CollectRealNamespaces(AssemblyEntry entry)
        {
            List<NamespaceEntry> namespaces = new List<NamespaceEntry>(entry.Namespaces.Count);

            foreach (NamespaceEntry ns in entry.Namespaces)
            {
                List<TypeEntry> types = ns.Types.Where(t => !t.IsSynthetic).ToList();

                if (types.Count == 0)
                {
                    continue;
                }

                namespaces.Add(types.Count == ns.Types.Count ? ns : new NamespaceEntry(ns.Fullname, types));
            }

            return namespaces;
        }

        public static ScannedProjectTypeSource Create(Action<string, float> reportProgress = null)
        {
            return new ScannedProjectTypeSource(AssemblyScanner.Scan(reportProgress));
        }

        public bool IsProjectAssembly(string assemblyName)
        {
            return !string.IsNullOrEmpty(assemblyName)
                && _byName.TryGetValue(assemblyName, out AssemblyEntry entry)
                && entry.Source == AssemblySource.Project;
        }

        public bool TryGetNamespaces(string assemblyName, out IReadOnlyList<NamespaceEntry> namespaces)
        {
            namespaces = Array.Empty<NamespaceEntry>();

            if (string.IsNullOrEmpty(assemblyName)
                || !_namespacesByName.TryGetValue(assemblyName, out IReadOnlyList<NamespaceEntry> entryNamespaces))
            {
                return false;
            }

            namespaces = entryNamespaces;
            return true;
        }
    }
}
