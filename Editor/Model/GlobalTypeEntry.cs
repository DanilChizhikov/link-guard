namespace DTech.LinkGuard.Editor
{
    internal sealed class GlobalTypeEntry
    {
        public string Fullname { get; }
        public bool IsSelected { get; set; }

        public GlobalTypeEntry(string fullname)
        {
            Fullname = fullname;
            IsSelected = false;
        }
    }
}
