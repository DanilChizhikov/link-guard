using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace DTech.LinkGuard.Editor.Tests
{
    [TestFixture]
    public sealed class LinkXmlBuilderTests
    {
        [SetUp]
        public void SetUp()
        {
            LinkXmlPreservation.Clear(null);
        }

        [Test]
        public void Build_NoEntriesProducingOutput_ReturnsLinkerWithNoAssemblies()
        {
            AssemblyEntry inert = MakeEntry("Game.Core", AssemblySource.Project, MakeType("Ns", "Foo"));

            string xml = LinkXmlBuilder.Build(new[] { inert });
            XElement linker = XDocument.Parse(xml).Root;

            Assert.That(linker, Is.Not.Null);
            Assert.That(linker.Name.LocalName, Is.EqualTo("linker"));
            Assert.That(linker.Elements("assembly"), Is.Empty);
            Assert.That(linker.Nodes().OfType<XComment>(), Is.Empty);
        }

        [Test]
        public void Build_AssemblySelected_AddsPreserveAllAndOmitsTypeChildren()
        {
            AssemblyEntry entry = MakeEntry("Game.Core", AssemblySource.Project, MakeType("Ns", "Foo"));
            entry.IsAssemblySelected = true;

            XElement assembly = ParseFirstAssembly(LinkXmlBuilder.Build(new[] { entry }));

            Assert.That(assembly.Attribute("fullname")?.Value, Is.EqualTo("Game.Core"));
            Assert.That(assembly.Attribute("preserve")?.Value, Is.EqualTo("all"));
            Assert.That(assembly.Elements("type"), Is.Empty);
        }

        [Test]
        public void Build_TypeSelectedNotAssembly_AddsPerTypePreserveAll()
        {
            TypeEntry type = MakeType("Ns", "Foo");
            type.IsSelected = true;
            AssemblyEntry entry = MakeEntry("Game.Core", AssemblySource.Project, type);

            XElement assembly = ParseFirstAssembly(LinkXmlBuilder.Build(new[] { entry }));

            Assert.That(assembly.Attribute("preserve"), Is.Null);
            XElement typeElement = assembly.Element("type");
            Assert.That(typeElement, Is.Not.Null);
            Assert.That(typeElement.Attribute("fullname")?.Value, Is.EqualTo("Ns.Foo"));
            Assert.That(typeElement.Attribute("preserve")?.Value, Is.EqualTo("all"));
        }

        [Test]
        public void Build_IgnoreIfMissing_AddsAttribute_WhenTrue()
        {
            AssemblyEntry entry = MakeEntry("Game.Core", AssemblySource.Project, MakeType("Ns", "Foo"));
            entry.IsAssemblySelected = true;
            entry.IgnoreIfMissing = true;

            XElement assembly = ParseFirstAssembly(LinkXmlBuilder.Build(new[] { entry }));

            Assert.That(assembly.Attribute("ignoreIfMissing")?.Value, Is.EqualTo("1"));
        }

        [Test]
        public void Build_IgnoreIfMissing_OmitsAttribute_WhenFalse()
        {
            AssemblyEntry entry = MakeEntry("Game.Core", AssemblySource.Project, MakeType("Ns", "Foo"));
            entry.IsAssemblySelected = true;

            XElement assembly = ParseFirstAssembly(LinkXmlBuilder.Build(new[] { entry }));

            Assert.That(assembly.Attribute("ignoreIfMissing"), Is.Null);
        }

        [Test]
        public void Build_GroupsEntriesByAssemblySource_InEnumOrder()
        {
            AssemblyEntry plugin = MakeEntry("Plug", AssemblySource.Plugin, MakeType("Ns", "P"));
            plugin.IsAssemblySelected = true;
            AssemblyEntry project = MakeEntry("Proj", AssemblySource.Project, MakeType("Ns", "Q"));
            project.IsAssemblySelected = true;
            AssemblyEntry unity = MakeEntry("UMod", AssemblySource.Unity, MakeType("Ns", "U"));
            unity.IsAssemblySelected = true;

            string xml = LinkXmlBuilder.Build(new[] { plugin, unity, project });
            List<string> assemblyOrder = XDocument.Parse(xml)
                .Root!
                .Elements("assembly")
                .Select(a => a.Attribute("fullname")!.Value)
                .ToList();

            Assert.That(assemblyOrder, Is.EqualTo(new[] { "Proj", "Plug", "UMod" }));
        }

        [Test]
        public void Build_AddsHumanReadableGroupComments_ForEachSource()
        {
            AssemblyEntry project = MakeEntry("Proj", AssemblySource.Project, MakeType("Ns", "A"));
            project.IsAssemblySelected = true;
            AssemblyEntry plugin = MakeEntry("Plug", AssemblySource.Plugin, MakeType("Ns", "B"));
            plugin.IsAssemblySelected = true;
            AssemblyEntry upm = MakeEntry("Upm", AssemblySource.UpmPackage, MakeType("Ns", "C"));
            upm.IsAssemblySelected = true;
            AssemblyEntry sdk = MakeEntry("Sdk", AssemblySource.Sdk, MakeType("Ns", "D"));
            sdk.IsAssemblySelected = true;
            AssemblyEntry unity = MakeEntry("Uni", AssemblySource.Unity, MakeType("Ns", "E"));
            unity.IsAssemblySelected = true;
            AssemblyEntry linkXml = MakeEntry("LXml", AssemblySource.LinkXml, MakeType("Ns", "F"));
            linkXml.IsAssemblySelected = true;

            string xml = LinkXmlBuilder.Build(new[] { project, plugin, upm, sdk, unity, linkXml });
            List<string> comments = XDocument.Parse(xml)
                .Root!
                .Nodes()
                .OfType<XComment>()
                .Select(c => c.Value.Trim())
                .ToList();

            Assert.That(comments, Is.EqualTo(new[]
            {
                "Project assemblies",
                "Plugins folder",
                "UPM packages",
                "SDKs",
                "Unity built-in modules",
                "Merged link.xml entries"
            }));
        }

        [Test]
        public void Build_OrdersAssembliesAlphabeticallyWithinGroup()
        {
            AssemblyEntry b = MakeEntry("Bravo", AssemblySource.Project, MakeType("Ns", "B"));
            b.IsAssemblySelected = true;
            AssemblyEntry a = MakeEntry("Alpha", AssemblySource.Project, MakeType("Ns", "A"));
            a.IsAssemblySelected = true;

            List<string> order = XDocument.Parse(LinkXmlBuilder.Build(new[] { b, a }))
                .Root!
                .Elements("assembly")
                .Select(asm => asm.Attribute("fullname")!.Value)
                .ToList();

            Assert.That(order, Is.EqualTo(new[] { "Alpha", "Bravo" }));
        }

        [Test]
        public void Build_OrdersTypesByLinkerFullname()
        {
            TypeEntry t1 = MakeType("Ns", "Charlie");
            t1.IsSelected = true;
            TypeEntry t2 = MakeType("Ns", "Alpha");
            t2.IsSelected = true;
            TypeEntry t3 = MakeType("Ns", "Bravo");
            t3.IsSelected = true;
            AssemblyEntry entry = MakeEntry("Game.Core", AssemblySource.Project, t1, t2, t3);

            XElement assembly = ParseFirstAssembly(LinkXmlBuilder.Build(new[] { entry }));
            List<string> typeOrder = assembly.Elements("type")
                .Select(t => t.Attribute("fullname")!.Value)
                .ToList();

            Assert.That(typeOrder, Is.EqualTo(new[] { "Ns.Alpha", "Ns.Bravo", "Ns.Charlie" }));
        }

        [Test]
        public void Build_PreservesCarriedLinkXmlAttributes_OnAssembly()
        {
            AssemblyEntry entry = MakeEntry("Game.Core", AssemblySource.Project, MakeType("Ns", "Foo"));
            entry.IsAssemblySelected = true;
            entry.LinkXmlAttributes.Add(new XAttribute("customA", "value"));

            XElement assembly = ParseFirstAssembly(LinkXmlBuilder.Build(new[] { entry }));

            Assert.That(assembly.Attribute("customA")?.Value, Is.EqualTo("value"));
        }

        [Test]
        public void Build_PreservesCarriedLinkXmlChildren_OnType()
        {
            TypeEntry type = MakeType("Ns", "Foo");
            type.IsSelected = true;
            type.LinkXmlChildren.Add(new XElement("method", new XAttribute("signature", "void Run()")));
            AssemblyEntry entry = MakeEntry("Game.Core", AssemblySource.Project, type);

            XElement typeElement = ParseFirstAssembly(LinkXmlBuilder.Build(new[] { entry })).Element("type");
            XElement method = typeElement?.Element("method");

            Assert.That(method, Is.Not.Null);
            Assert.That(method!.Attribute("signature")?.Value, Is.EqualTo("void Run()"));
        }

        [Test]
        public void Build_InsertsBlankLineBeforeComments_BetweenGroups()
        {
            AssemblyEntry project = MakeEntry("Proj", AssemblySource.Project, MakeType("Ns", "A"));
            project.IsAssemblySelected = true;
            AssemblyEntry plugin = MakeEntry("Plug", AssemblySource.Plugin, MakeType("Ns", "B"));
            plugin.IsAssemblySelected = true;

            string xml = LinkXmlBuilder.Build(new[] { project, plugin });

            Assert.That(xml, Does.Contain("\n\n"), "Expected a blank line to be inserted between groups.");
            Assert.That(xml, Does.Match(@"(?:/>|</assembly>)\n\n\s*<!--\s*Plugins folder"));
        }

        [Test]
        public void Build_OmitsXmlDeclaration_UsesFourSpaceIndent()
        {
            AssemblyEntry entry = MakeEntry("Game.Core", AssemblySource.Project, MakeType("Ns", "Foo"));
            entry.IsAssemblySelected = true;

            string xml = LinkXmlBuilder.Build(new[] { entry });

            Assert.That(xml, Does.Not.Contain("<?xml"));
            Assert.That(xml, Does.Contain("    <"), "Expected 4-space indentation.");
            Assert.That(xml, Does.Not.Contain("\r\n"), "Expected unix line endings.");
        }

        [Test]
        public void Serialize_RoundTrips_Through_XDocument()
        {
            XDocument source = new XDocument(new XElement("linker", new XElement("assembly", new XAttribute("fullname", "X"))));

            string xml = LinkXmlBuilder.Serialize(source);
            XDocument parsed = XDocument.Parse(xml);

            Assert.That(parsed.Root!.Name.LocalName, Is.EqualTo("linker"));
            Assert.That(parsed.Root.Element("assembly")!.Attribute("fullname")!.Value, Is.EqualTo("X"));
        }

        private static TypeEntry MakeType(string ns, string name)
        {
            string fullname = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
            return new TypeEntry(ns, fullname, fullname, name);
        }

        private static AssemblyEntry MakeEntry(string name, AssemblySource source, params TypeEntry[] types)
        {
            return new AssemblyEntry(name, source, "/path/" + name, types);
        }

        private static XElement ParseFirstAssembly(string xml)
        {
            XElement assembly = XDocument.Parse(xml).Root!.Elements("assembly").FirstOrDefault();
            if (assembly == null)
            {
                throw new InvalidOperationException("Expected at least one <assembly> element in the output.");
            }
            return assembly;
        }
    }
}
