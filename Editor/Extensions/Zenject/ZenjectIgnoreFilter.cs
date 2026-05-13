#if LINKGUARD_ZENJECT_ENABLED
using System;
using DTech.LinkGuard;

namespace DTech.LinkGuard.Editor.Zenject
{
    internal static class ZenjectIgnoreFilter
    {
        public static bool IsIgnored(Type type)
        {
            if (type == null)
            {
                return false;
            }

            try
            {
                return type.IsDefined(typeof(LinkGuardIgnoreAttribute), true);
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif
