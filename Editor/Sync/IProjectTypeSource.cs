using System.Collections.Generic;

namespace DTech.LinkGuard.Editor
{
    /// <summary>
    /// Supplies the types currently present in the project, grouped by assembly and namespace,
    /// so that link.xml coverage can be compared against the real code base.
    /// </summary>
    internal interface IProjectTypeSource
    {
        /// <summary>Names of every scanned assembly.</summary>
        IReadOnlyList<string> AssemblyNames { get; }

        /// <summary>Whether the assembly is project code (an asmdef under Assets), not a package or SDK.</summary>
        bool IsProjectAssembly(string assemblyName);

        /// <summary>Returns the namespaces of a scanned assembly, or <c>false</c> when it was not scanned.</summary>
        bool TryGetNamespaces(string assemblyName, out IReadOnlyList<NamespaceEntry> namespaces);
    }
}
