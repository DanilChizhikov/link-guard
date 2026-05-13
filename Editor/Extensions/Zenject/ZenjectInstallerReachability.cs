#if LINKGUARD_ZENJECT_ENABLED
using System;
using System.Collections.Generic;
using ZN = global::Zenject;

namespace DTech.LinkGuard.Editor.Zenject
{
    internal static class ZenjectInstallerReachability
    {
        public static ZenjectReachableInstallers Expand(
            IReadOnlyCollection<Type> roots,
            Action<string, float> reportProgress = null)
        {
            HashSet<Type> reachable = new HashSet<Type>();
            List<string> warnings = new List<string>();

            if (roots == null)
            {
                return new ZenjectReachableInstallers(reachable, warnings);
            }

            Queue<Type> queue = new Queue<Type>();

            foreach (Type root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                if (reachable.Add(root))
                {
                    queue.Enqueue(root);
                }
            }

            int processed = 0;

            while (queue.Count > 0)
            {
                Type current = queue.Dequeue();
                processed++;

                reportProgress?.Invoke(
                    $"Expanding installer graph ({processed} visited, {queue.Count} pending)",
                    0.5f);

                ZenjectExtractionResult result = ZenjectIlBindingExtractor.Extract(new[] { current }, warnings);

                foreach (Type edge in result.InstallEdges)
                {
                    if (edge == null)
                    {
                        continue;
                    }

                    if (!IsInstallerType(edge))
                    {
                        // Container.Install<T>() can also be used for non-installer composites in some
                        // patterns; we still add them so they are not stripped, but only follow the
                        // graph through actual installers.
                        reachable.Add(edge);
                        continue;
                    }

                    if (reachable.Add(edge))
                    {
                        queue.Enqueue(edge);
                    }
                }
            }

            return new ZenjectReachableInstallers(reachable, warnings);
        }

        private static bool IsInstallerType(Type type)
        {
            return typeof(ZN.IInstaller).IsAssignableFrom(type);
        }
    }

    internal sealed class ZenjectReachableInstallers
    {
        public IReadOnlyCollection<Type> InstallerTypes { get; }
        public IReadOnlyList<string> Warnings { get; }

        public ZenjectReachableInstallers(IReadOnlyCollection<Type> installerTypes, IReadOnlyList<string> warnings)
        {
            InstallerTypes = installerTypes ?? Array.Empty<Type>();
            Warnings = warnings ?? Array.Empty<string>();
        }
    }
}
#endif
