namespace DTech.LinkGuard.Editor
{
    /// <summary>
    /// A link.xml entry kept during validation without verification, because its
    /// presence in the build could not be determined.
    /// </summary>
    public sealed class LinkXmlValidationSkippedEntry
    {
        /// <summary>Assembly name of the kept entry.</summary>
        public string AssemblyName { get; }

        /// <summary>Type full name of the kept entry; empty for assembly-level entries.</summary>
        public string TypeName { get; }

        /// <summary>Why the entry was kept without verification.</summary>
        public string Reason { get; }

        /// <summary>
        /// Creates a skipped (kept-unverified) entry.
        /// </summary>
        /// <param name="assemblyName">Assembly name of the entry.</param>
        /// <param name="typeName">Type full name, or empty for an assembly entry.</param>
        /// <param name="reason">Why the entry was kept without verification.</param>
        public LinkXmlValidationSkippedEntry(string assemblyName, string typeName, string reason)
        {
            AssemblyName = assemblyName ?? string.Empty;
            TypeName = typeName ?? string.Empty;
            Reason = reason ?? string.Empty;
        }
    }
}
