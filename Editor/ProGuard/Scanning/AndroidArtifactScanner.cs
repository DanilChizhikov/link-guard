using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using PackageSource = UnityEditor.PackageManager.PackageSource;

namespace DTech.LinkGuard.Editor.ProGuard
{
    internal static class AndroidArtifactScanner
    {
        internal readonly struct SearchRoot
        {
            public readonly string FullPath;
            public readonly string OriginPrefix;

            public SearchRoot(string fullPath, string originPrefix)
            {
                FullPath = fullPath;
                OriginPrefix = originPrefix;
            }
        }

        public static List<AndroidArtifactEntry> Scan(Action<string, float> reportProgress = null)
        {
            string root = Normalize(Directory.GetCurrentDirectory());
            List<SearchRoot> roots = CollectSearchRoots(root);

            List<string> searchRoots = roots.Select(r => r.FullPath).ToList();
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
                    ResolveStableOrigin(lib, roots, root),
                    classes));
            }

            reportProgress?.Invoke("Scanning .aar plugins...", 0.55f);

            foreach (string aar in aarFiles)
            {
                if (IsUnderLib(aar) || !IsAndroidPath(aar))
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
                    ResolveStableOrigin(aar, roots, root),
                    classes));
            }

            reportProgress?.Invoke("Scanning .jar plugins...", 0.75f);

            foreach (string jar in jarFiles)
            {
                if (IsUnderLib(jar) || !IsAndroidPath(jar))
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
                    ResolveStableOrigin(jar, roots, root),
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

                foreach (JavaSourceType sourceType in JavaSourceTypeExtractor.Extract(content, out string package))
                {
                    string fullname = string.IsNullOrEmpty(package)
                        ? sourceType.SimpleName
                        : package + "." + sourceType.SimpleName;
                    classes.Add(new JavaClassEntry(package, fullname, sourceType.SimpleName, sourceType.HasInnerClasses));
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

        private static List<SearchRoot> CollectSearchRoots(string normalizedProjectRoot)
        {
            List<SearchRoot> roots = new List<SearchRoot>();

            foreach (string folder in new[] { "Assets", "Packages" })
            {
                string fullPath = normalizedProjectRoot + "/" + folder;

                if (Directory.Exists(fullPath))
                {
                    roots.Add(new SearchRoot(fullPath, folder));
                }
            }

            string embeddedPrefix = normalizedProjectRoot + "/Packages/";

            try
            {
                foreach (PackageInfo package in PackageInfo.GetAllRegisteredPackages())
                {
                    if (package == null
                        || package.source == PackageSource.BuiltIn
                        || string.IsNullOrEmpty(package.resolvedPath)
                        || string.IsNullOrEmpty(package.name))
                    {
                        continue;
                    }

                    string resolved = Normalize(package.resolvedPath);

                    if (resolved.StartsWith(embeddedPrefix, StringComparison.OrdinalIgnoreCase)
                        || !Directory.Exists(resolved))
                    {
                        continue;
                    }

                    roots.Add(new SearchRoot(resolved, "Packages/" + package.name));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LinkGuard] [proguard] Failed to collect registered packages: {ex.Message}");
            }

            return roots;
        }
        
        internal static string ResolveStableOrigin(string path, IReadOnlyList<SearchRoot> roots, string normalizedProjectRoot)
        {
            string normalized = Normalize(path);
            SearchRoot best = default;
            bool found = false;

            foreach (SearchRoot candidate in roots)
            {
                if (!normalized.StartsWith(candidate.FullPath + "/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!found || candidate.FullPath.Length > best.FullPath.Length)
                {
                    best = candidate;
                    found = true;
                }
            }

            if (found)
            {
                return best.OriginPrefix + normalized.Substring(best.FullPath.Length);
            }

            if (normalized.StartsWith(normalizedProjectRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return normalized.Substring(normalizedProjectRoot.Length + 1);
            }

            return normalized;
        }

        private static string Normalize(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
