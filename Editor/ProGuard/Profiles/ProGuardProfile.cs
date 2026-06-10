using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor.ProGuard
{
    [Serializable]
    internal sealed class ProGuardSelection
    {
        public string Artifact;
        public bool KeepAll;
        public List<string> Packages = new();
        public List<string> Classes = new();
    }

    [Serializable]
    internal sealed class ProGuardProfile
    {
        public int Version = 1;
        public List<ProGuardSelection> Selections = new();
    }
}
