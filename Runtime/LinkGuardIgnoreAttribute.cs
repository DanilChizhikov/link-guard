using System;

namespace DTech.LinkGuard
{
    /// <summary>
    /// Marks a class so Link Guard excludes it from Zenject installer discovery,
    /// keeping it out of the generated link.xml preservation set.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class LinkGuardIgnoreAttribute : Attribute
    {
    }
}
