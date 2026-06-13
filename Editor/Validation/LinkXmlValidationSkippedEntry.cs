namespace DTech.LinkGuard.Editor
{
    public sealed class LinkXmlValidationSkippedEntry
    {
        public string AssemblyName { get; }
        public string TypeName { get; }
        public string Reason { get; }

        public LinkXmlValidationSkippedEntry(string assemblyName, string typeName, string reason)
        {
            AssemblyName = assemblyName ?? string.Empty;
            TypeName = typeName ?? string.Empty;
            Reason = reason ?? string.Empty;
        }
    }
}
