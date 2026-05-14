using System;

namespace DTech.LinkGuard
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class LinkGuardIgnoreAttribute : Attribute
    {
    }
}
