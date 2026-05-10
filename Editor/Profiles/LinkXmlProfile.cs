using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor
{
    [Serializable]
    internal sealed class LinkXmlProfile
    {
        public int Version = 2;
        public List<LinkXmlSelection> Selections = new();
    }
}
