using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace DTech.LinkGuard.Editor
{
    internal static class LinkXmlBuilder
    {
        public static string Build(IReadOnlyList<AssemblyEntry> entries)
        {
            XDocument document = new XDocument();
            XElement linker = new XElement("linker");
            document.Add(linker);
            LinkXmlPreservation.ApplyToRoot(linker);

            IEnumerable<IGrouping<AssemblySource, AssemblyEntry>> grouped = entries
                .Where(e => e.ProducesEntry)
                .GroupBy(e => e.Source)
                .OrderBy(g => (int)g.Key);

            foreach (IGrouping<AssemblySource, AssemblyEntry> group in grouped)
            {
                linker.Add(new XComment($" {GetGroupComment(group.Key)} "));

                foreach (AssemblyEntry entry in group.OrderBy(e => e.Name))
                {
                    linker.Add(BuildAssemblyElement(entry));
                }
            }

            string xml = SerializeWithDeclaration(document);

            return InsertBlankLineBeforeComments(xml);
        }

        private static XElement BuildAssemblyElement(AssemblyEntry entry)
        {
            XElement assembly = new XElement("assembly", new XAttribute("fullname", entry.Name));

            bool wholeAssembly = entry.IsAssemblySelected
                || (entry.TypeCount > 0
                    && entry.SelectedTypeCount == entry.TypeCount
                    && entry.Types.All(t => t.LinkXmlAttributes.Count == 0 && t.LinkXmlChildren.Count == 0));

            if (wholeAssembly)
            {
                assembly.Add(new XAttribute("preserve", "all"));
            }

            if (entry.IgnoreIfMissing)
            {
                assembly.Add(new XAttribute("ignoreIfMissing", "1"));
            }

            LinkXmlPreservation.ApplyToAssembly(assembly, entry);

            if (!wholeAssembly)
            {
                foreach (NamespaceEntry ns in entry.Namespaces.Where(n => n.ProducesEntry).OrderBy(n => n.Fullname))
                {
                    if (CanCollapse(ns))
                    {
                        assembly.Add(BuildNamespaceWildcardElement(ns));
                        continue;
                    }

                    foreach (TypeEntry type in ns.Types.Where(t => t.ProducesEntry).OrderBy(t => t.LinkerFullname))
                    {
                        assembly.Add(BuildTypeElement(type));
                    }
                }
            }

            return assembly;
        }

        private static bool CanCollapse(NamespaceEntry ns)
        {
            return !string.IsNullOrEmpty(ns.Fullname)
                && ns.Types.Count > 0
                && ns.Types.All(t => t.IsSelected)
                && ns.Types.All(t => t.LinkXmlAttributes.Count == 0 && t.LinkXmlChildren.Count == 0);
        }

        private static XElement BuildNamespaceWildcardElement(NamespaceEntry ns)
        {
            return new XElement(
                "type",
                new XAttribute("fullname", ns.Fullname + ".*"),
                new XAttribute("preserve", "all"));
        }

        private static XElement BuildTypeElement(TypeEntry type)
        {
            XElement typeElement = new XElement("type", new XAttribute("fullname", type.LinkerFullname));

            if (type.IsSelected)
            {
                typeElement.Add(new XAttribute("preserve", "all"));
            }

            LinkXmlPreservation.ApplyToType(typeElement, type);

            return typeElement;
        }

        private static string InsertBlankLineBeforeComments(string xml)
        {
            return Regex.Replace(xml, @"(/>|</assembly>)(\r?\n)(\s*)<!--", "$1$2$2$3<!--", RegexOptions.None);
        }

        private static string GetGroupComment(AssemblySource source)
        {
            return source switch
            {
                AssemblySource.Project => "Project assemblies",
                AssemblySource.Plugin => "Plugins folder",
                AssemblySource.UpmPackage => "UPM packages",
                AssemblySource.Sdk => "SDKs",
                AssemblySource.Unity => "Unity built-in modules",
                AssemblySource.LinkXml => "Merged link.xml entries",
                _ => "Assemblies"
            };
        }

        private static string SerializeWithDeclaration(XDocument document)
        {
            return Serialize(document);
        }

        public static string Serialize(XDocument document)
        {
            StringBuilder builder = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "    ",
                OmitXmlDeclaration = true,
                NewLineChars = "\n",
                Encoding = new UTF8Encoding(false)
            };

            using (StringWriter stringWriter = new StringWriter(builder))
            {
                using XmlWriter writer = XmlWriter.Create(stringWriter, settings);
                document.Save(writer);
            }

            return builder.ToString();
        }
    }
}
