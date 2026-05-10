namespace DTech.LinkGuard.Editor
{
    internal sealed class NamespaceEntry
    {
        public string Fullname { get; }
        public bool IsSelected { get; set; }

        public NamespaceEntry(string fullname)
        {
            Fullname = fullname;
            IsSelected = false;
        }
    }
}
