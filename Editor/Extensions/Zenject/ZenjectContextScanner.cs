#if LINKGUARD_ZENJECT_ENABLED
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZN = global::Zenject;
#if LINKGUARD_ADDRESSABLES_ENABLED
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

namespace DTech.LinkGuard.Editor.Zenject
{
    internal static class ZenjectContextScanner
    {
        public static ZenjectRootedSet ScanRoots(Action<string, float> reportProgress = null)
        {
            HashSet<Type> installerTypes = new HashSet<Type>();
            List<string> warnings = new List<string>();

            ScanScenes(installerTypes, warnings, reportProgress);
            ScanProjectContext(installerTypes, warnings);
            ScanPrefabsForContexts(installerTypes, warnings, reportProgress);

            int ignoredInstallers = RemoveIgnoredInstallers(installerTypes);

            return new ZenjectRootedSet(installerTypes, warnings, ignoredInstallers);
        }

        private static void ScanScenes(
            HashSet<Type> installerTypes,
            List<string> warnings,
            Action<string, float> reportProgress)
        {
            HashSet<string> scenePaths = new HashSet<string>(StringComparer.Ordinal);

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene == null || !scene.enabled || string.IsNullOrEmpty(scene.path))
                {
                    continue;
                }

                scenePaths.Add(scene.path);
            }

#if LINKGUARD_ADDRESSABLES_ENABLED
            CollectAddressableScenes(scenePaths, warnings);
#endif

            int index = 0;
            int total = scenePaths.Count;

            foreach (string path in scenePaths)
            {
                index++;
                reportProgress?.Invoke($"Scanning scene {index}/{total}: {Path.GetFileName(path)}", 0.05f + 0.35f * index / Math.Max(1, total));
                ProcessScene(path, installerTypes, warnings);
            }
        }

#if LINKGUARD_ADDRESSABLES_ENABLED
        private static void CollectAddressableScenes(HashSet<string> scenePaths, List<string> warnings)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

            if (settings == null)
            {
                return;
            }

            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null)
                {
                    continue;
                }

                foreach (AddressableAssetEntry entry in group.entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.AssetPath))
                    {
                        continue;
                    }

                    if (!entry.AssetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    scenePaths.Add(entry.AssetPath);
                }
            }
        }
#endif

        private static void ProcessScene(string path, HashSet<Type> installerTypes, List<string> warnings)
        {
            if (!File.Exists(path))
            {
                warnings.Add($"Scene file not found: {path}");
                return;
            }

            Scene loadedScene = default;
            bool weOpened = false;

            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                Scene candidate = EditorSceneManager.GetSceneAt(i);
                if (string.Equals(candidate.path, path, StringComparison.Ordinal))
                {
                    loadedScene = candidate;
                    break;
                }
            }

            if (!loadedScene.IsValid())
            {
                try
                {
                    loadedScene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    weOpened = true;
                }
                catch (Exception ex)
                {
                    warnings.Add($"Failed to open scene '{path}': {ex.Message}");
                    return;
                }
            }

            try
            {
                foreach (GameObject root in loadedScene.GetRootGameObjects())
                {
                    foreach (ZN.Context context in root.GetComponentsInChildren<ZN.Context>(true))
                    {
                        if (context == null)
                        {
                            continue;
                        }

                        ExtractInstallers(context, installerTypes, warnings);
                    }
                }
            }
            finally
            {
                if (weOpened && loadedScene.IsValid())
                {
                    EditorSceneManager.CloseScene(loadedScene, true);
                }
            }
        }

        private static void ScanProjectContext(HashSet<Type> installerTypes, List<string> warnings)
        {
            ZN.ProjectContext projectContext = Resources.Load<ZN.ProjectContext>(ZN.ProjectContext.ProjectContextResourcePath);

            if (projectContext == null)
            {
                return;
            }

            ExtractInstallers(projectContext, installerTypes, warnings);
        }

        private static void ScanPrefabsForContexts(
            HashSet<Type> installerTypes,
            List<string> warnings,
            Action<string, float> reportProgress)
        {
            string[] guids = AssetDatabase.FindAssets("t:GameObject");
            int index = 0;

            foreach (string guid in guids)
            {
                index++;

                if (index % 32 == 0)
                {
                    reportProgress?.Invoke(
                        $"Scanning prefabs for GameObjectContext {index}/{guids.Length}",
                        0.4f + 0.3f * index / Math.Max(1, guids.Length));
                }

                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                ZN.Context[] contexts = prefab.GetComponentsInChildren<ZN.Context>(true);
                if (contexts == null || contexts.Length == 0)
                {
                    continue;
                }

                foreach (ZN.Context context in contexts)
                {
                    if (context == null)
                    {
                        continue;
                    }

                    ExtractInstallers(context, installerTypes, warnings);
                }
            }
        }

        private static void ExtractInstallers(ZN.Context context, HashSet<Type> installerTypes, List<string> warnings)
        {
            try
            {
                if (context.Installers != null)
                {
                    foreach (ZN.MonoInstaller installer in context.Installers)
                    {
                        AddType(installer, installerTypes);
                    }
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Reading Installers on '{context.GetType().Name}' failed: {ex.Message}");
            }

            try
            {
                if (context.InstallerPrefabs != null)
                {
                    foreach (ZN.MonoInstaller installer in context.InstallerPrefabs)
                    {
                        AddType(installer, installerTypes);
                    }
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Reading InstallerPrefabs on '{context.GetType().Name}' failed: {ex.Message}");
            }

            try
            {
                if (context.ScriptableObjectInstallers != null)
                {
                    foreach (ZN.ScriptableObjectInstaller installer in context.ScriptableObjectInstallers)
                    {
                        AddType(installer, installerTypes);
                    }
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Reading ScriptableObjectInstallers on '{context.GetType().Name}' failed: {ex.Message}");
            }

            try
            {
                if (context.NormalInstallerTypes != null)
                {
                    foreach (Type normalType in context.NormalInstallerTypes)
                    {
                        if (normalType != null)
                        {
                            installerTypes.Add(normalType);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Reading NormalInstallerTypes on '{context.GetType().Name}' failed: {ex.Message}");
            }
        }

        private static void AddType(UnityEngine.Object obj, HashSet<Type> installerTypes)
        {
            if (obj == null)
            {
                return;
            }

            installerTypes.Add(obj.GetType());
        }

        private static int RemoveIgnoredInstallers(HashSet<Type> installerTypes)
        {
            int ignoredInstallers = 0;

            installerTypes.RemoveWhere(type =>
            {
                bool ignored = ZenjectIgnoreFilter.IsIgnored(type);
                if (ignored)
                {
                    ignoredInstallers++;
                }

                return ignored;
            });

            return ignoredInstallers;
        }
    }

    internal sealed class ZenjectRootedSet
    {
        public IReadOnlyCollection<Type> InstallerTypes { get; }
        public IReadOnlyList<string> Warnings { get; }
        public int IgnoredInstallerCount { get; }

        public ZenjectRootedSet(
            IReadOnlyCollection<Type> installerTypes,
            IReadOnlyList<string> warnings,
            int ignoredInstallerCount)
        {
            InstallerTypes = installerTypes ?? Array.Empty<Type>();
            Warnings = warnings ?? Array.Empty<string>();
            IgnoredInstallerCount = ignoredInstallerCount;
        }
    }
}
#endif
