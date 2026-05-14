#if LINKGUARD_ZENJECT_ENABLED && LINKGUARD_ADDRESSABLES_ENABLED
using System;
using System.Collections.Generic;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace DTech.LinkGuard.Editor.Zenject.Addressables
{
    internal sealed class AddressablesZenjectScenePathProvider : IZenjectScenePathProvider
    {
        public void CollectScenePaths(ISet<string> scenePaths, List<string> warnings)
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
    }
}
#endif
