#if LINKGUARD_ZENJECT_ENABLED
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor.Zenject
{
    /// <summary>
    /// Supplies additional scene paths to scan for Zenject scene contexts. Implement to
    /// include scenes that are not discoverable through the default project scan
    /// (for example, Addressables-managed scenes).
    /// </summary>
    public interface IZenjectScenePathProvider
    {
        /// <summary>
        /// Adds scene paths to scan for Zenject contexts.
        /// </summary>
        /// <param name="scenePaths">Set to add discovered scene paths to.</param>
        /// <param name="warnings">List to append non-fatal warnings to.</param>
        void CollectScenePaths(ISet<string> scenePaths, List<string> warnings);
    }
}
#endif
