using System.Collections.Generic;
using System.Linq;

namespace DTech.LinkGuard.Editor
{
    internal sealed class AssemblyEntry
    {
        public string Name { get; }
        public AssemblySource Source { get; }
        public string OriginPath { get; }
        public List<NamespaceEntry> Namespaces { get; }
        public bool IgnoreIfMissing { get; set; }
        public bool HasNamespaces => Namespaces.Count > 0;
        public IEnumerable<TypeEntry> Types => Namespaces.SelectMany(ns => ns.Types);
        public int TypeCount => Namespaces.Sum(ns => ns.Types.Count);
        public int SelectedTypeCount => Types.Count(t => t.IsSelected);
        public int SelectedMethodCount => Types.Sum(t => t.SelectedMethodCount);
        public bool IsAssemblySelected { get; set; }
        public bool ProducesEntry => IsAssemblySelected || IsAnySelected;
        
        private bool IsAnySelected => Namespaces.Any(ns => ns.ProducesEntry);

        public AssemblyEntry(string name,
            AssemblySource source,
            string originPath,
            IEnumerable<TypeEntry> types)
        {
            Name = name;
            Source = source;
            OriginPath = originPath;
            List<TypeEntry> typeList = types == null
                ? new List<TypeEntry>()
                : types
                    .Where(t => t != null)
                    .GroupBy(t => t.LinkerFullname)
                    .Select(g => g.First())
                    .OrderBy(t => t.LinkerFullname)
                    .ToList();

            Namespaces = typeList
                .GroupBy(t => t.Namespace)
                .OrderBy(g => g.Key)
                .Select(g => new NamespaceEntry(g.Key, g))
                .ToList();
        }

        public void SelectAll(bool value)
        {
            IsAssemblySelected = value;
            foreach (NamespaceEntry ns in Namespaces)
            {
                ns.IsSelected = value;
            }
        }
    }
}
