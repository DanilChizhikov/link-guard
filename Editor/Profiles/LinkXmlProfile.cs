using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor
{
    [Serializable]
    internal sealed class LinkXmlProfile
    {
        public int Version { get; set; } = 1;
        public List<LinkXmlSelection> Selections { get; set; } = new();
    }
}
