#if LINKGUARD_ZENJECT_ENABLED
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor.Zenject
{
    public interface IZenjectScenePathProvider
    {
        void CollectScenePaths(ISet<string> scenePaths, List<string> warnings);
    }
}
#endif
