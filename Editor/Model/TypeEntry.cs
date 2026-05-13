using System.Collections.Generic;
using System.Xml.Linq;

namespace DTech.LinkGuard.Editor
{
    internal sealed class TypeEntry
    {
        public string Namespace { get; }
        public string Fullname { get; }
        public string LinkerFullname { get; }
        public string DisplayName { get; }
        public List<XAttribute> LinkXmlAttributes { get; } = new();
        public List<XElement> LinkXmlChildren { get; } = new();
        public bool IsSynthetic { get; }
        public bool IsSelected { get; set; }
        public bool ProducesEntry => IsSelected || HasLinkXmlContent;
        private bool HasLinkXmlContent => LinkXmlAttributes.Count > 0 || LinkXmlChildren.Count > 0;

        public TypeEntry(string namespaceName,
            string fullname,
            string linkerFullname,
            string displayName,
            bool isSynthetic = false)
        {
            Namespace = namespaceName ?? string.Empty;
            Fullname = fullname;
            LinkerFullname = linkerFullname;
            DisplayName = displayName;
            IsSynthetic = isSynthetic;
            IsSelected = false;
        }

        public void SelectAll(bool value)
        {
            IsSelected = value;
        }
    }
}
