namespace DTech.LinkGuard.Editor
{
    public class GlobalTypeEntry
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
