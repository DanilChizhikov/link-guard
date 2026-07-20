using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor
{
    [Serializable]
    internal sealed class LinkXmlSelection
    {
        public string Assembly;
        public bool PreserveAll;
        public bool IgnoreIfMissing;

        public List<string> Namespaces = new();
        public List<string> GlobalTypes = new();
        public List<string> Types = new();

        // Deserialization-only carrier for v2 profiles. v3 writers leave it empty;
        // v2 entries here are promoted to whole-type selection at load time.
        public List<LinkXmlMethodSelection> Methods = new();
    }
}
