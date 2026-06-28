namespace DTech.LinkGuard.Editor
{
    internal sealed class LinkXmlMergeInput
    {
        public string Source { get; }
        public string Xml { get; }

        public LinkXmlMergeInput(string source, string xml)
        {
            Source = source ?? string.Empty;
            Xml = xml ?? string.Empty;
        }
    }
}
