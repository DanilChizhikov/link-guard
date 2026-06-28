using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Compilation;
using UnityEngine;
using Assembly = System.Reflection.Assembly;

namespace DTech.LinkGuard.Editor
{
    internal sealed class PrecompiledTypeResolver : IPrecompiledTypeResolver
    {
        private readonly Dictionary<string, string> _pathsByName = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Assembly> _loadedByName = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Assembly> _resolveCache = new(StringComparer.Ordinal);

        private bool _initialized;

        public bool IsKnownAssembly(string assemblyName)
        {
            if (!IsUnityEngineName(assemblyName))
            {
                return false;
            }

            EnsureInitialized();

            return _pathsByName.ContainsKey(assemblyName) || _loadedByName.ContainsKey(assemblyName);
        }

        public bool TryResolveType(string assemblyName, string linkerTypeFullname, out TypeEntry entry)
        {
            entry = null;

            if (!IsUnityEngineName(assemblyName) || string.IsNullOrEmpty(linkerTypeFullname))
            {
                return false;
            }

            if (linkerTypeFullname.IndexOf('<') >= 0)
            {
                return false;
            }

            Assembly assembly = ResolveAssembly(assemblyName);
            if (assembly == null)
            {
                return false;
            }

            string reflectionName = linkerTypeFullname.Replace('/', '+');
            Type type;

            try
            {
                type = assembly.GetType(reflectionName, throwOnError: false);
            }
            catch
            {
                return false;
            }

            return type != null && ReflectionTypeCollector.TryCreateEntry(type, out entry);
        }

        private Assembly ResolveAssembly(string assemblyName)
        {
            EnsureInitialized();

            if (_resolveCache.TryGetValue(assemblyName, out Assembly cached))
            {
                return cached;
            }

            Assembly resolved = null;

            if (_loadedByName.TryGetValue(assemblyName, out Assembly loaded))
            {
                resolved = loaded;
            }
            else if (_pathsByName.TryGetValue(assemblyName, out string path) && File.Exists(path))
            {
                try
                {
                    resolved = Assembly.LoadFrom(path);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LinkXmlGenerator] Failed to load precompiled assembly '{assemblyName}' from {path}: {ex.Message}");
                }
            }

            _resolveCache[assemblyName] = resolved;

            return resolved;
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _pathsByName.Clear();
            string[] paths = CompilationPipeline.GetPrecompiledAssemblyPaths(
                CompilationPipeline.PrecompiledAssemblySources.UnityEngine);

            if (paths != null)
            {
                foreach (string path in paths)
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        continue;
                    }

                    string name = Path.GetFileNameWithoutExtension(path);

                    if (IsUnityEngineName(name))
                    {
                        _pathsByName[name] = path;
                    }
                }
            }

            _loadedByName.Clear();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;

                if (IsUnityEngineName(name) && !_loadedByName.ContainsKey(name))
                {
                    _loadedByName[name] = assembly;
                }
            }

            _initialized = true;
        }

        private static bool IsUnityEngineName(string assemblyName)
        {
            return !string.IsNullOrEmpty(assemblyName)
                && (string.Equals(assemblyName, "UnityEngine", StringComparison.Ordinal)
                    || assemblyName.StartsWith("UnityEngine.", StringComparison.Ordinal));
        }
    }
}
