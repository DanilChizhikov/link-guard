using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor
{
    /// <summary>
    /// A set of entries added to link.xml, grouped by the assembly they were added to.
    /// </summary>
    public sealed class LinkXmlSyncEntryGroup
    {
        /// <summary>Name of the assembly the entries were added to.</summary>
        public string AssemblyName { get; }

        /// <summary>Full names of the added namespaces or types.</summary>
        public IReadOnlyList<string> Names { get; }

        /// <summary>
        /// Creates an added-entry group.
        /// </summary>
        /// <param name="assemblyName">Owning assembly name.</param>
        /// <param name="names">Added namespace or type full names.</param>
        public LinkXmlSyncEntryGroup(string assemblyName, IReadOnlyList<string> names)
        {
            AssemblyName = assemblyName ?? string.Empty;
            Names = names ?? Array.Empty<string>();
        }
    }
}
