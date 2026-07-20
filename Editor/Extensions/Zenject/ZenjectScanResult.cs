#if LINKGUARD_ZENJECT_ENABLED
using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor.Zenject
{
    internal sealed class ZenjectScanResult
    {
        public IReadOnlyCollection<TypeIdentifier> LinkEntries { get; }
        public IReadOnlyList<string> Warnings { get; }
        public string Report { get; }
        public int ReachableInstallerCount { get; }
        public int IgnoredInstallerCount { get; }

        public ZenjectScanResult(
            IReadOnlyCollection<TypeIdentifier> linkEntries,
            IReadOnlyList<string> warnings,
            string report,
            int reachableInstallerCount,
            int ignoredInstallerCount)
        {
            LinkEntries = linkEntries ?? Array.Empty<TypeIdentifier>();
            Warnings = warnings ?? Array.Empty<string>();
            Report = report ?? string.Empty;
            ReachableInstallerCount = reachableInstallerCount;
            IgnoredInstallerCount = ignoredInstallerCount;
        }
    }
}
#endif
