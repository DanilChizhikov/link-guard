using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor
{
    /// <summary>
    /// A set of type names removed from link.xml, grouped by their owning assembly.
    /// </summary>
    public sealed class LinkXmlValidationTypeGroup
    {
        /// <summary>Name of the assembly the removed types belonged to.</summary>
        public string AssemblyName { get; }

        /// <summary>Full names of the removed types.</summary>
        public IReadOnlyList<string> TypeNames { get; }

        /// <summary>
        /// Creates a removed-type group.
        /// </summary>
        /// <param name="assemblyName">Owning assembly name.</param>
        /// <param name="typeNames">Removed type full names.</param>
        public LinkXmlValidationTypeGroup(string assemblyName, IReadOnlyList<string> typeNames)
        {
            AssemblyName = assemblyName ?? string.Empty;
            TypeNames = typeNames ?? Array.Empty<string>();
        }
    }
}
