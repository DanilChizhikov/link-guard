using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor
{
    public sealed class LinkXmlValidationTypeGroup
    {
        public string AssemblyName { get; }
        public IReadOnlyList<string> TypeNames { get; }

        public LinkXmlValidationTypeGroup(string assemblyName, IReadOnlyList<string> typeNames)
        {
            AssemblyName = assemblyName ?? string.Empty;
            TypeNames = typeNames ?? Array.Empty<string>();
        }
    }
}
