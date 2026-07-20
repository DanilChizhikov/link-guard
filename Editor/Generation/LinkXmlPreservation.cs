using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace DTech.LinkGuard.Editor
{
    internal static class LinkXmlPreservation
    {
        private const string AssemblyElement = "assembly";
        private const string TypeElement = "type";
        private const string NamespaceElement = "namespace";
        private const string MethodElement = "method";
        private const string FullnameAttribute = "fullname";
        private const string PreserveAttribute = "preserve";
        private const string IgnoreIfMissingAttribute = "ignoreIfMissing";

        private static readonly List<XAttribute> _rootAttributes = new();
        private static readonly List<XElement> _rootChildren = new();

        public static void Clear(IEnumerable<AssemblyEntry> entries)
        {
            _rootAttributes.Clear();
            _rootChildren.Clear();

            if (entries == null)
            {
                return;
            }

            foreach (AssemblyEntry entry in entries)
            {
                entry.LinkXmlAttributes.Clear();
                entry.LinkXmlChildren.Clear();

                foreach (TypeEntry type in entry.Types)
                {
                    type.LinkXmlAttributes.Clear();
                    type.LinkXmlChildren.Clear();
                }
            }
        }

        public static void CaptureDocument(XElement linker)
        {
            _rootAttributes.Clear();
            _rootAttributes.AddRange(linker.Attributes().Select(a => new XAttribute(a)));

            _rootChildren.Clear();
            _rootChildren.AddRange(linker.Elements()
                .Where(e => !IsElement(e, AssemblyElement))
                .Select(CloneElement));
        }

        public static void ClearAssembly(AssemblyEntry entry)
        {
            entry.LinkXmlAttributes.Clear();
            entry.LinkXmlChildren.Clear();

            foreach (TypeEntry type in entry.Types)
            {
                ClearType(type);
            }
        }

        public static void ClearType(TypeEntry type)
        {
            type.LinkXmlAttributes.Clear();
            type.LinkXmlChildren.Clear();
        }

        public static void CaptureAssembly(AssemblyEntry entry, XElement assemblyElement)
        {
            CaptureAttributes(entry.LinkXmlAttributes, assemblyElement, IsModeledAssemblyAttribute);
            entry.LinkXmlChildren.Clear();
            entry.LinkXmlChildren.AddRange(assemblyElement.Elements()
                .Where(e => !IsRegeneratedAssemblyChild(e))
                .Select(CloneElement));
        }

        private static bool IsRegeneratedAssemblyChild(XElement element)
        {
            if (IsElement(element, TypeElement))
            {
                return true;
            }

            return IsElement(element, NamespaceElement) && IsAllPreserveElement(element);
        }

        private static bool IsAllPreserveElement(XElement element)
        {
            return string.Equals(
                element.Attribute(PreserveAttribute)?.Value,
                "all",
                StringComparison.OrdinalIgnoreCase);
        }

        public static void CaptureType(TypeEntry type, XElement typeElement)
        {
            CaptureAttributes(type.LinkXmlAttributes, typeElement, IsModeledTypeAttribute);
            CaptureChildren(type.LinkXmlChildren, typeElement, MethodElement);
        }

        public static void ApplyToRoot(XElement linker)
        {
            AddMissingAttributes(linker, _rootAttributes);
            AddClonedChildren(linker, _rootChildren);
        }

        public static void ApplyToAssembly(XElement assembly, AssemblyEntry entry)
        {
            AddMissingAttributes(assembly, entry.LinkXmlAttributes);
            AddClonedChildren(assembly, entry.LinkXmlChildren);
        }

        public static void ApplyToType(XElement typeElement, TypeEntry type)
        {
            AddMissingAttributes(typeElement, type.LinkXmlAttributes);
            AddClonedChildren(typeElement, type.LinkXmlChildren);
        }

        private static void CaptureAttributes(
            List<XAttribute> target,
            XElement element,
            Func<XAttribute, bool> isModeledAttribute)
        {
            target.Clear();
            target.AddRange(element.Attributes()
                .Where(a => !isModeledAttribute(a))
                .Select(a => new XAttribute(a)));
        }

        private static void CaptureChildren(List<XElement> target, XElement element, string modeledChildName)
        {
            target.Clear();
            target.AddRange(element.Elements()
                .Where(e => string.IsNullOrEmpty(modeledChildName) || !IsElement(e, modeledChildName))
                .Select(CloneElement));
        }

        private static bool IsModeledAssemblyAttribute(XAttribute attribute)
        {
            return IsAttribute(attribute, FullnameAttribute)
                || IsAttribute(attribute, IgnoreIfMissingAttribute)
                || IsAllPreserveAttribute(attribute);
        }

        private static bool IsModeledTypeAttribute(XAttribute attribute)
        {
            return IsAttribute(attribute, FullnameAttribute)
                || IsAllPreserveAttribute(attribute);
        }

        private static bool IsAllPreserveAttribute(XAttribute attribute)
        {
            return IsAttribute(attribute, PreserveAttribute)
                && string.Equals(attribute.Value, "all", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddMissingAttributes(XElement element, IEnumerable<XAttribute> attributes)
        {
            foreach (XAttribute attribute in attributes)
            {
                if (element.Attribute(attribute.Name) == null)
                {
                    element.Add(new XAttribute(attribute));
                }
            }
        }

        private static void AddClonedChildren(XElement element, IEnumerable<XElement> children)
        {
            foreach (XElement child in children)
            {
                element.Add(CloneElement(child));
            }
        }

        private static XElement CloneElement(XElement element)
        {
            return new XElement(element);
        }

        private static bool IsAttribute(XAttribute attribute, string name)
        {
            return string.Equals(attribute.Name.LocalName, name, StringComparison.Ordinal);
        }

        private static bool IsElement(XElement element, string name)
        {
            return string.Equals(element.Name.LocalName, name, StringComparison.Ordinal);
        }
    }
}
