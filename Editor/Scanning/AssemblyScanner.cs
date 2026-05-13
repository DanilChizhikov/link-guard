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

            List<AssemblyEntry> result = new List<AssemblyEntry>(scanAssemblies.Count);

            var sdks = new KnownSdks();
            for (int i = 0; i < scanAssemblies.Count; i++)
            {
                CompilationAssembly assembly = scanAssemblies[i];
                reportProgress?.Invoke(
                    $"Scanning assemblies... {i + 1}/{scanAssemblies.Count}: {assembly.name}",
                    Mathf.Lerp(0.3f, 0.95f, (float)(i + 1) / scanAssemblies.Count));

                AssemblySource source = ResolveSource(sdks, assembly);
                List<TypeEntry> types = ResolveTypes(assembly, loadedByName);
                string originPath = ResolveOriginPath(assembly);

                result.Add(new AssemblyEntry(assembly.name, source, originPath, types));
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

        private static List<TypeEntry> ResolveTypes(
            CompilationAssembly assembly,
            Dictionary<string, Assembly> loadedByName)
        {
            List<TypeEntry> types = new List<TypeEntry>();

            if (loadedByName.TryGetValue(assembly.name, out Assembly loaded))
            {
                CollectFromLoaded(loaded, types);
            }
            else if (!string.IsNullOrEmpty(assembly.outputPath) && File.Exists(assembly.outputPath))
            {
                CollectFromOutput(assembly.outputPath, types);
            }

            return types
                .GroupBy(t => t.LinkerFullname)
                .Select(g => g.First())
                .OrderBy(t => t.LinkerFullname)
                .ToList();
        }

        private static void CollectFromLoaded(Assembly assembly, List<TypeEntry> types)
        {
            try
            {
                foreach (Type type in assembly.GetTypes())
                {
                    AppendType(type, types);
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
                    AppendType(type, types);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LinkXmlGenerator] Failed to load types from {assembly.GetName().Name}: {ex.Message}");
            }
        }

        private static void AppendType(Type type, List<TypeEntry> types)
        {
            if (!ShouldIncludeType(type))
            {
                return;
            }

            string linkerFullname = GetLinkerTypeName(type);
            string displayName = string.IsNullOrEmpty(type.Namespace)
                ? linkerFullname
                : linkerFullname.Substring(type.Namespace.Length + 1);

            types.Add(new TypeEntry(
                type.Namespace,
                type.FullName,
                linkerFullname,
                displayName));
        }

        private static bool ShouldIncludeType(Type type)
        {
            if (type == null || string.IsNullOrEmpty(type.FullName))
            {
                return false;
            }

            if (type.FullName.IndexOf('<') >= 0 || type.FullName == "<Module>")
            {
                return false;
            }

            return !type.IsDefined(typeof(CompilerGeneratedAttribute), false);
        }

        private static string GetLinkerTypeName(Type type)
        {
            if (type == typeof(void))
            {
                return "System.Void";
            }

            if (type.IsByRef)
            {
                return $"{GetLinkerTypeName(type.GetElementType())}&";
            }

            if (type.IsPointer)
            {
                return $"{GetLinkerTypeName(type.GetElementType())}*";
            }

            if (type.IsArray)
            {
                return $"{GetLinkerTypeName(type.GetElementType())}{GetArrayRankSuffix(type.GetArrayRank())}";
            }

            if (type.IsGenericParameter)
            {
                return type.Name;
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                Type definition = type.GetGenericTypeDefinition();
                string definitionName = GetNonGenericLinkerTypeName(definition);
                string arguments = string.Join(",", type.GetGenericArguments().Select(GetLinkerTypeName));

                return $"{definitionName}<{arguments}>";
            }

            return GetNonGenericLinkerTypeName(type);
        }

        private static string GetNonGenericLinkerTypeName(Type type)
        {
            string fullname = type.FullName ?? type.Name;

            int genericArgumentStart = fullname.IndexOf("[[", StringComparison.Ordinal);
            if (genericArgumentStart >= 0)
            {
                fullname = fullname.Substring(0, genericArgumentStart);
            }

            return fullname.Replace('+', '/');
        }

        private static string GetArrayRankSuffix(int rank)
        {
            if (rank <= 1)
            {
                return "[]";
            }

            return $"[{new string(',', rank - 1)}]";
        }

        private static void CollectFromOutput(string outputPath, List<TypeEntry> types)
        {
            try
            {
                Assembly loaded = Assembly.LoadFrom(outputPath);
                CollectFromLoaded(loaded, types);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LinkXmlGenerator] Failed to load assembly from {outputPath}: {ex.Message}");
            }
        }
    }
}
