using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DTech.LinkGuard.Editor
{
    internal static class GeneratorTabRegistry
    {
        public static IReadOnlyList<IGeneratorTab> Discover()
        {
            List<IGeneratorTab> tabs = new List<IGeneratorTab>();

            TypeCache.TypeCollection candidates = TypeCache.GetTypesDerivedFrom<IGeneratorTab>();

            foreach (Type type in candidates)
            {
                if (type == null || type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                IGeneratorTab tab = TryCreate(type);

                if (tab == null)
                {
                    continue;
                }

                tabs.Add(tab);
            }

            return tabs
                .OrderBy(t => t.Order)
                .ThenBy(t => t.TabLabel, StringComparer.Ordinal)
                .ToList();
        }

        private static IGeneratorTab TryCreate(Type type)
        {
            try
            {
                return (IGeneratorTab)Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LinkGuard] Failed to instantiate generator tab '{type.FullName}': {ex.Message}");
                return null;
            }
        }
    }
}
