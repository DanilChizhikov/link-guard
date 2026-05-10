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

            if (entry.IsAssemblySelected)
            {
                assembly.Add(new XAttribute("preserve", "all"));
            }

            if (entry.IgnoreIfMissing)
            {
                assembly.Add(new XAttribute("ignoreIfMissing", "1"));
            }

            if (!entry.IsAssemblySelected)
            {
                foreach (GlobalTypeEntry type in entry.GlobalTypes.Where(t => t.IsSelected).OrderBy(t => t.Fullname))
                {
                    assembly.Add(new XElement("type",
                        new XAttribute("fullname", type.Fullname),
                        new XAttribute("preserve", "all")));
                }

                foreach (NamespaceEntry ns in entry.Namespaces.Where(n => n.IsSelected).OrderBy(n => n.Fullname))
                {
                    assembly.Add(new XElement("namespace",
                        new XAttribute("fullname", ns.Fullname),
                        new XAttribute("preserve", "all")));
                }
            }

            return assembly;
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
                _ => "Assemblies"
            };
        }

        private static string SerializeWithDeclaration(XDocument document)
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
