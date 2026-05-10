namespace DTech.LinkGuard.Editor
{
    internal sealed class MethodEntry
    {
        public string Name { get; }
        public string Signature { get; }
        public bool IsConstructor { get; }
        public bool IsSynthetic { get; }
        public bool IsSelected { get; set; }

        public MethodEntry(string name, string signature, bool isConstructor, bool isSynthetic = false)
        {
            Name = name;
            Signature = signature;
            IsConstructor = isConstructor;
            IsSynthetic = isSynthetic;
            IsSelected = false;
        }
    }
}
