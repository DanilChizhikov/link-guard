#if LINKGUARD_ZENJECT_ENABLED
using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor.Zenject
{
    /// <summary>
    /// Outcome of a <see cref="ZenjectLinkXmlPatcher.Patch(string)"/> run: how many types
    /// were added to link.xml and how the installer graph was resolved.
    /// </summary>
    public sealed class ZenjectPatchReport
    {
        /// <summary>Path of the patched link.xml.</summary>
        public string Path { get; }

        /// <summary>Number of type entries added to link.xml.</summary>
        public int TypesAdded { get; }

        /// <summary>Number of types already covered by existing entries.</summary>
        public int TypesAlreadyCovered { get; }

        /// <summary>Number of reachable installers found (including transitive installs).</summary>
        public int ReachableInstallerCount { get; }

        /// <summary>Number of installers excluded via <see cref="LinkGuardIgnoreAttribute"/>.</summary>
        public int IgnoredInstallerCount { get; }

        /// <summary>Non-fatal warnings raised during the run.</summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>
        /// Creates a report without an ignored-installer count (defaults to zero).
        /// </summary>
        /// <param name="path">Path of the patched link.xml.</param>
        /// <param name="typesAdded">Number of entries added.</param>
        /// <param name="typesAlreadyCovered">Number of types already covered.</param>
        /// <param name="reachableInstallerCount">Number of reachable installers.</param>
        /// <param name="warnings">Non-fatal warnings, or <c>null</c> for none.</param>
        public ZenjectPatchReport(
            string path,
            int typesAdded,
            int typesAlreadyCovered,
            int reachableInstallerCount,
            IReadOnlyList<string> warnings)
            : this(path, typesAdded, typesAlreadyCovered, reachableInstallerCount, 0, warnings)
        {
        }

        /// <summary>
        /// Creates a Zenject patch report.
        /// </summary>
        /// <param name="path">Path of the patched link.xml.</param>
        /// <param name="typesAdded">Number of entries added.</param>
        /// <param name="typesAlreadyCovered">Number of types already covered.</param>
        /// <param name="reachableInstallerCount">Number of reachable installers.</param>
        /// <param name="ignoredInstallerCount">Number of ignored installers.</param>
        /// <param name="warnings">Non-fatal warnings, or <c>null</c> for none.</param>
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

        /// <summary>Returns a one-line summary of the Zenject patch outcome.</summary>
        /// <returns>A human-readable summary string.</returns>
        public override string ToString()
        {
            return $"ZenjectPatchReport[Path={Path}, Added={TypesAdded}, "
                + $"AlreadyCovered={TypesAlreadyCovered}, Installers={ReachableInstallerCount}, "
                + $"Ignored={IgnoredInstallerCount}, Warnings={Warnings.Count}]";
        }
    }
}
#endif
