using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Assembly = System.Reflection.Assembly;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using PackageSource = UnityEditor.PackageManager.PackageSource;

namespace DTech.LinkGuard.Editor
{
    internal static class PrecompiledAssemblyScanner
    {
        public static List<AssemblyEntry> Scan(
            ISet<string> existingNames,
            AssemblySourceResolver sourceResolver,
            IReadOnlyDictionary<string, Assembly> loadedByName,
            Action<string, float> reportProgress = null)
        {
            List<AssemblyEntry> result = new List<AssemblyEntry>();

            string[] paths;

            try
            {
                paths = CompilationPipeline.GetPrecompiledAssemblyPaths(
                    CompilationPipeline.PrecompiledAssemblySources.UserAssembly);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LinkXmlGenerator] Failed to collect precompiled assemblies: {ex.Message}");
                return result;
            }

            if (paths == null || paths.Length == 0)
            {
                return result;
            }

            reportProgress?.Invoke("Scanning precompiled assemblies...", 0.97f);

            foreach (string path in paths)
            {
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                string name = Path.GetFileNameWithoutExtension(path);

                if (string.IsNullOrEmpty(name)
                    || existingNames.Contains(name)
                    || SystemAssemblyFilter.ShouldExclude(name)
                    || !IsIncludedInPlayerBuild(path))
                {
                    continue;
                }

                List<TypeEntry> types = ReflectionTypeCollector.Collect(name, path, loadedByName);

                if (types.Count == 0)
                {
                    continue;
                }

                AssemblySource source = sourceResolver.Resolve(name, ToProjectRelative(path));
                result.Add(new AssemblyEntry(name, source, ToProjectRelative(path), types));
            }

            return result;
        }

        private static bool IsIncludedInPlayerBuild(string path)
        {
            PluginImporter importer = AssetImporter.GetAtPath(ResolveAssetPath(path)) as PluginImporter;

            if (importer == null)
            {
                return true;
            }

            return importer.GetCompatibleWithAnyPlatform()
                || importer.GetCompatibleWithPlatform(EditorUserBuildSettings.activeBuildTarget);
        }

        private static string ResolveAssetPath(string path)
        {
            string normalized = path.Replace('\\', '/');

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

                    string resolved = package.resolvedPath.Replace('\\', '/');

                    if (normalized.StartsWith(resolved + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        return "Packages/" + package.name + normalized.Substring(resolved.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LinkXmlGenerator] Failed to resolve package asset path for '{path}': {ex.Message}");
            }

            return ToProjectRelative(path);
        }

        private static string ToProjectRelative(string path)
        {
            string normalized = path.Replace('\\', '/');
            string projectRoot = Directory.GetCurrentDirectory().Replace('\\', '/') + "/";

            return normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
                ? normalized.Substring(projectRoot.Length)
                : normalized;
        }
    }
}
