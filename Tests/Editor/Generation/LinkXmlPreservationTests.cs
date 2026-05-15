using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace DTech.LinkGuard.Editor.Tests
{
    [TestFixture]
    public sealed class LinkXmlPreservationTests
    {
        [SetUp]
        public void SetUp()
        {
            LinkXmlPreservation.Clear(null);
            LinkXmlPreservation.CaptureDocument(new XElement("linker"));
        }

        [Test]
        public void Clear_NullEntries_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => LinkXmlPreservation.Clear(null));
        }

        [Test]
        public void Clear_ResetsCarriedDataOnEntriesAndTypes()
        {
            TypeEntry type = new TypeEntry("Ns", "Ns.Foo", "Ns.Foo", "Foo");
            type.LinkXmlAttributes.Add(new XAttribute("a", "1"));
            type.LinkXmlChildren.Add(new XElement("method"));
            AssemblyEntry entry = new AssemblyEntry("A", AssemblySource.Project, "/p", new[] { type });
            entry.LinkXmlAttributes.Add(new XAttribute("a", "1"));
            entry.LinkXmlChildren.Add(new XElement("type"));

            LinkXmlPreservation.Clear(new[] { entry });

            Assert.That(entry.LinkXmlAttributes, Is.Empty);
            Assert.That(entry.LinkXmlChildren, Is.Empty);
            Assert.That(type.LinkXmlAttributes, Is.Empty);
            Assert.That(type.LinkXmlChildren, Is.Empty);
        }

        [Test]
        public void CaptureAssembly_StoresOnlyNonModeledAttributes()
        {
            AssemblyEntry entry = new AssemblyEntry("A", AssemblySource.Project, "/p", null);
            XElement assemblyXml = new XElement("assembly",
                new XAttribute("fullname", "A"),
                new XAttribute("preserve", "all"),
                new XAttribute("ignoreIfMissing", "1"),
                new XAttribute("customA", "value"),
                new XAttribute("customB", "value2"));

            LinkXmlPreservation.CaptureAssembly(entry, assemblyXml);

            List<string> captured = entry.LinkXmlAttributes.Select(a => a.Name.LocalName).ToList();
            Assert.That(captured, Is.EquivalentTo(new[] { "customA", "customB" }));
        }

        [Test]
        public void CaptureAssembly_PreservesNonAllPreserveAttribute()
        {
            AssemblyEntry entry = new AssemblyEntry("A", AssemblySource.Project, "/p", null);
            XElement assemblyXml = new XElement("assembly",
                new XAttribute("fullname", "A"),
                new XAttribute("preserve", "fields"));

            LinkXmlPreservation.CaptureAssembly(entry, assemblyXml);

            Assert.That(entry.LinkXmlAttributes.Select(a => a.Name.LocalName), Contains.Item("preserve"));
        }

        [Test]
        public void CaptureAssembly_StoresNonTypeChildren()
        {
            AssemblyEntry entry = new AssemblyEntry("A", AssemblySource.Project, "/p", null);
            XElement assemblyXml = new XElement("assembly",
                new XAttribute("fullname", "A"),
                new XElement("type", new XAttribute("fullname", "A.Foo")),
                new XElement("custom-element"));

            LinkXmlPreservation.CaptureAssembly(entry, assemblyXml);

            Assert.That(entry.LinkXmlChildren, Has.Count.EqualTo(1));
            Assert.That(entry.LinkXmlChildren[0].Name.LocalName, Is.EqualTo("custom-element"));
        }

        [Test]
        public void CaptureType_StoresNonMethodChildren_AndNonModeledAttributes()
        {
            TypeEntry type = new TypeEntry("Ns", "Ns.Foo", "Ns.Foo", "Foo");
            XElement typeXml = new XElement("type",
                new XAttribute("fullname", "Ns.Foo"),
                new XAttribute("preserve", "all"),
                new XAttribute("customX", "value"),
                new XElement("method", new XAttribute("signature", "void Run()")),
                new XElement("custom"));

            LinkXmlPreservation.CaptureType(type, typeXml);

            Assert.That(type.LinkXmlAttributes.Select(a => a.Name.LocalName), Is.EqualTo(new[] { "customX" }));
            Assert.That(type.LinkXmlChildren, Has.Count.EqualTo(1));
            Assert.That(type.LinkXmlChildren[0].Name.LocalName, Is.EqualTo("custom"));
        }

        [Test]
        public void ApplyToAssembly_AddsMissingAttributes_DoesNotOverrideExisting()
        {
            AssemblyEntry entry = new AssemblyEntry("A", AssemblySource.Project, "/p", null);
            entry.LinkXmlAttributes.Add(new XAttribute("custom", "carried"));
            entry.LinkXmlAttributes.Add(new XAttribute("already-set", "carried"));
            XElement target = new XElement("assembly",
                new XAttribute("fullname", "A"),
                new XAttribute("already-set", "existing"));

            LinkXmlPreservation.ApplyToAssembly(target, entry);

            Assert.That(target.Attribute("custom")!.Value, Is.EqualTo("carried"));
            Assert.That(target.Attribute("already-set")!.Value, Is.EqualTo("existing"));
        }

        [Test]
        public void ApplyToType_ClonesCarriedChildren()
        {
            TypeEntry type = new TypeEntry("Ns", "Ns.Foo", "Ns.Foo", "Foo");
            XElement carried = new XElement("method", new XAttribute("signature", "void Run()"));
            type.LinkXmlChildren.Add(carried);
            XElement target = new XElement("type", new XAttribute("fullname", "Ns.Foo"));

            LinkXmlPreservation.ApplyToType(target, type);

            XElement applied = target.Element("method");
            Assert.That(applied, Is.Not.Null);
            Assert.That(applied!.Attribute("signature")!.Value, Is.EqualTo("void Run()"));
            Assert.That(applied, Is.Not.SameAs(carried), "Children should be cloned, not shared.");
        }

        [Test]
        public void ApplyToRoot_AppliesPreviouslyCapturedRootData()
        {
            XElement captured = new XElement("linker",
                new XAttribute("customRoot", "value"),
                new XElement("custom-doc-child"));
            LinkXmlPreservation.CaptureDocument(captured);

            XElement target = new XElement("linker");
            LinkXmlPreservation.ApplyToRoot(target);

            Assert.That(target.Attribute("customRoot")!.Value, Is.EqualTo("value"));
            Assert.That(target.Element("custom-doc-child"), Is.Not.Null);
        }

        [Test]
        public void CaptureDocument_IgnoresAssemblyChildren()
        {
            XElement captured = new XElement("linker",
                new XElement("assembly", new XAttribute("fullname", "A")),
                new XElement("custom-doc-child"));
            LinkXmlPreservation.CaptureDocument(captured);

            XElement target = new XElement("linker");
            LinkXmlPreservation.ApplyToRoot(target);

            Assert.That(target.Element("assembly"), Is.Null);
            Assert.That(target.Element("custom-doc-child"), Is.Not.Null);
        }
    }
}
