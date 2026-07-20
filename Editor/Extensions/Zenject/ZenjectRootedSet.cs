#if LINKGUARD_ZENJECT_ENABLED
using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor.Zenject
{
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
