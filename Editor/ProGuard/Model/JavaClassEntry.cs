namespace DTech.LinkGuard.Editor.ProGuard
{
    internal sealed class JavaClassEntry
    {
        public string Package { get; }
        public string Fullname { get; }
        public string DisplayName { get; }
        public bool HasInnerClasses { get; set; }
        public bool IsSelected { get; set; }
        public bool ProducesEntry => IsSelected;

        public JavaClassEntry(string package, string fullname, string displayName, bool hasInnerClasses = false)
        {
            Package = package ?? string.Empty;
            Fullname = fullname;
            DisplayName = displayName;
            HasInnerClasses = hasInnerClasses;
            IsSelected = false;
        }

        public void SelectAll(bool value)
        {
            IsSelected = value;
        }
    }
}
