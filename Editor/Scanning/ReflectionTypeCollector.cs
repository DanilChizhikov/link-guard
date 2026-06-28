using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using Assembly = System.Reflection.Assembly;
using CompilationAssembly = UnityEditor.Compilation.Assembly;

namespace DTech.LinkGuard.Editor
{
    internal static class ReflectionTypeCollector
    {
        public static List<TypeEntry> Collect(
            CompilationAssembly assembly,
            IReadOnlyDictionary<string, Assembly> loadedByName)
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

        public static bool TryCreateEntry(Type type, out TypeEntry entry)
        {
            entry = null;

            if (!ShouldIncludeType(type))
            {
                return false;
            }

            string linkerFullname = TypeNameResolver.GetLinkerTypeName(type);
            string displayName = string.IsNullOrEmpty(type.Namespace)
                ? linkerFullname
                : linkerFullname.Substring(type.Namespace.Length + 1);

            entry = new TypeEntry(
                type.Namespace,
                type.FullName,
                linkerFullname,
                displayName);

            return true;
        }

        private static void AppendType(Type type, List<TypeEntry> types)
        {
            if (TryCreateEntry(type, out TypeEntry entry))
            {
                types.Add(entry);
            }
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
    }
}
