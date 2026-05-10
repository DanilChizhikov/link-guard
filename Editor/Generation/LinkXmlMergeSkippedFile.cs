namespace DTech.LinkGuard.Editor
{
    internal sealed class LinkXmlMergeSkippedFile
    {
        public string Path { get; }
        public string Reason { get; }

        public LinkXmlMergeSkippedFile(string path, string reason)
        {
            Path = path;
            Reason = reason;
        }
    }
}
