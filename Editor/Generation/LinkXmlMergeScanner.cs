using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DTech.LinkGuard.Editor
{
    internal static class LinkXmlMergeScanner
    {
        private static readonly string[] SearchRoots =
        {
            "Assets",
            "Packages"
        };

        public static IReadOnlyList<string> FindLinkXmlFiles()
        {
            List<string> paths = new List<string>();

            foreach (string root in SearchRoots)
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                paths.AddRange(Directory.EnumerateFiles(root, "link.xml", SearchOption.AllDirectories)
                    .Select(NormalizePath)
                    .Where(IsProjectOwnedPath));
            }

            return paths
                .Distinct(StringComparer.Ordinal)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
        }

        private static string NormalizePath(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, '/');
        }

        private static bool IsProjectOwnedPath(string path)
        {
            return !path.StartsWith("Library/", StringComparison.Ordinal)
                && !path.StartsWith("Temp/", StringComparison.Ordinal)
                && !path.StartsWith("obj/", StringComparison.Ordinal);
        }
    }
}
