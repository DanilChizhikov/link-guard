using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

namespace DTech.LinkGuard.Editor
{
    internal static class LinkXmlSelectionImporter
    {
        private const string SyntheticOrigin = "Merged link.xml";
        private const string LinkerElement = "linker";
        private const string AssemblyElement = "assembly";
        private const string TypeElement = "type";
        private const string MethodElement = "method";
        private const string FieldElement = "field";
        private const string PropertyElement = "property";
        private const string EventElement = "event";
        private const string FullnameAttribute = "fullname";
        private const string PreserveAttribute = "preserve";
        private const string IgnoreIfMissingAttribute = "ignoreIfMissing";

        public static bool Apply(string xml, List<AssemblyEntry> entries, IPrecompiledTypeResolver resolver = null)
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

            foreach (XElement assemblyElement in document.Root.Elements().Where(e => e.Name.LocalName == AssemblyElement))
            {
                ApplyAssembly(assemblyElement, entries, resolver);
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

        private static void ApplyAssembly(XElement assemblyElement, List<AssemblyEntry> entries, IPrecompiledTypeResolver resolver)
        {
            string assemblyName = GetAttributeValue(assemblyElement, FullnameAttribute);

            if (string.IsNullOrEmpty(assemblyName))
            {
                return;
            }

            AssemblyEntry entry = FindAssembly(entries, assemblyName);
            if (entry == null)
            {
                bool isPrecompiled = resolver != null && resolver.IsKnownAssembly(assemblyName);

                entry = new AssemblyEntry(
                    assemblyName,
                    isPrecompiled ? AssemblySource.Unity : AssemblySource.LinkXml,
                    isPrecompiled ? string.Empty : SyntheticOrigin,
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
                ApplyType(typeElement, entry, resolver);
            }
        }

        private static void ApplyType(XElement typeElement, AssemblyEntry entry, IPrecompiledTypeResolver resolver)
        {
            string typeFullname = GetAttributeValue(typeElement, FullnameAttribute);

            if (string.IsNullOrEmpty(typeFullname))
            {
                return;
            }

            TypeEntry type = FindType(entry, typeFullname);
            if (type == null)
            {
                type = resolver != null && resolver.TryResolveType(entry.Name, typeFullname, out TypeEntry resolved)
                    ? resolved
                    : CreateSyntheticType(typeFullname);
                AddType(entry, type);
            }

            LinkXmlPreservation.CaptureType(type, typeElement);

            if (PreservesAll(typeElement))
            {
                type.SelectAll(true);
                return;
            }

            bool hasMemberChildren = typeElement.Elements()
                .Any(e => IsMemberElement(e.Name.LocalName));

            if (hasMemberChildren)
            {
                Debug.LogWarning(
                    $"[LinkXmlGenerator] Type '{typeFullname}' in assembly '{entry.Name}' had member-level entries; promoted to preserve=\"all\".");
            }

            type.SelectAll(true);
        }

        private static bool IsMemberElement(string localName)
        {
            return string.Equals(localName, MethodElement, StringComparison.Ordinal)
                || string.Equals(localName, FieldElement, StringComparison.Ordinal)
                || string.Equals(localName, PropertyElement, StringComparison.Ordinal)
                || string.Equals(localName, EventElement, StringComparison.Ordinal);
        }

        private static AssemblyEntry FindAssembly(List<AssemblyEntry> entries, string assemblyName)
        {
            return entries.FirstOrDefault(e => string.Equals(e.Name, assemblyName, StringComparison.Ordinal));
        }

        private static TypeEntry FindType(AssemblyEntry entry, string typeFullname)
        {
            return entry.Types.FirstOrDefault(t => string.Equals(t.LinkerFullname, typeFullname, StringComparison.Ordinal)
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
                true);
        }

        private static void RemoveSyntheticChildren(AssemblyEntry entry)
        {
            foreach (NamespaceEntry ns in entry.Namespaces)
            {
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
            string value = GetAttributeValue(element, PreserveAttribute);
            return string.Equals(value, "all", StringComparison.OrdinalIgnoreCase);
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
    }
}
