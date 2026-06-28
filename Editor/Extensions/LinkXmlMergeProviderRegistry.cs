using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DTech.LinkGuard.Editor
{
    internal static class LinkXmlMergeProviderRegistry
    {
        public static IReadOnlyList<ILinkXmlMergeProvider> Discover()
        {
            List<ILinkXmlMergeProvider> providers = new List<ILinkXmlMergeProvider>();

            TypeCache.TypeCollection candidates = TypeCache.GetTypesDerivedFrom<ILinkXmlMergeProvider>();

            foreach (Type type in candidates)
            {
                if (type == null || type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    continue;
                }

                ILinkXmlMergeProvider provider = TryCreate(type);

                if (provider == null)
                {
                    continue;
                }

                providers.Add(provider);
            }

            return providers
                .OrderBy(p => p.Id, StringComparer.Ordinal)
                .ToList();
        }

        private static ILinkXmlMergeProvider TryCreate(Type type)
        {
            try
            {
                return (ILinkXmlMergeProvider)Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[LinkXmlGenerator] Failed to instantiate merge provider '{type.FullName}': {ex.Message}");
                return null;
            }
        }
    }
}
