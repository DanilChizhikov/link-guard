using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace DTech.LinkGuard.Editor
{
    internal static class LinkXmlSelectionImporter
    {
        private const string SyntheticOrigin = "Merged link.xml";
        private const string LinkerElement = "linker";
        private const string AssemblyElement = "assembly";
        private const string TypeElement = "type";
        private const string MethodElement = "method";
        private const string FullnameAttribute = "fullname";
        private const string PreserveAttribute = "preserve";
        private const string SignatureAttribute = "signature";
        private const string IgnoreIfMissingAttribute = "ignoreIfMissing";

        public static bool Apply(string xml, List<AssemblyEntry> entries)
        {
            if (string.IsNullOrEmpty(xml) || entries == null)
            {
                return false;
            }

            XDocument document;

            try
            {
                document = XDocument.Parse(xml);
            }
            catch
            {
                return false;
            }

            if (document.Root == null || document.Root.Name.LocalName != LinkerElement)
            {
                return false;
            }

            ResetEntries(entries);
            LinkXmlPreservation.CaptureDocument(document.Root);

            foreach (XElement assemblyElement in document.Root.Elements()
                .Where(e => e.Name.LocalName == AssemblyElement))
            {
                ApplyAssembly(assemblyElement, entries);
            }

            return true;
        }

        private static void ResetEntries(List<AssemblyEntry> entries)
        {
            LinkXmlPreservation.Clear(entries);
            entries.RemoveAll(e => e.Source == AssemblySource.LinkXml);

            foreach (AssemblyEntry entry in entries)
            {
                RemoveSyntheticChildren(entry);
                entry.SelectAll(false);
                entry.IgnoreIfMissing = false;
            }
        }

        private static void ApplyAssembly(XElement assemblyElement, List<AssemblyEntry> entries)
        {
            string assemblyName = GetAttributeValue(assemblyElement, FullnameAttribute);

            if (string.IsNullOrEmpty(assemblyName))
            {
                return;
            }

            AssemblyEntry entry = FindAssembly(entries, assemblyName);
            if (entry == null)
            {
                entry = new AssemblyEntry(
                    assemblyName,
                    AssemblySource.LinkXml,
                    SyntheticOrigin,
                    Enumerable.Empty<TypeEntry>());
                entries.Add(entry);
            }

            entry.IgnoreIfMissing = IsTruthy(GetAttributeValue(assemblyElement, IgnoreIfMissingAttribute));
            LinkXmlPreservation.CaptureAssembly(entry, assemblyElement);

            if (PreservesAll(assemblyElement))
            {
                entry.IsAssemblySelected = true;
                return;
            }

            foreach (XElement typeElement in assemblyElement.Elements()
                .Where(e => e.Name.LocalName == TypeElement))
            {
                ApplyType(typeElement, entry);
            }
        }

        private static void ApplyType(XElement typeElement, AssemblyEntry entry)
        {
            string typeFullname = GetAttributeValue(typeElement, FullnameAttribute);

            if (string.IsNullOrEmpty(typeFullname))
            {
                return;
            }

            TypeEntry type = FindType(entry, typeFullname);
            if (type == null)
            {
                type = CreateSyntheticType(typeFullname);
                AddType(entry, type);
            }

            LinkXmlPreservation.CaptureType(type, typeElement);

            if (PreservesAll(typeElement))
            {
                type.SelectAll(true);
                return;
            }

            foreach (XElement methodElement in typeElement.Elements()
                .Where(e => e.Name.LocalName == MethodElement))
            {
                ApplyMethod(methodElement, type);
            }
        }

        private static void ApplyMethod(XElement methodElement, TypeEntry type)
        {
            string signature = GetAttributeValue(methodElement, SignatureAttribute);

            if (string.IsNullOrEmpty(signature) || type.IsSelected)
            {
                return;
            }

            MethodEntry method = type.Methods.FirstOrDefault(m =>
                string.Equals(m.Signature, signature, StringComparison.Ordinal));

            if (method == null)
            {
                method = new MethodEntry(signature, signature, false, true);
                type.Methods.Add(method);
                SortMethods(type.Methods);
            }

            LinkXmlPreservation.CaptureMethod(method, methodElement);
            method.IsSelected = true;
        }

        private static AssemblyEntry FindAssembly(List<AssemblyEntry> entries, string assemblyName)
        {
            return entries.FirstOrDefault(e => string.Equals(e.Name, assemblyName, StringComparison.Ordinal));
        }

        private static TypeEntry FindType(AssemblyEntry entry, string typeFullname)
        {
            return entry.Types.FirstOrDefault(t =>
                string.Equals(t.LinkerFullname, typeFullname, StringComparison.Ordinal)
                || string.Equals(t.Fullname, typeFullname, StringComparison.Ordinal));
        }

        private static TypeEntry CreateSyntheticType(string typeFullname)
        {
            SplitTypeName(typeFullname, out string namespaceName, out string displayName);

            return new TypeEntry(
                namespaceName,
                typeFullname,
                typeFullname,
                displayName,
                Enumerable.Empty<MethodEntry>(),
                true);
        }

        private static void RemoveSyntheticChildren(AssemblyEntry entry)
        {
            foreach (NamespaceEntry ns in entry.Namespaces)
            {
                foreach (TypeEntry type in ns.Types)
                {
                    type.Methods.RemoveAll(m => m.IsSynthetic);
                }

                ns.Types.RemoveAll(t => t.IsSynthetic);
            }

            entry.Namespaces.RemoveAll(ns => ns.Types.Count == 0);
        }

        private static void AddType(AssemblyEntry entry, TypeEntry type)
        {
            NamespaceEntry ns = entry.Namespaces.FirstOrDefault(n =>
                string.Equals(n.Fullname, type.Namespace, StringComparison.Ordinal));

            if (ns == null)
            {
                ns = new NamespaceEntry(type.Namespace, Enumerable.Empty<TypeEntry>());
                entry.Namespaces.Add(ns);
            }

            ns.Types.Add(type);
            SortTypes(ns.Types);
            SortNamespaces(entry.Namespaces);
        }

        private static void SplitTypeName(string typeFullname, out string namespaceName, out string displayName)
        {
            int slashIndex = typeFullname.IndexOf('/', StringComparison.Ordinal);
            string rootTypeName = slashIndex < 0 ? typeFullname : typeFullname.Substring(0, slashIndex);
            int splitIndex = rootTypeName.LastIndexOf('.');

            if (splitIndex < 0)
            {
                namespaceName = string.Empty;
                displayName = typeFullname;
                return;
            }

            namespaceName = typeFullname.Substring(0, splitIndex);
            displayName = typeFullname.Substring(splitIndex + 1);
        }

        private static string GetAttributeValue(XElement element, string attributeName)
        {
            return element.Attribute(attributeName)?.Value ?? string.Empty;
        }

        private static bool PreservesAll(XElement element)
        {
            return string.Equals(
                GetAttributeValue(element, PreserveAttribute),
                "all",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTruthy(string value)
        {
            return string.Equals(value, "1", StringComparison.Ordinal)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }

        private static void SortNamespaces(List<NamespaceEntry> namespaces)
        {
            namespaces.Sort((a, b) => string.Compare(a.Fullname, b.Fullname, StringComparison.Ordinal));
        }

        private static void SortTypes(List<TypeEntry> types)
        {
            types.Sort((a, b) => string.Compare(a.LinkerFullname, b.LinkerFullname, StringComparison.Ordinal));
        }

        private static void SortMethods(List<MethodEntry> methods)
        {
            methods.Sort((a, b) => string.Compare(a.Signature, b.Signature, StringComparison.Ordinal));
        }
    }
}
