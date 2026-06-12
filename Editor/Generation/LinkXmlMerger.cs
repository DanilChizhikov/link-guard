using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace DTech.LinkGuard.Editor
{
    internal static class LinkXmlMerger
    {
        private const string LinkerElement = "linker";
        private const string AssemblyElement = "assembly";
        private const string TypeElement = "type";
        private const string MethodElement = "method";
        private const string FullnameAttribute = "fullname";
        private const string SignatureAttribute = "signature";
        private const string PreserveAttribute = "preserve";
        private const string IgnoreIfMissingAttribute = "ignoreIfMissing";

        private static readonly string[] GenericIdentityAttributes =
        {
            FullnameAttribute,
            SignatureAttribute,
            "name",
            "feature",
            "windowsruntime"
        };

        public static LinkXmlMergeResult Merge(IReadOnlyList<string> paths)
        {
            int filesMerged = 0;
            int duplicatesCollapsed = 0;
            List<LinkXmlMergeSkippedFile> skippedFiles = new List<LinkXmlMergeSkippedFile>();
            XElement linker = new XElement(LinkerElement);

            foreach (string path in paths)
            {
                if (!TryLoadLinker(path, skippedFiles, out XElement sourceLinker))
                {
                    continue;
                }

                filesMerged++;

                foreach (XElement child in sourceLinker.Elements())
                {
                    MergeChild(linker, child, ref duplicatesCollapsed);
                }
            }

            XDocument document = new XDocument(linker);
            string xml = LinkXmlBuilder.Serialize(document);

            return new LinkXmlMergeResult(xml, paths.Count, filesMerged, duplicatesCollapsed, skippedFiles);
        }

        public static LinkXmlMergeResult Merge(IReadOnlyList<LinkXmlMergeInput> inputs)
        {
            int sourcesMerged = 0;
            int duplicatesCollapsed = 0;
            List<LinkXmlMergeSkippedFile> skippedFiles = new List<LinkXmlMergeSkippedFile>();
            XElement linker = new XElement(LinkerElement);

            foreach (LinkXmlMergeInput input in inputs)
            {
                if (!TryParseLinker(input.Xml, input.Source, skippedFiles, out XElement sourceLinker))
                {
                    continue;
                }

                sourcesMerged++;

                foreach (XElement child in sourceLinker.Elements())
                {
                    MergeChild(linker, child, ref duplicatesCollapsed);
                }
            }

            XDocument document = new XDocument(linker);
            string xml = LinkXmlBuilder.Serialize(document);

            return new LinkXmlMergeResult(xml, inputs.Count, sourcesMerged, duplicatesCollapsed, skippedFiles);
        }

        private static bool TryLoadLinker(
            string path,
            List<LinkXmlMergeSkippedFile> skippedFiles,
            out XElement linker)
        {
            linker = null;

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                skippedFiles.Add(new LinkXmlMergeSkippedFile(path, "File does not exist."));
                return false;
            }

            try
            {
                return TryParseLinker(File.ReadAllText(path), path, skippedFiles, out linker);
            }
            catch (Exception ex)
            {
                skippedFiles.Add(new LinkXmlMergeSkippedFile(path, ex.Message));
                return false;
            }
        }

        private static bool TryParseLinker(
            string xml,
            string sourceLabel,
            List<LinkXmlMergeSkippedFile> skippedFiles,
            out XElement linker)
        {
            linker = null;

            try
            {
                XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
                if (document.Root == null || document.Root.Name.LocalName != LinkerElement)
                {
                    skippedFiles.Add(new LinkXmlMergeSkippedFile(sourceLabel, "Root element is not <linker>."));
                    return false;
                }

                linker = document.Root;
                return true;
            }
            catch (Exception ex)
            {
                skippedFiles.Add(new LinkXmlMergeSkippedFile(sourceLabel, ex.Message));
                return false;
            }
        }

        private static void MergeChild(XElement parent, XElement incoming, ref int duplicatesCollapsed)
        {
            string key = GetElementKey(incoming);
            XElement existing = parent.Elements()
                .FirstOrDefault(e => string.Equals(GetElementKey(e), key, StringComparison.Ordinal));

            if (existing == null)
            {
                parent.Add(CloneWithoutWhitespace(incoming));
                SortChildren(parent);
                return;
            }

            duplicatesCollapsed++;
            MergeAttributes(existing, incoming);

            if (PreservesAll(existing) && IsContainerElement(existing))
            {
                existing.RemoveNodes();
                SortChildren(parent);
                return;
            }

            foreach (XElement child in incoming.Elements())
            {
                MergeChild(existing, child, ref duplicatesCollapsed);
            }

            SortChildren(existing);
            SortChildren(parent);
        }

        private static XElement CloneWithoutWhitespace(XElement source)
        {
            XElement clone = new XElement(source.Name);

            foreach (XAttribute attribute in source.Attributes().OrderBy(a => a.Name.LocalName, StringComparer.Ordinal))
            {
                clone.Add(new XAttribute(attribute));
            }

            foreach (XElement child in source.Elements())
            {
                clone.Add(CloneWithoutWhitespace(child));
            }

            if (PreservesAll(clone) && IsContainerElement(clone))
            {
                clone.RemoveNodes();
            }

            SortChildren(clone);

            return clone;
        }

        private static void MergeAttributes(XElement target, XElement incoming)
        {
            foreach (XAttribute attribute in incoming.Attributes())
            {
                XAttribute existing = target.Attribute(attribute.Name);

                if (existing == null)
                {
                    target.Add(new XAttribute(attribute));
                    continue;
                }

                existing.Value = MergeAttributeValue(attribute.Name.LocalName, existing.Value, attribute.Value);
            }

            SortAttributes(target);
        }

        private static string MergeAttributeValue(string attributeName, string current, string incoming)
        {
            if (string.Equals(attributeName, PreserveAttribute, StringComparison.Ordinal))
            {
                return GetPreserveRank(incoming) > GetPreserveRank(current) ? incoming : current;
            }

            if (string.Equals(attributeName, IgnoreIfMissingAttribute, StringComparison.Ordinal))
            {
                return IsTruthy(incoming) ? incoming : current;
            }

            return string.IsNullOrEmpty(current) ? incoming : current;
        }

        private static string GetElementKey(XElement element)
        {
            string elementName = element.Name.LocalName;

            if (string.Equals(elementName, AssemblyElement, StringComparison.Ordinal))
            {
                return $"{AssemblyElement}:{GetAttributeValue(element, FullnameAttribute)}";
            }

            if (string.Equals(elementName, TypeElement, StringComparison.Ordinal))
            {
                return $"{TypeElement}:{GetAttributeValue(element, FullnameAttribute)}";
            }

            if (string.Equals(elementName, MethodElement, StringComparison.Ordinal))
            {
                return $"{MethodElement}:{GetAttributeValue(element, SignatureAttribute)}";
            }

            string genericKey = GetGenericElementKey(element);
            if (!string.IsNullOrEmpty(genericKey))
            {
                return $"{elementName}:{genericKey}";
            }

            return $"{elementName}:{GetStableXml(element)}";
        }

        private static string GetGenericElementKey(XElement element)
        {
            foreach (string attributeName in GenericIdentityAttributes)
            {
                string value = GetAttributeValue(element, attributeName);
                if (!string.IsNullOrEmpty(value))
                {
                    return $"{attributeName}={value}";
                }
            }

            return string.Empty;
        }

        private static string GetStableXml(XElement element)
        {
            return CloneWithoutWhitespace(element).ToString(SaveOptions.DisableFormatting);
        }

        private static string GetAttributeValue(XElement element, string attributeName)
        {
            return element.Attribute(attributeName)?.Value ?? string.Empty;
        }

        private static bool IsContainerElement(XElement element)
        {
            string elementName = element.Name.LocalName;

            return string.Equals(elementName, AssemblyElement, StringComparison.Ordinal)
                || string.Equals(elementName, TypeElement, StringComparison.Ordinal);
        }

        private static bool PreservesAll(XElement element)
        {
            string preserve = GetAttributeValue(element, PreserveAttribute);

            return string.Equals(preserve, "all", StringComparison.OrdinalIgnoreCase);
        }

        private static int GetPreserveRank(string preserve)
        {
            if (string.Equals(preserve, "all", StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (string.Equals(preserve, "fields", StringComparison.OrdinalIgnoreCase)
                || string.Equals(preserve, "methods", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            return string.IsNullOrEmpty(preserve) ? 0 : 1;
        }

        private static bool IsTruthy(string value)
        {
            return string.Equals(value, "1", StringComparison.Ordinal)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }

        private static void SortChildren(XElement parent)
        {
            List<XElement> ordered = parent.Elements()
                .OrderBy(e => GetElementSortRank(e), Comparer<int>.Default)
                .ThenBy(GetElementKey, StringComparer.Ordinal)
                .ToList();

            if (ordered.Count <= 1)
            {
                return;
            }

            parent.RemoveNodes();
            parent.Add(ordered);
        }

        private static int GetElementSortRank(XElement element)
        {
            string elementName = element.Name.LocalName;

            if (string.Equals(elementName, AssemblyElement, StringComparison.Ordinal))
            {
                return 0;
            }

            if (string.Equals(elementName, TypeElement, StringComparison.Ordinal))
            {
                return 1;
            }

            if (string.Equals(elementName, MethodElement, StringComparison.Ordinal))
            {
                return 2;
            }

            return 3;
        }

        private static void SortAttributes(XElement element)
        {
            List<XAttribute> ordered = element.Attributes()
                .OrderBy(a => GetAttributeSortRank(a), Comparer<int>.Default)
                .ThenBy(a => a.Name.LocalName, StringComparer.Ordinal)
                .ToList();

            element.ReplaceAttributes(ordered);
        }

        private static int GetAttributeSortRank(XAttribute attribute)
        {
            string attributeName = attribute.Name.LocalName;

            if (string.Equals(attributeName, FullnameAttribute, StringComparison.Ordinal))
            {
                return 0;
            }

            if (string.Equals(attributeName, SignatureAttribute, StringComparison.Ordinal))
            {
                return 1;
            }

            if (string.Equals(attributeName, PreserveAttribute, StringComparison.Ordinal))
            {
                return 2;
            }

            if (string.Equals(attributeName, IgnoreIfMissingAttribute, StringComparison.Ordinal))
            {
                return 3;
            }

            return 4;
        }
    }
}
