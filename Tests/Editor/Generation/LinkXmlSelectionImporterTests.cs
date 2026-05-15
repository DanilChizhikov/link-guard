using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DTech.LinkGuard.Editor.Tests
{
    [TestFixture]
    public sealed class LinkXmlSelectionImporterTests
    {
        [SetUp]
        public void SetUp()
        {
            LinkXmlPreservation.Clear(null);
        }

        [Test]
        public void Apply_InvalidXml_ReturnsFalse_LeavesEntriesUnchanged()
        {
            List<AssemblyEntry> entries = new List<AssemblyEntry>
            {
                MakeEntry("Game.Core", "Foo")
            };
            entries[0].Types.First().IsSelected = true;

            bool result = LinkXmlSelectionImporter.Apply("<not valid", entries);

            Assert.That(result, Is.False);
            Assert.That(entries[0].Types.First().IsSelected, Is.True);
        }

        [Test]
        public void Apply_RootNotLinker_ReturnsFalse()
        {
            List<AssemblyEntry> entries = new List<AssemblyEntry> { MakeEntry("Game.Core", "Foo") };

            bool result = LinkXmlSelectionImporter.Apply("<root/>", entries);

            Assert.That(result, Is.False);
        }

        [Test]
        public void Apply_AssemblyPreserveAll_SetsIsAssemblySelected()
        {
            List<AssemblyEntry> entries = new List<AssemblyEntry> { MakeEntry("Game.Core", "Foo") };

            bool result = LinkXmlSelectionImporter.Apply(
                "<linker><assembly fullname=\"Game.Core\" preserve=\"all\"/></linker>",
                entries);

            Assert.That(result, Is.True);
            Assert.That(entries[0].IsAssemblySelected, Is.True);
        }

        [Test]
        public void Apply_TypePreserveAll_SetsTypeIsSelected()
        {
            List<AssemblyEntry> entries = new List<AssemblyEntry> { MakeEntry("Game.Core", "Foo") };

            LinkXmlSelectionImporter.Apply(
                "<linker><assembly fullname=\"Game.Core\"><type fullname=\"Game.Core.Foo\" preserve=\"all\"/></assembly></linker>",
                entries);

            Assert.That(entries[0].IsAssemblySelected, Is.False);
            Assert.That(entries[0].Types.First(t => t.LinkerFullname == "Game.Core.Foo").IsSelected, Is.True);
        }

        [Test]
        public void Apply_LegacyMethodChildren_PromotesTypeToPreserveAll_AndWarns()
        {
            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(".*Type 'Game\\.Core\\.Foo'.*promoted to preserve=\"all\".*"));

            List<AssemblyEntry> entries = new List<AssemblyEntry> { MakeEntry("Game.Core", "Foo") };

            LinkXmlSelectionImporter.Apply(
                "<linker><assembly fullname=\"Game.Core\"><type fullname=\"Game.Core.Foo\"><method signature=\"void Run()\"/></type></assembly></linker>",
                entries);

            Assert.That(entries[0].Types.First(t => t.LinkerFullname == "Game.Core.Foo").IsSelected, Is.True);
        }

        [Test]
        public void Apply_UnknownAssembly_CreatesSyntheticAssembly_WithSourceLinkXml()
        {
            List<AssemblyEntry> entries = new List<AssemblyEntry>();

            LinkXmlSelectionImporter.Apply(
                "<linker><assembly fullname=\"NewAsm\" preserve=\"all\"/></linker>",
                entries);

            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].Name, Is.EqualTo("NewAsm"));
            Assert.That(entries[0].Source, Is.EqualTo(AssemblySource.LinkXml));
            Assert.That(entries[0].IsAssemblySelected, Is.True);
        }

        [Test]
        public void Apply_UnknownType_CreatesSyntheticTypeEntry_AndSplitsNamespace()
        {
            List<AssemblyEntry> entries = new List<AssemblyEntry> { MakeEntry("Game.Core") };

            LinkXmlSelectionImporter.Apply(
                "<linker><assembly fullname=\"Game.Core\"><type fullname=\"My.New.Type\" preserve=\"all\"/></assembly></linker>",
                entries);

            TypeEntry created = entries[0].Types.First(t => t.LinkerFullname == "My.New.Type");
            Assert.That(created.IsSynthetic, Is.True);
            Assert.That(created.Namespace, Is.EqualTo("My.New"));
            Assert.That(created.DisplayName, Is.EqualTo("Type"));
            Assert.That(created.IsSelected, Is.True);
        }

        [Test]
        public void Apply_NestedTypeName_KeepsForwardSlashSegment_InDisplayName()
        {
            List<AssemblyEntry> entries = new List<AssemblyEntry> { MakeEntry("Game.Core") };

            LinkXmlSelectionImporter.Apply(
                "<linker><assembly fullname=\"Game.Core\"><type fullname=\"My.Ns.Outer/Nested\" preserve=\"all\"/></assembly></linker>",
                entries);

            TypeEntry created = entries[0].Types.First(t => t.LinkerFullname == "My.Ns.Outer/Nested");
            Assert.That(created.Namespace, Is.EqualTo("My.Ns"));
            Assert.That(created.DisplayName, Is.EqualTo("Outer/Nested"));
        }

        [Test]
        public void Apply_TypeWithoutNamespace_LeavesNamespaceEmpty()
        {
            List<AssemblyEntry> entries = new List<AssemblyEntry> { MakeEntry("Game.Core") };

            LinkXmlSelectionImporter.Apply(
                "<linker><assembly fullname=\"Game.Core\"><type fullname=\"Standalone\" preserve=\"all\"/></assembly></linker>",
                entries);

            TypeEntry created = entries[0].Types.First(t => t.LinkerFullname == "Standalone");
            Assert.That(created.Namespace, Is.EqualTo(string.Empty));
            Assert.That(created.DisplayName, Is.EqualTo("Standalone"));
        }

        [Test]
        public void Apply_RemovesPreviousLinkXmlSourceEntries_BeforeReimport()
        {
            List<AssemblyEntry> entries = new List<AssemblyEntry>
            {
                new AssemblyEntry("OldSynthetic", AssemblySource.LinkXml, "Merged link.xml", null)
            };
            entries[0].IsAssemblySelected = true;

            LinkXmlSelectionImporter.Apply(
                "<linker><assembly fullname=\"NewSynthetic\" preserve=\"all\"/></linker>",
                entries);

            Assert.That(entries.Any(e => e.Name == "OldSynthetic"), Is.False);
            Assert.That(entries.Any(e => e.Name == "NewSynthetic" && e.Source == AssemblySource.LinkXml), Is.True);
        }

        [TestCase("1")]
        [TestCase("true")]
        [TestCase("True")]
        [TestCase("YES")]
        public void Apply_RecognizesTruthyValuesForIgnoreIfMissing(string truthyValue)
        {
            List<AssemblyEntry> entries = new List<AssemblyEntry> { MakeEntry("Game.Core") };

            LinkXmlSelectionImporter.Apply(
                $"<linker><assembly fullname=\"Game.Core\" ignoreIfMissing=\"{truthyValue}\" preserve=\"all\"/></linker>",
                entries);

            Assert.That(entries[0].IgnoreIfMissing, Is.True);
        }

        [Test]
        public void Apply_ClearsPriorSelectionsOnTrackedEntries()
        {
            List<AssemblyEntry> entries = new List<AssemblyEntry> { MakeEntry("Game.Core", "Foo", "Bar") };
            entries[0].Types.First(t => t.LinkerFullname == "Game.Core.Foo").IsSelected = true;
            entries[0].IgnoreIfMissing = true;

            LinkXmlSelectionImporter.Apply(
                "<linker><assembly fullname=\"Game.Core\"><type fullname=\"Game.Core.Bar\" preserve=\"all\"/></assembly></linker>",
                entries);

            Assert.That(entries[0].Types.First(t => t.LinkerFullname == "Game.Core.Foo").IsSelected, Is.False);
            Assert.That(entries[0].Types.First(t => t.LinkerFullname == "Game.Core.Bar").IsSelected, Is.True);
            Assert.That(entries[0].IgnoreIfMissing, Is.False);
        }

        private static AssemblyEntry MakeEntry(string assemblyName, params string[] typeShortNames)
        {
            List<TypeEntry> types = new List<TypeEntry>();
            foreach (string shortName in typeShortNames)
            {
                string fullname = assemblyName + "." + shortName;
                types.Add(new TypeEntry(assemblyName, fullname, fullname, shortName));
            }
            return new AssemblyEntry(assemblyName, AssemblySource.Project, "/path/" + assemblyName, types);
        }
    }
}
