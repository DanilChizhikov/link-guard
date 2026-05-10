using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor.Compilation;
using UnityEngine;
using Assembly = System.Reflection.Assembly;
using CompilationAssembly = UnityEditor.Compilation.Assembly;

namespace DTech.LinkGuard.Editor
{
    internal static class AssemblyScanner
    {
        public static List<AssemblyEntry> Scan()
        {
            CompilationAssembly[] compilationAssemblies =
                CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);

            Dictionary<string, Assembly> loadedByName = AppDomain.CurrentDomain
                .GetAssemblies()
                .GroupBy(a => a.GetName().Name)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            List<AssemblyEntry> result = new List<AssemblyEntry>(compilationAssemblies.Length);

            var sdks = new KnownSdks();
            foreach (CompilationAssembly assembly in compilationAssemblies)
            {
                if (SystemAssemblyFilter.ShouldExclude(assembly.name))
                {
                    continue;
                }

                AssemblySource source = ResolveSource(sdks, assembly);
                ResolveTypes(assembly, loadedByName, out HashSet<string> namespaces, out HashSet<string> globalTypes);
                string originPath = ResolveOriginPath(assembly);

                result.Add(new AssemblyEntry(assembly.name, source, originPath, namespaces, globalTypes));
            }

            result.Sort((a, b) =>
            {
                int cmp = ((int)a.Source).CompareTo((int)b.Source);

                return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
            });

            return result;
        }

        private static AssemblySource ResolveSource(KnownSdks sdks, CompilationAssembly assembly)
        {
            if (sdks.IsSdk(assembly.name))
            {
                return AssemblySource.Sdk;
            }

            string firstSource = assembly.sourceFiles != null && assembly.sourceFiles.Length > 0
                ? assembly.sourceFiles[0].Replace('\\', '/')
                : string.Empty;

            if (firstSource.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                return AssemblySource.UpmPackage;
            }

            if (firstSource.StartsWith("Assets/Plugins/", StringComparison.OrdinalIgnoreCase))
            {
                return AssemblySource.Plugin;
            }

            if (assembly.name.StartsWith("Unity.", StringComparison.Ordinal)
                || assembly.name.StartsWith("UnityEngine.", StringComparison.Ordinal)
                || assembly.name.StartsWith("UnityEditor.", StringComparison.Ordinal))
            {
                return AssemblySource.Unity;
            }

            return AssemblySource.Project;
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

        private static void ResolveTypes(
            CompilationAssembly assembly,
            Dictionary<string, Assembly> loadedByName,
            out HashSet<string> namespaces,
            out HashSet<string> globalTypes)
        {
            namespaces = new HashSet<string>(StringComparer.Ordinal);
            globalTypes = new HashSet<string>(StringComparer.Ordinal);

            if (loadedByName.TryGetValue(assembly.name, out Assembly loaded))
            {
                CollectFromLoaded(loaded, namespaces, globalTypes);
            }
            else if (!string.IsNullOrEmpty(assembly.outputPath) && File.Exists(assembly.outputPath))
            {
                CollectFromOutput(assembly.outputPath, namespaces, globalTypes);
            }
        }

        private static void CollectFromLoaded(Assembly assembly,
            HashSet<string> namespaces, HashSet<string> globalTypes)
        {
            try
            {
                foreach (Type type in assembly.GetTypes())
                {
                    AppendType(type, namespaces, globalTypes);
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                if (ex.Types == null)
                {
                    return;
                }

                foreach (Type type in ex.Types)
                {
                    AppendType(type, namespaces, globalTypes);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LinkXmlGenerator] Failed to load types from {assembly.GetName().Name}: {ex.Message}");
            }
        }

        private static void AppendType(Type type, HashSet<string> namespaces, HashSet<string> globalTypes)
        {
            if (type == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(type.Namespace))
            {
                namespaces.Add(type.Namespace);

                return;
            }

            if (type.IsNested)
            {
                return;
            }

            string fullname = type.FullName;

            if (string.IsNullOrEmpty(fullname))
            {
                return;
            }

            if (fullname.IndexOf('<') >= 0 || fullname == "<Module>")
            {
                return;
            }

            if (type.IsDefined(typeof(CompilerGeneratedAttribute), false))
            {
                return;
            }

            globalTypes.Add(fullname);
        }

        private static void CollectFromOutput(string outputPath,
            HashSet<string> namespaces, HashSet<string> globalTypes)
        {
            try
            {
                Assembly loaded = Assembly.LoadFrom(outputPath);
                CollectFromLoaded(loaded, namespaces, globalTypes);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LinkXmlGenerator] Failed to load assembly from {outputPath}: {ex.Message}");
            }
        }
    }
}
