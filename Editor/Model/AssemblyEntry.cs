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
        public List<GlobalTypeEntry> GlobalTypes { get; }
        public bool IgnoreIfMissing { get; set; }
        public bool HasNamespaces => Namespaces.Count > 0;
        public bool HasGlobalTypes => GlobalTypes.Count > 0;
        public bool IsAllGlobalTypesSelected => GlobalTypes.Count > 0 && GlobalTypes.All(t => t.IsSelected);
        public bool IsAssemblySelected { get; set; }
        public bool ProducesEntry => IsAssemblySelected || IsAnySelected;
        
        private bool IsAnyNamespaceSelected => Namespaces.Any(ns => ns.IsSelected);
        private bool IsAnyGlobalTypeSelected => GlobalTypes.Any(t => t.IsSelected);
        private bool IsAnySelected => IsAnyNamespaceSelected || IsAnyGlobalTypeSelected;

        public AssemblyEntry(string name,
            AssemblySource source,
            string originPath,
            IEnumerable<string> namespaces,
            IEnumerable<string> globalTypes)
        {
            Name = name;
            Source = source;
            OriginPath = originPath;
            Namespaces = namespaces == null
                ? new List<NamespaceEntry>()
                : namespaces
                    .Where(ns => !string.IsNullOrEmpty(ns))
                    .Distinct()
                    .OrderBy(ns => ns)
                    .Select(ns => new NamespaceEntry(ns))
                    .ToList();

            GlobalTypes = globalTypes == null
                ? new List<GlobalTypeEntry>()
                : globalTypes
                    .Where(t => !string.IsNullOrEmpty(t))
                    .Distinct()
                    .OrderBy(t => t)
                    .Select(t => new GlobalTypeEntry(t))
                    .ToList();
        }

        public void SelectAll(bool value)
        {
            IsAssemblySelected = value;
            foreach (NamespaceEntry ns in Namespaces)
            {
                ns.IsSelected = value;
            }

            foreach (GlobalTypeEntry t in GlobalTypes)
            {
                t.IsSelected = value;
            }
        }
    }
}
