namespace DTech.LinkGuard.Editor
{
    internal sealed class MethodEntry
    {
        public string Name { get; }
        public string Signature { get; }
        public bool IsConstructor { get; }
        public bool IsSelected { get; set; }

        public MethodEntry(string name, string signature, bool isConstructor)
        {
            Name = name;
            Signature = signature;
            IsConstructor = isConstructor;
            IsSelected = false;
        }
    }
}
