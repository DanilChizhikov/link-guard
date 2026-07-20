#if LINKGUARD_ZENJECT_ENABLED
using System;

namespace DTech.LinkGuard.Editor.Zenject
{
    internal sealed class TypeIdentifierFromRuntime
    {
        public TypeIdentifier Identifier { get; }

        public TypeIdentifierFromRuntime(Type type)
        {
            string assemblyName = type.Assembly.GetName().Name;
            string fullname = type.FullName?.Replace('+', '/') ?? type.Name;

            int genericMark = fullname.IndexOf('[');
            if (genericMark >= 0)
            {
                fullname = fullname.Substring(0, genericMark);
            }

            Identifier = TypeIdentifier.From(assemblyName, fullname);
        }
    }
}
#endif
