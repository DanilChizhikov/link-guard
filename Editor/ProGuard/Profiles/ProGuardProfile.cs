using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor.ProGuard
{
    [Serializable]
    internal sealed class ProGuardProfile
    {
        public int Version = 1;
        public List<ProGuardSelection> Selections = new();
    }
}
