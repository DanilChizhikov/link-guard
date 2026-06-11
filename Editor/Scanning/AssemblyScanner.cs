using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.Compilation;
using UnityEngine;
using Assembly = System.Reflection.Assembly;
using CompilationAssembly = UnityEditor.Compilation.Assembly;

namespace DTech.LinkGuard.Editor
{
    internal static class AssemblyScanner
    {
        public static List<AssemblyEntry> Scan(Action<string, float> reportProgress = null)
        {
            CompilationAssembly[] compilationAssemblies =
                CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);

            List<CompilationAssembly> scanAssemblies = compilationAssemblies
                .Where(assembly => !SystemAssemblyFilter.ShouldExclude(assembly.name))
                .ToList();

            reportProgress?.Invoke("Scanning assemblies...", 0.3f);

            Dictionary<string, Assembly> loadedByName = AppDomain.CurrentDomain
                .GetAssemblies()
                .GroupBy(a => a.GetName().Name)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var sourceResolver = new AssemblySourceResolver(new KnownSdks());
            List<AssemblyEntry> result = new List<AssemblyEntry>(scanAssemblies.Count);

            for (int i = 0; i < scanAssemblies.Count; i++)
            {
                CompilationAssembly assembly = scanAssemblies[i];
                reportProgress?.Invoke(
                    $"Scanning assemblies... {i + 1}/{scanAssemblies.Count}: {assembly.name}",
                    Mathf.Lerp(0.3f, 0.95f, (float)(i + 1) / scanAssemblies.Count));

                AssemblySource source = sourceResolver.Resolve(assembly);
                List<TypeEntry> types = ReflectionTypeCollector.Collect(assembly, loadedByName);
                string originPath = ResolveOriginPath(assembly);

                result.Add(new AssemblyEntry(assembly.name, source, originPath, types));
            }

            HashSet<string> compiledNames = new HashSet<string>(
                compilationAssemblies.Select(a => a.name),
                StringComparer.Ordinal);

            result.AddRange(DisabledAssemblyScanner.Scan(compiledNames, sourceResolver, reportProgress));

            result.Sort((a, b) =>
            {
                int cmp = ((int)a.Source).CompareTo((int)b.Source);

                return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
            });

            return result;
        }

        private static string ResolveOriginPath(CompilationAssembly assembly)
        {
            if (assembly.sourceFiles == null || assembly.sourceFiles.Length == 0)
            {
                return assembly.outputPath ?? string.Empty;
            }

            string first = assembly.sourceFiles[0].Replace('\\', '/');
            int lastSlash = first.LastIndexOf('/');

            return lastSlash >= 0 ? first.Substring(0, lastSlash) : first;
        }
    }
}
