using System.Collections.Generic;
using System.Linq;

namespace DTech.LinkGuard.Editor.ProGuard
{
    internal sealed class JavaPackageEntry
    {
        public string Fullname { get; }
        public List<JavaClassEntry> Classes { get; }
        public bool IsSelected
        {
            get => Classes.Count > 0 && Classes.All(c => c.IsSelected);
            set
            {
                foreach (JavaClassEntry javaClass in Classes)
                {
                    javaClass.SelectAll(value);
                }
            }
        }
        public bool ProducesEntry => Classes.Any(c => c.ProducesEntry);
        public int SelectedClassCount => Classes.Count(c => c.IsSelected);

        public JavaPackageEntry(string fullname, IEnumerable<JavaClassEntry> classes)
        {
            Fullname = fullname ?? string.Empty;
            Classes = classes == null
                ? new List<JavaClassEntry>()
                : classes
                    .Where(c => c != null)
                    .OrderBy(c => c.Fullname, System.StringComparer.Ordinal)
                    .ToList();
        }
    }
}
