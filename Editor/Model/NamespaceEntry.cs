using System.Collections.Generic;
using System.Linq;

namespace DTech.LinkGuard.Editor
{
    internal sealed class NamespaceEntry
    {
        public string Fullname { get; }
        public List<TypeEntry> Types { get; }
        public bool IsSelected
        {
            get => Types.Count > 0 && Types.All(t => t.IsSelected);
            set
            {
                foreach (TypeEntry type in Types)
                {
                    type.SelectAll(value);
                }
            }
        }
        public bool ProducesEntry => Types.Any(t => t.ProducesEntry);
        public int SelectedTypeCount => Types.Count(t => t.IsSelected);

        public NamespaceEntry(string fullname, IEnumerable<TypeEntry> types)
        {
            Fullname = fullname ?? string.Empty;
            Types = types == null
                ? new List<TypeEntry>()
                : types
                    .Where(t => t != null)
                    .OrderBy(t => t.LinkerFullname)
                    .ToList();
        }
    }
}
