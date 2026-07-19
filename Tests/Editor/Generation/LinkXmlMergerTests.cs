using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace DTech.LinkGuard.Editor.Tests
{
    [TestFixture]
    internal sealed class LinkXmlMergerTests
    {
        private string _tempDirectory;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "LinkGuardMergerTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_tempDirectory) && Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        [Test]
        public void Merge_EmptyInputList_ReturnsEmptyLinker()
        {
            LinkXmlMergeResult result = LinkXmlMerger.Merge(Array.Empty<string>());

            Assert.That(result.FilesFound, Is.EqualTo(0));
            Assert.That(result.FilesMerged, Is.EqualTo(0));
            Assert.That(result.DuplicatesCollapsed, Is.EqualTo(0));
            Assert.That(result.SkippedFiles, Is.Empty);

            XDocument document = XDocument.Parse(result.Xml);
            Assert.That(document.Root!.Name.LocalName, Is.EqualTo("linker"));
            Assert.That(document.Root.Elements(), Is.Empty);
        }

        [Test]
        public void Merge_MissingFile_RecordsSkippedFile_AndContinues()
        {
            string valid = Write("<linker><assembly fullname=\"X\" preserve=\"all\"/></linker>");
            string missing = Path.Combine(_tempDirectory, "missing.xml");

            LinkXmlMergeResult result = LinkXmlMerger.Merge(new[] { missing, valid });

            Assert.That(result.FilesMerged, Is.EqualTo(1));
            Assert.That(result.SkippedFiles, Has.Count.EqualTo(1));
            Assert.That(result.SkippedFiles[0].Path, Is.EqualTo(missing));
            Assert.That(result.SkippedFiles[0].Reason, Does.Contain("does not exist"));
        }

        [Test]
        public void Merge_InvalidXml_RecordsSkippedFile()
        {
            string invalid = Write("<linker><assembly fullname=\"oops\"</linker>");

            LinkXmlMergeResult result = LinkXmlMerger.Merge(new[] { invalid });

            Assert.That(result.FilesMerged, Is.EqualTo(0));
            Assert.That(result.SkippedFiles, Has.Count.EqualTo(1));
            Assert.That(result.SkippedFiles[0].Path, Is.EqualTo(invalid));
        }

        [Test]
        public void Merge_RootElementNotLinker_RecordsSkippedFile()
        {
            string wrongRoot = Write("<root><assembly fullname=\"X\"/></root>");

            LinkXmlMergeResult result = LinkXmlMerger.Merge(new[] { wrongRoot });

            Assert.That(result.FilesMerged, Is.EqualTo(0));
            Assert.That(result.SkippedFiles, Has.Count.EqualTo(1));
            Assert.That(result.SkippedFiles[0].Reason, Does.Contain("<linker>"));
        }

        [Test]
        public void Merge_TwoFiles_CombinesAssemblies()
        {
            string a = Write("<linker><assembly fullname=\"A\" preserve=\"all\"/></linker>");
            string b = Write("<linker><assembly fullname=\"B\" preserve=\"all\"/></linker>");

            LinkXmlMergeResult result = LinkXmlMerger.Merge(new[] { a, b });
            List<string> names = ParseAssemblies(result.Xml).Select(asm => asm.Attribute("fullname")!.Value).ToList();

            Assert.That(result.FilesMerged, Is.EqualTo(2));
            Assert.That(result.DuplicatesCollapsed, Is.EqualTo(0));
            Assert.That(names, Is.EqualTo(new[] { "A", "B" }));
        }

        [Test]
        public void Merge_DuplicateAssembly_DeduplicatesByFullname_IncrementsCounter()
        {
            string a = Write("<linker><assembly fullname=\"X\"><type fullname=\"X.Foo\" preserve=\"all\"/></assembly></linker>");
            string b = Write("<linker><assembly fullname=\"X\"><type fullname=\"X.Bar\" preserve=\"all\"/></assembly></linker>");

            LinkXmlMergeResult result = LinkXmlMerger.Merge(new[] { a, b });
            List<XElement> assemblies = ParseAssemblies(result.Xml);

            Assert.That(assemblies, Has.Count.EqualTo(1));
            Assert.That(result.DuplicatesCollapsed, Is.EqualTo(1));
            List<string> types = assemblies[0].Elements("type").Select(t => t.Attribute("fullname")!.Value).ToList();
            Assert.That(types, Is.EqualTo(new[] { "X.Bar", "X.Foo" }));
        }

        [Test]
        public void Merge_DuplicateType_DeduplicatesByFullname()
        {
            string a = Write("<linker><assembly fullname=\"X\"><type fullname=\"X.Foo\" preserve=\"fields\"/></assembly></linker>");
            string b = Write("<linker><assembly fullname=\"X\"><type fullname=\"X.Foo\" preserve=\"all\"/></assembly></linker>");

            LinkXmlMergeResult result = LinkXmlMerger.Merge(new[] { a, b });
            XElement assembly = ParseAssemblies(result.Xml).Single();

            Assert.That(assembly.Elements("type").Count(), Is.EqualTo(1));
            Assert.That(result.DuplicatesCollapsed, Is.EqualTo(2));
            Assert.That(assembly.Element("type")!.Attribute("preserve")!.Value, Is.EqualTo("all"));
        }

        [Test]
        public void Merge_DuplicateMethod_DeduplicatesBySignature()
        {
            string a = Write("<linker><assembly fullname=\"X\"><type fullname=\"X.Foo\"><method signature=\"void Run()\"/></type></assembly></linker>");
            string b = Write("<linker><assembly fullname=\"X\"><type fullname=\"X.Foo\"><method signature=\"void Run()\"/></type></assembly></linker>");

            LinkXmlMergeResult result = LinkXmlMerger.Merge(new[] { a, b });
            XElement type = ParseAssemblies(result.Xml).Single().Element("type");

            Assert.That(type!.Elements("method").Count(), Is.EqualTo(1));
            Assert.That(result.DuplicatesCollapsed, Is.EqualTo(3));
        }

        [Test]
        public void Merge_PreserveAttribute_AllBeatsFields()
        {
            string a = Write("<linker><assembly fullname=\"X\"><type fullname=\"X.Foo\" preserve=\"fields\"/></assembly></linker>");
            string b = Write("<linker><assembly fullname=\"X\"><type fullname=\"X.Foo\" preserve=\"all\"/></assembly></linker>");

            string xml = LinkXmlMerger.Merge(new[] { a, b }).Xml;
            string preserve = ParseAssemblies(xml).Single().Element("type")!.Attribute("preserve")!.Value;

            Assert.That(preserve, Is.EqualTo("all"));
        }

        [Test]
        public void Merge_PreserveAttribute_AllStaysWhenIncomingIsWeaker()
        {
            string a = Write("<linker><assembly fullname=\"X\"><type fullname=\"X.Foo\" preserve=\"all\"/></assembly></linker>");
            string b = Write("<linker><assembly fullname=\"X\"><type fullname=\"X.Foo\" preserve=\"fields\"/></assembly></linker>");

            string xml = LinkXmlMerger.Merge(new[] { a, b }).Xml;
            string preserve = ParseAssemblies(xml).Single().Element("type")!.Attribute("preserve")!.Value;

            Assert.That(preserve, Is.EqualTo("all"));
        }

        [Test]
        public void Merge_PreserveAllOnAssembly_DropsAllNestedTypes()
        {
            string a = Write("<linker><assembly fullname=\"X\"><type fullname=\"X.Foo\" preserve=\"all\"/></assembly></linker>");
            string b = Write("<linker><assembly fullname=\"X\" preserve=\"all\"/></linker>");

            string xml = LinkXmlMerger.Merge(new[] { a, b }).Xml;
            XElement assembly = ParseAssemblies(xml).Single();

            Assert.That(assembly.Attribute("preserve")!.Value, Is.EqualTo("all"));
            Assert.That(assembly.Elements("type"), Is.Empty);
        }

        [Test]
        public void Merge_PreserveAllOnType_DropsAllNestedMethods()
        {
            string a = Write("<linker><assembly fullname=\"X\"><type fullname=\"X.Foo\"><method signature=\"void Run()\"/></type></assembly></linker>");
            string b = Write("<linker><assembly fullname=\"X\"><type fullname=\"X.Foo\" preserve=\"all\"/></assembly></linker>");

            string xml = LinkXmlMerger.Merge(new[] { a, b }).Xml;
            XElement type = ParseAssemblies(xml).Single().Element("type");

            Assert.That(type!.Attribute("preserve")!.Value, Is.EqualTo("all"));
            Assert.That(type.Elements("method"), Is.Empty);
        }

        [Test]
        public void Merge_IgnoreIfMissing_TrueWinsOverFalse()
        {
            string a = Write("<linker><assembly fullname=\"X\" ignoreIfMissing=\"0\" preserve=\"all\"/></linker>");
            string b = Write("<linker><assembly fullname=\"X\" ignoreIfMissing=\"1\" preserve=\"all\"/></linker>");

            string xml = LinkXmlMerger.Merge(new[] { a, b }).Xml;
            string ignore = ParseAssemblies(xml).Single().Attribute("ignoreIfMissing")!.Value;

            Assert.That(ignore, Is.EqualTo("1"));
        }

        [Test]
        public void Merge_IgnoreIfMissing_TrueStaysWhenIncomingIsFalse()
        {
            string a = Write("<linker><assembly fullname=\"X\" ignoreIfMissing=\"1\" preserve=\"all\"/></linker>");
            string b = Write("<linker><assembly fullname=\"X\" ignoreIfMissing=\"0\" preserve=\"all\"/></linker>");

            string xml = LinkXmlMerger.Merge(new[] { a, b }).Xml;
            string ignore = ParseAssemblies(xml).Single().Attribute("ignoreIfMissing")!.Value;

            Assert.That(ignore, Is.EqualTo("1"));
        }

        [Test]
        public void Merge_NonModeledAttribute_FirstNonEmptyWins()
        {
            string a = Write("<linker><assembly fullname=\"X\" customA=\"alpha\" preserve=\"all\"/></linker>");
            string b = Write("<linker><assembly fullname=\"X\" customA=\"beta\" preserve=\"all\"/></linker>");

            string xml = LinkXmlMerger.Merge(new[] { a, b }).Xml;
            string customA = ParseAssemblies(xml).Single().Attribute("customA")!.Value;

            Assert.That(customA, Is.EqualTo("alpha"));
        }

        [Test]
        public void Merge_GenericElementKey_DeduplicatesByNameAttribute()
        {
            string a = Write("<linker><pattern name=\"x\" value=\"a\"/></linker>");
            string b = Write("<linker><pattern name=\"x\" value=\"b\"/></linker>");

            LinkXmlMergeResult result = LinkXmlMerger.Merge(new[] { a, b });
            XElement linker = XDocument.Parse(result.Xml).Root!;
            List<XElement> patterns = linker.Elements("pattern").ToList();

            Assert.That(patterns, Has.Count.EqualTo(1));
            Assert.That(result.DuplicatesCollapsed, Is.EqualTo(1));
            Assert.That(patterns[0].Attribute("value")!.Value, Is.EqualTo("a"));
        }

        [Test]
        public void Merge_SortsChildrenWithinAssembly_TypeBeforeMethod()
        {
            string file = Write("<linker><assembly fullname=\"X\"><method signature=\"void Run()\"/><type fullname=\"X.Foo\" preserve=\"fields\"/></assembly></linker>");
            string other = Write("<linker><assembly fullname=\"X\" preserve=\"fields\"/></linker>");

            string xml = LinkXmlMerger.Merge(new[] { file, other }).Xml;
            XElement assembly = ParseAssemblies(xml).Single();
            List<string> childNames = assembly.Elements().Select(e => e.Name.LocalName).ToList();

            Assert.That(childNames, Is.EqualTo(new[] { "type", "method" }));
        }

        [Test]
        public void Merge_ChildlessTypeWithoutPreserve_IsNotDowngradedByPreserveNothing()
        {
            string a = Write("<linker><assembly fullname=\"X\"><type fullname=\"X.Foo\"/></assembly></linker>");
            string b = Write("<linker><assembly fullname=\"X\"><type fullname=\"X.Foo\" preserve=\"nothing\"/></assembly></linker>");

            string xml = LinkXmlMerger.Merge(new[] { a, b }).Xml;
            XElement type = ParseAssemblies(xml).Single().Element("type")!;

            Assert.That(type.Attribute("preserve")!.Value, Is.EqualTo("all"));
        }

        [Test]
        public void Merge_PreserveNothingFirst_ChildlessTypeSecond_UpgradesToAll()
        {
            string a = Write("<linker><assembly fullname=\"X\"><type fullname=\"X.Foo\" preserve=\"nothing\"/></assembly></linker>");
            string b = Write("<linker><assembly fullname=\"X\"><type fullname=\"X.Foo\"/></assembly></linker>");

            string xml = LinkXmlMerger.Merge(new[] { a, b }).Xml;
            XElement type = ParseAssemblies(xml).Single().Element("type")!;

            Assert.That(type.Attribute("preserve")!.Value, Is.EqualTo("all"));
        }

        [Test]
        public void Merge_ChildlessAssemblyWithoutPreserve_UpgradesOverIncomingPreserve()
        {
            string a = Write("<linker><assembly fullname=\"X\"/></linker>");
            string b = Write("<linker><assembly fullname=\"X\" preserve=\"fields\"/></linker>");

            string xml = LinkXmlMerger.Merge(new[] { a, b }).Xml;
            XElement assembly = ParseAssemblies(xml).Single();

            Assert.That(assembly.Attribute("preserve")!.Value, Is.EqualTo("all"));
        }

        [Test]
        public void Merge_TypeWithMemberChildren_DoesNotMaterializePreserve_AndAcceptsIncomingRank()
        {
            string a = Write("<linker><assembly fullname=\"X\"><type fullname=\"X.Foo\"><method signature=\"void Run()\"/></type></assembly></linker>");
            string b = Write("<linker><assembly fullname=\"X\"><type fullname=\"X.Foo\" preserve=\"fields\"/></assembly></linker>");

            string xml = LinkXmlMerger.Merge(new[] { a, b }).Xml;
            XElement type = ParseAssemblies(xml).Single().Element("type")!;

            Assert.That(type.Attribute("preserve")!.Value, Is.EqualTo("fields"));
            Assert.That(type.Elements("method").Count(), Is.EqualTo(1));
        }

        [Test]
        public void Merge_SortsAttributes_FullnamePreserveIgnoreIfMissingThenAlpha()
        {
            string a = Write("<linker><assembly fullname=\"X\" zeta=\"1\"/></linker>");
            string b = Write("<linker><assembly fullname=\"X\" alpha=\"1\" preserve=\"all\" ignoreIfMissing=\"1\"/></linker>");

            string xml = LinkXmlMerger.Merge(new[] { a, b }).Xml;
            XElement assembly = ParseAssemblies(xml).Single();
            List<string> attrOrder = assembly.Attributes().Select(a2 => a2.Name.LocalName).ToList();

            Assert.That(attrOrder, Is.EqualTo(new[] { "fullname", "preserve", "ignoreIfMissing", "alpha", "zeta" }));
        }

        [Test]
        public void MergeContents_EmptyInputList_ReturnsEmptyLinker()
        {
            LinkXmlMergeResult result = LinkXmlMerger.Merge(Array.Empty<LinkXmlMergeInput>());

            Assert.That(result.FilesFound, Is.EqualTo(0));
            Assert.That(result.FilesMerged, Is.EqualTo(0));
            Assert.That(result.SkippedFiles, Is.Empty);

            XDocument document = XDocument.Parse(result.Xml);
            Assert.That(document.Root!.Name.LocalName, Is.EqualTo("linker"));
            Assert.That(document.Root.Elements(), Is.Empty);
        }

        [Test]
        public void MergeContents_InvalidXml_RecordsSkippedWithSourceLabel()
        {
            LinkXmlMergeInput invalid = new LinkXmlMergeInput("broken", "<linker><assembly fullname=\"oops\"</linker>");
            LinkXmlMergeInput valid = new LinkXmlMergeInput("ok", "<linker><assembly fullname=\"X\" preserve=\"all\"/></linker>");

            LinkXmlMergeResult result = LinkXmlMerger.Merge(new[] { invalid, valid });

            Assert.That(result.FilesMerged, Is.EqualTo(1));
            Assert.That(result.SkippedFiles, Has.Count.EqualTo(1));
            Assert.That(result.SkippedFiles[0].Path, Is.EqualTo("broken"));
        }

        [Test]
        public void MergeContents_RootElementNotLinker_RecordsSkipped()
        {
            LinkXmlMergeInput wrongRoot = new LinkXmlMergeInput("wrong", "<root><assembly fullname=\"X\"/></root>");

            LinkXmlMergeResult result = LinkXmlMerger.Merge(new[] { wrongRoot });

            Assert.That(result.FilesMerged, Is.EqualTo(0));
            Assert.That(result.SkippedFiles, Has.Count.EqualTo(1));
            Assert.That(result.SkippedFiles[0].Path, Is.EqualTo("wrong"));
            Assert.That(result.SkippedFiles[0].Reason, Does.Contain("<linker>"));
        }

        [Test]
        public void MergeContents_TwoInputs_CombinesAssemblies()
        {
            LinkXmlMergeInput a = new LinkXmlMergeInput("a", "<linker><assembly fullname=\"A\" preserve=\"all\"/></linker>");
            LinkXmlMergeInput b = new LinkXmlMergeInput("b", "<linker><assembly fullname=\"B\" preserve=\"all\"/></linker>");

            LinkXmlMergeResult result = LinkXmlMerger.Merge(new[] { a, b });
            List<string> names = ParseAssemblies(result.Xml).Select(asm => asm.Attribute("fullname")!.Value).ToList();

            Assert.That(result.FilesMerged, Is.EqualTo(2));
            Assert.That(names, Is.EqualTo(new[] { "A", "B" }));
        }

        [Test]
        public void MergeContents_DuplicateAssembly_Collapses()
        {
            LinkXmlMergeInput a = new LinkXmlMergeInput("a", "<linker><assembly fullname=\"X\" preserve=\"all\"/></linker>");
            LinkXmlMergeInput b = new LinkXmlMergeInput("b", "<linker><assembly fullname=\"X\" preserve=\"all\"/></linker>");

            LinkXmlMergeResult result = LinkXmlMerger.Merge(new[] { a, b });

            Assert.That(ParseAssemblies(result.Xml), Has.Count.EqualTo(1));
            Assert.That(result.DuplicatesCollapsed, Is.EqualTo(1));
        }

        private string Write(string content)
        {
            string path = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(path, content);
            return path;
        }

        private static List<XElement> ParseAssemblies(string xml)
        {
            return XDocument.Parse(xml).Root!.Elements("assembly").ToList();
        }
    }
}
