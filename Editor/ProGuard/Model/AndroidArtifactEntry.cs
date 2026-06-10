using System;
using System.Collections.Generic;
using System.Linq;

namespace DTech.LinkGuard.Editor.ProGuard
{
    internal sealed class AndroidArtifactEntry
    {
        public string Name { get; }
        public AndroidArtifactSource Source { get; }
        public string OriginPath { get; }
        public List<JavaPackageEntry> Packages { get; }
        public bool IsArtifactSelected { get; set; }
        public bool HasPackages => Packages.Count > 0;
        public IEnumerable<JavaClassEntry> Classes => Packages.SelectMany(p => p.Classes);
        public int ClassCount => Packages.Sum(p => p.Classes.Count);
        public int SelectedClassCount => Classes.Count(c => c.IsSelected);
        public bool ProducesEntry => IsArtifactSelected || Packages.Any(p => p.ProducesEntry);

        public AndroidArtifactEntry(string name,
            AndroidArtifactSource source,
            string originPath,
            IEnumerable<JavaClassEntry> classes)
        {
            Name = name;
            Source = source;
            OriginPath = originPath;

            List<JavaClassEntry> classList = classes == null
                ? new List<JavaClassEntry>()
                : classes
                    .Where(c => c != null)
                    .GroupBy(c => c.Fullname, StringComparer.Ordinal)
                    .Select(MergeDuplicates)
                    .OrderBy(c => c.Fullname, StringComparer.Ordinal)
                    .ToList();

            Packages = classList
                .GroupBy(c => c.Package, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new JavaPackageEntry(g.Key, g))
                .ToList();
        }

        public void SelectAll(bool value)
        {
            IsArtifactSelected = value;
            foreach (JavaPackageEntry package in Packages)
            {
                package.IsSelected = value;
            }
        }

        private static JavaClassEntry MergeDuplicates(IGrouping<string, JavaClassEntry> group)
        {
            JavaClassEntry first = group.First();
            first.HasInnerClasses = group.Any(c => c.HasInnerClasses);
            return first;
        }
    }
}
