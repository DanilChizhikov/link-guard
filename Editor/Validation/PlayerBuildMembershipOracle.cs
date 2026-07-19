using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Assembly = System.Reflection.Assembly;
using CompilationAssembly = UnityEditor.Compilation.Assembly;

namespace DTech.LinkGuard.Editor
{
    internal sealed class PlayerBuildMembershipOracle : IBuildMembershipOracle
    {
        private readonly struct ResolvedAssembly
        {
            public Assembly Assembly { get; }
            public bool IsPlayerExact { get; }

            public ResolvedAssembly(Assembly assembly, bool isPlayerExact)
            {
                Assembly = assembly;
                IsPlayerExact = isPlayerExact;
            }
        }

        private static readonly CompilationPipeline.PrecompiledAssemblySources _precompiledSources =
            CompilationPipeline.PrecompiledAssemblySources.UserAssembly |
            CompilationPipeline.PrecompiledAssemblySources.UnityEngine |
            CompilationPipeline.PrecompiledAssemblySources.SystemAssembly;

        private readonly Dictionary<string, ResolvedAssembly> _reflectionCache = new(StringComparer.Ordinal);
        private readonly HashSet<string> _playerNames = new(StringComparer.Ordinal);
        private readonly HashSet<string> _editorNames = new(StringComparer.Ordinal);
        private readonly HashSet<string> _precompiledEditorNames = new(StringComparer.Ordinal);
        private readonly HashSet<string> _projectAsmdefNames = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _playerOutputPaths = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _precompiledPlayerPaths = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Assembly> _loadedByName = new(StringComparer.Ordinal);

        private bool _initialized;
        private bool _projectAsmdefScanIncomplete;

        public BuildPresence ResolveAssembly(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                return BuildPresence.Unknown;
            }

            EnsureInitialized();

            if (IsUnityEditorName(assemblyName) || _precompiledEditorNames.Contains(assemblyName))
            {
                return BuildPresence.Missing;
            }

            if (_playerNames.Contains(assemblyName))
            {
                return BuildPresence.Present;
            }

            if (_precompiledPlayerPaths.ContainsKey(assemblyName))
            {
                return BuildPresence.Present;
            }

            if (IsBclName(assemblyName) || IsUnityEngineName(assemblyName))
            {
                return BuildPresence.Present;
            }

            if (_editorNames.Contains(assemblyName))
            {
                return BuildPresence.Missing;
            }

            if (_projectAsmdefNames.Contains(assemblyName))
            {
                return BuildPresence.Unknown;
            }

            if (_loadedByName.ContainsKey(assemblyName))
            {
                return BuildPresence.Unknown;
            }

            if (_projectAsmdefScanIncomplete)
            {
                return BuildPresence.Unknown;
            }

            return BuildPresence.Missing;
        }

        public BuildPresence ResolveType(string assemblyName, string linkerTypeFullname)
        {
            if (string.IsNullOrEmpty(assemblyName) || string.IsNullOrEmpty(linkerTypeFullname))
            {
                return BuildPresence.Unknown;
            }

            if (linkerTypeFullname.IndexOf('<') >= 0)
            {
                return BuildPresence.Unknown;
            }

            EnsureInitialized();

            ResolvedAssembly handle = ResolveReflectionAssembly(assemblyName);
            if (handle.Assembly == null)
            {
                return BuildPresence.Unknown;
            }

            string reflectionName = linkerTypeFullname.Replace('/', '+');

            try
            {
                Type type = handle.Assembly.GetType(reflectionName, throwOnError: false);

                if (type != null)
                {
                    return BuildPresence.Present;
                }

                return handle.IsPlayerExact ? BuildPresence.Missing : BuildPresence.Unknown;
            }
            catch
            {
                return BuildPresence.Unknown;
            }
        }

        private static void BuildPrecompiledMap(Dictionary<string, string> map, CompilationPipeline.PrecompiledAssemblySources sources)
        {
            map.Clear();
            string[] paths = CompilationPipeline.GetPrecompiledAssemblyPaths(sources);
            if (paths == null)
            {
                return;
            }

            foreach (string path in paths)
            {
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                string name = Path.GetFileNameWithoutExtension(path);

                if (!string.IsNullOrEmpty(name))
                {
                    map[name] = path;
                }
            }
        }

        private static bool IsBclName(string assemblyName)
        {
            return string.Equals(assemblyName, "mscorlib", StringComparison.Ordinal)
                || string.Equals(assemblyName, "netstandard", StringComparison.Ordinal)
                || string.Equals(assemblyName, "System", StringComparison.Ordinal)
                || assemblyName.StartsWith("System.", StringComparison.Ordinal)
                || assemblyName.StartsWith("Microsoft.", StringComparison.Ordinal);
        }

        private static bool IsUnityEngineName(string assemblyName)
        {
            return string.Equals(assemblyName, "UnityEngine", StringComparison.Ordinal)
                || assemblyName.StartsWith("UnityEngine.", StringComparison.Ordinal);
        }

        private static bool IsUnityEditorName(string assemblyName)
        {
            return string.Equals(assemblyName, "UnityEditor", StringComparison.Ordinal)
                || assemblyName.StartsWith("UnityEditor.", StringComparison.Ordinal);
        }

        private ResolvedAssembly ResolveReflectionAssembly(string assemblyName)
        {
            if (_reflectionCache.TryGetValue(assemblyName, out ResolvedAssembly cached))
            {
                return cached;
            }

            ResolvedAssembly resolved;
            if (_precompiledPlayerPaths.TryGetValue(assemblyName, out string precompiled)
                && File.Exists(precompiled))
            {
                resolved = new ResolvedAssembly(LoadFromPath(assemblyName, precompiled), isPlayerExact: true);
            }
            else if (_playerOutputPaths.TryGetValue(assemblyName, out string outputPath)
                && File.Exists(outputPath))
            {
                resolved = new ResolvedAssembly(LoadFromPath(assemblyName, outputPath), isPlayerExact: false);
            }
            else if (_loadedByName.TryGetValue(assemblyName, out Assembly loaded))
            {
                resolved = new ResolvedAssembly(loaded, isPlayerExact: false);
            }
            else
            {
                resolved = default;
            }

            _reflectionCache[assemblyName] = resolved;
            return resolved;
        }

        private static Assembly LoadFromPath(string assemblyName, string path)
        {
            try
            {
                return Assembly.LoadFrom(path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LinkXmlGenerator] Failed to load assembly '{assemblyName}' from {path}: {ex.Message}");
                return null;
            }
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            CompilationAssembly[] playerAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
            CompilationAssembly[] editorAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);

            _playerNames.Clear();
            _playerNames.UnionWith(playerAssemblies.Select(a => a.name));
            _editorNames.Clear();
            _editorNames.UnionWith(editorAssemblies.Select(a => a.name));

            BuildPathMap(playerAssemblies);
            BuildPrecompiledMap(_precompiledPlayerPaths, _precompiledSources);
            var editorNames = new Dictionary<string, string>(StringComparer.Ordinal);
            BuildPrecompiledMap(editorNames, CompilationPipeline.PrecompiledAssemblySources.UnityEditor);
            _precompiledEditorNames.Clear();
            _precompiledEditorNames.UnionWith(editorNames.Keys);
            Dictionary<string, Assembly> loadedByName = AppDomain.CurrentDomain
                .GetAssemblies()
                .GroupBy(a => a.GetName().Name)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            _loadedByName.Clear();
            foreach (var item in loadedByName)
            {
                _loadedByName[item.Key] = item.Value;
            }

            LoadProjectAsmdefNames();

            _initialized = true;
        }

        private void BuildPathMap(IEnumerable<CompilationAssembly> assemblies)
        {
            _playerOutputPaths.Clear();
            foreach (CompilationAssembly assembly in assemblies)
            {
                if (!string.IsNullOrEmpty(assembly.name))
                {
                    _playerOutputPaths[assembly.name] = assembly.outputPath;
                }
            }
        }

        private void LoadProjectAsmdefNames()
        {
            _projectAsmdefNames.Clear();
            _projectAsmdefScanIncomplete = false;

            foreach (string guid in AssetDatabase.FindAssets("t:AssemblyDefinitionAsset"))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                try
                {
                    string json = File.ReadAllText(assetPath);

                    if (AssemblyDefinitionInfo.TryParse(json, out AssemblyDefinitionInfo info, out string reason))
                    {
                        _projectAsmdefNames.Add(info.name);
                        continue;
                    }

                    _projectAsmdefScanIncomplete = true;
                    Debug.LogWarning($"[LinkXmlGenerator] Failed to parse asmdef at {assetPath}: {reason}");
                }
                catch (Exception ex)
                {
                    _projectAsmdefScanIncomplete = true;
                    Debug.LogWarning($"[LinkXmlGenerator] Failed to read asmdef at {assetPath}: {ex.Message}");
                }
            }
        }
    }
}
