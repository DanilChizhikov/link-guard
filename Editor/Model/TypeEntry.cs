using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace DTech.LinkGuard.Editor
{
    internal sealed class TypeEntry
    {
        public string Namespace { get; }
        public string Fullname { get; }
        public string LinkerFullname { get; }
        public string DisplayName { get; }
        public List<MethodEntry> Methods { get; }
        public List<XAttribute> LinkXmlAttributes { get; } = new();
        public List<XElement> LinkXmlChildren { get; } = new();
        public bool IsSynthetic { get; }
        public bool IsSelected { get; set; }
        public bool HasMethods => Methods.Count > 0;
        public bool ProducesEntry => IsSelected || HasLinkXmlContent || Methods.Any(m => m.IsSelected);
        public int SelectedMethodCount => Methods.Count(m => m.IsSelected);
        private bool HasLinkXmlContent => LinkXmlAttributes.Count > 0 || LinkXmlChildren.Count > 0;

        public TypeEntry(string namespaceName,
            string fullname,
            string linkerFullname,
            string displayName,
            IEnumerable<MethodEntry> methods,
            bool isSynthetic = false)
        {
            Namespace = namespaceName ?? string.Empty;
            Fullname = fullname;
            LinkerFullname = linkerFullname;
            DisplayName = displayName;
            IsSynthetic = isSynthetic;
            Methods = methods == null
                ? new List<MethodEntry>()
                : methods
                    .Where(m => m != null)
                    .OrderBy(m => m.IsConstructor ? 0 : 1)
                    .ThenBy(m => m.Signature)
                    .ToList();
            IsSelected = false;
        }

        public void SelectAll(bool value)
        {
            IsSelected = value;

            foreach (MethodEntry method in Methods)
            {
                method.IsSelected = false;
            }
        }
    }
}
