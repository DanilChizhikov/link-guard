using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DTech.LinkGuard.Editor.ProGuard
{
    internal static class AndroidArtifactScanner
    {
        private static readonly Regex PackageRegex =
            new Regex(@"(?m)^\s*package\s+([A-Za-z_][\w.]*)", RegexOptions.Compiled);

        private static readonly Regex TypeRegex =
            new Regex(@"\b(?:class|interface|enum|object)\s+([A-Za-z_]\w*)", RegexOptions.Compiled);

        public static List<AndroidArtifactEntry> Scan(Action<string, float> reportProgress = null)
        {
            string root = Normalize(Directory.GetCurrentDirectory());

            List<string> searchRoots = new[] { "Assets", "Packages" }
                .Select(d => Path.Combine(root, d))
                .Where(Directory.Exists)
                .ToList();

            List<AndroidArtifactEntry> result = new List<AndroidArtifactEntry>();

            if (searchRoots.Count == 0)
            {
                return result;
            }

            reportProgress?.Invoke("Scanning Android artifacts...", 0.1f);

            List<string> androidLibDirs = CollectDirectories(searchRoots, "*.androidlib");
            List<string> aarFiles = CollectFiles(searchRoots, "*.aar");
            List<string> jarFiles = CollectFiles(searchRoots, "*.jar");
            List<string> sourceFiles = CollectFiles(searchRoots, "*.java")
                .Concat(CollectFiles(searchRoots, "*.kt"))
                .ToList();

            List<string> normalizedLibDirs = androidLibDirs.Select(Normalize).ToList();

            bool IsUnderLib(string path)
            {
                string normalized = Normalize(path);
                return normalizedLibDirs.Any(lib =>
                    normalized.StartsWith(lib + "/", StringComparison.OrdinalIgnoreCase));
            }

            reportProgress?.Invoke("Scanning Android libraries...", 0.3f);

            foreach (string lib in androidLibDirs)
            {
                List<JavaClassEntry> classes = new List<JavaClassEntry>();

                foreach (string jar in Directory.GetFiles(lib, "*.jar", SearchOption.AllDirectories))
                {
                    AddArchiveClasses(jar, classes);
                }

                foreach (string aar in Directory.GetFiles(lib, "*.aar", SearchOption.AllDirectories))
                {
                    AddArchiveClasses(aar, classes);
                }

                AddSourceClasses(Directory.GetFiles(lib, "*.java", SearchOption.AllDirectories), classes);
                AddSourceClasses(Directory.GetFiles(lib, "*.kt", SearchOption.AllDirectories), classes);

                if (classes.Count == 0)
                {
                    continue;
                }

                result.Add(new AndroidArtifactEntry(
                    Path.GetFileName(lib),
                    AndroidArtifactSource.AndroidLib,
                    ToProjectRelative(lib, root),
                    classes));
            }

            reportProgress?.Invoke("Scanning .aar plugins...", 0.55f);

            foreach (string aar in aarFiles)
            {
                if (IsUnderLib(aar))
                {
                    continue;
                }

                List<JavaClassEntry> classes = new List<JavaClassEntry>();
                AddArchiveClasses(aar, classes);

                if (classes.Count == 0)
                {
                    continue;
                }

                result.Add(new AndroidArtifactEntry(
                    Path.GetFileName(aar),
                    AndroidArtifactSource.Aar,
                    ToProjectRelative(aar, root),
                    classes));
            }

            reportProgress?.Invoke("Scanning .jar plugins...", 0.75f);

            foreach (string jar in jarFiles)
            {
                if (IsUnderLib(jar))
                {
                    continue;
                }

                List<JavaClassEntry> classes = new List<JavaClassEntry>();
                AddArchiveClasses(jar, classes);

                if (classes.Count == 0)
                {
                    continue;
                }

                result.Add(new AndroidArtifactEntry(
                    Path.GetFileName(jar),
                    AndroidArtifactSource.Jar,
                    ToProjectRelative(jar, root),
                    classes));
            }

            reportProgress?.Invoke("Scanning Android sources...", 0.9f);

            List<string> looseSources = sourceFiles
                .Where(s => !IsUnderLib(s) && IsAndroidPath(s))
                .ToList();

            if (looseSources.Count > 0)
            {
                List<JavaClassEntry> classes = new List<JavaClassEntry>();
                AddSourceClasses(looseSources, classes);

                if (classes.Count > 0)
                {
                    result.Add(new AndroidArtifactEntry(
                        "Android sources",
                        AndroidArtifactSource.JavaSource,
                        "Assets/Plugins/Android",
                        classes));
                }
            }

            result.Sort((a, b) =>
            {
                int cmp = ((int)a.Source).CompareTo((int)b.Source);
                return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
            });

            reportProgress?.Invoke("Scanning Android artifacts...", 1f);

            return result;
        }

        private static void AddArchiveClasses(string archivePath, List<JavaClassEntry> classes)
        {
            foreach (string entryPath in ArchiveClassReader.ReadClassEntryPaths(archivePath))
            {
                if (JavaClassNameResolver.TryResolveClassEntry(entryPath, out ResolvedJavaClass resolved))
                {
                    classes.Add(new JavaClassEntry(
                        resolved.Package,
                        resolved.Fullname,
                        resolved.SimpleName,
                        resolved.IsInner));
                }
            }
        }

        private static void AddSourceClasses(IEnumerable<string> sourcePaths, List<JavaClassEntry> classes)
        {
            foreach (string path in sourcePaths)
            {
                string content;

                try
                {
                    content = File.ReadAllText(path);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LinkGuard] [proguard] Failed to read source '{path}': {ex.Message}");
                    continue;
                }

                Match packageMatch = PackageRegex.Match(content);
                string package = packageMatch.Success ? packageMatch.Groups[1].Value : string.Empty;

                HashSet<string> typeNames = new HashSet<string>(StringComparer.Ordinal);

                foreach (Match typeMatch in TypeRegex.Matches(content))
                {
                    typeNames.Add(typeMatch.Groups[1].Value);
                }

                foreach (string typeName in typeNames)
                {
                    string fullname = string.IsNullOrEmpty(package) ? typeName : package + "." + typeName;
                    classes.Add(new JavaClassEntry(package, fullname, typeName));
                }
            }
        }

        private static List<string> CollectFiles(IEnumerable<string> roots, string pattern)
        {
            List<string> files = new List<string>();

            foreach (string root in roots)
            {
                files.AddRange(Directory.GetFiles(root, pattern, SearchOption.AllDirectories));
            }

            return files;
        }

        private static List<string> CollectDirectories(IEnumerable<string> roots, string pattern)
        {
            List<string> directories = new List<string>();

            foreach (string root in roots)
            {
                directories.AddRange(Directory.GetDirectories(root, pattern, SearchOption.AllDirectories));
            }

            return directories;
        }

        private static bool IsAndroidPath(string path)
        {
            return Normalize(path).IndexOf("/android", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ToProjectRelative(string path, string normalizedRoot)
        {
            string normalized = Normalize(path);

            if (normalized.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return normalized.Substring(normalizedRoot.Length + 1);
            }

            return normalized;
        }

        private static string Normalize(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
