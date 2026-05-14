#if LINKGUARD_ZENJECT_ENABLED
using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor.Zenject
{
    public sealed class ZenjectPatchReport
    {
        public string Path { get; }
        public int TypesAdded { get; }
        public int TypesAlreadyCovered { get; }
        public int ReachableInstallerCount { get; }
        public int IgnoredInstallerCount { get; }
        public IReadOnlyList<string> Warnings { get; }

        public ZenjectPatchReport(
            string path,
            int typesAdded,
            int typesAlreadyCovered,
            int reachableInstallerCount,
            IReadOnlyList<string> warnings)
            : this(path, typesAdded, typesAlreadyCovered, reachableInstallerCount, 0, warnings)
        {
        }

        public ZenjectPatchReport(
            string path,
            int typesAdded,
            int typesAlreadyCovered,
            int reachableInstallerCount,
            int ignoredInstallerCount,
            IReadOnlyList<string> warnings)
        {
            Path = path ?? string.Empty;
            TypesAdded = typesAdded;
            TypesAlreadyCovered = typesAlreadyCovered;
            ReachableInstallerCount = reachableInstallerCount;
            IgnoredInstallerCount = ignoredInstallerCount;
            Warnings = warnings ?? Array.Empty<string>();
        }

        public override string ToString()
        {
            return $"ZenjectPatchReport[Path={Path}, Added={TypesAdded}, "
                + $"AlreadyCovered={TypesAlreadyCovered}, Installers={ReachableInstallerCount}, "
                + $"Ignored={IgnoredInstallerCount}, Warnings={Warnings.Count}]";
        }
    }
}
#endif
