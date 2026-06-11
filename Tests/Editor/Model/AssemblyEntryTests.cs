using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace DTech.LinkGuard.Editor.Tests
{
    [TestFixture]
    internal sealed class AssemblyEntryTests
    {
        private static TypeEntry MakeType(string ns, string name, bool selected = false)
        {
            string fullname = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
            TypeEntry type = new TypeEntry(ns, fullname, fullname, name)
            {
                IsSelected = selected
            };
            return type;
        }

        [Test]
        public void Ctor_DeduplicatesTypesByLinkerFullname()
        {
            AssemblyEntry entry = new AssemblyEntry(
                "Game.Core",
                AssemblySource.Project,
                "/path",
                new[]
                {
                    MakeType("Game", "Foo"),
                    MakeType("Game", "Foo"),
                    MakeType("Game", "Bar"),
                });

            Assert.That(entry.TypeCount, Is.EqualTo(2));
        }

        [Test]
        public void Ctor_GroupsTypesByNamespace_AndSortsAlphabetically()
        {
            AssemblyEntry entry = new AssemblyEntry(
                "Game.Core",
                AssemblySource.Project,
                "/path",
                new[]
                {
                    MakeType("Beta", "Y"),
                    MakeType("Alpha", "X"),
                    MakeType("Beta", "Z"),
                });

            List<string> namespaces = entry.Namespaces.ConvertAll(n => n.Fullname);
            Assert.That(namespaces, Is.EqualTo(new[] { "Alpha", "Beta" }));

            NamespaceEntry beta = entry.Namespaces.First(n => n.Fullname == "Beta");
            Assert.That(beta.Types.Select(t => t.LinkerFullname),
                Is.EqualTo(new[] { "Beta.Y", "Beta.Z" }));
        }

        [Test]
        public void Ctor_NullTypes_ProducesEmptyNamespaces()
        {
            AssemblyEntry entry = new AssemblyEntry(
                "Game.Core",
                AssemblySource.Project,
                "/path",
                null);

            Assert.That(entry.Namespaces, Is.Empty);
            Assert.That(entry.HasNamespaces, Is.False);
            Assert.That(entry.TypeCount, Is.EqualTo(0));
        }

        [Test]
        public void Ctor_NullTypeItems_AreIgnored()
        {
            AssemblyEntry entry = new AssemblyEntry(
                "Game.Core",
                AssemblySource.Project,
                "/path",
                new[] { MakeType("Game", "Foo"), null, MakeType("Game", "Bar") });

            Assert.That(entry.TypeCount, Is.EqualTo(2));
        }

        [Test]
        public void TypeCount_SumsAcrossNamespaces()
        {
            AssemblyEntry entry = new AssemblyEntry(
                "Game.Core",
                AssemblySource.Project,
                "/path",
                new[]
                {
                    MakeType("Game", "A"),
                    MakeType("Game", "B"),
                    MakeType("Util", "C"),
                });

            Assert.That(entry.TypeCount, Is.EqualTo(3));
        }

        [Test]
        public void SelectedTypeCount_OnlyCountsSelected()
        {
            AssemblyEntry entry = new AssemblyEntry(
                "Game.Core",
                AssemblySource.Project,
                "/path",
                new[]
                {
                    MakeType("Game", "A", selected: true),
                    MakeType("Game", "B"),
                    MakeType("Util", "C", selected: true),
                });

            Assert.That(entry.SelectedTypeCount, Is.EqualTo(2));
        }

        [Test]
        public void ProducesEntry_IsTrueWhenAssemblySelected()
        {
            AssemblyEntry entry = MakeEmptyEntry();
            entry.IsAssemblySelected = true;

            Assert.That(entry.ProducesEntry, Is.True);
        }

        [Test]
        public void ProducesEntry_IsTrueWhenLinkXmlAttributesPresent()
        {
            AssemblyEntry entry = MakeEmptyEntry();
            entry.LinkXmlAttributes.Add(new XAttribute("custom", "value"));

            Assert.That(entry.ProducesEntry, Is.True);
        }

        [Test]
        public void ProducesEntry_IsTrueWhenLinkXmlChildrenPresent()
        {
            AssemblyEntry entry = MakeEmptyEntry();
            entry.LinkXmlChildren.Add(new XElement("custom"));

            Assert.That(entry.ProducesEntry, Is.True);
        }

        [Test]
        public void ProducesEntry_IsTrueWhenAnyTypeSelected()
        {
            AssemblyEntry entry = new AssemblyEntry(
                "Game.Core",
                AssemblySource.Project,
                "/path",
                new[] { MakeType("Game", "Foo", selected: true) });

            Assert.That(entry.ProducesEntry, Is.True);
        }

        [Test]
        public void ProducesEntry_IsFalseWhenNothingSelectedAndNoCarriedXml()
        {
            AssemblyEntry entry = new AssemblyEntry(
                "Game.Core",
                AssemblySource.Project,
                "/path",
                new[] { MakeType("Game", "Foo") });

            Assert.That(entry.ProducesEntry, Is.False);
        }

        [Test]
        public void SelectAll_True_SetsIsAssemblySelectedAndAllNamespaces()
        {
            AssemblyEntry entry = new AssemblyEntry(
                "Game.Core",
                AssemblySource.Project,
                "/path",
                new[] { MakeType("Game", "A"), MakeType("Util", "B") });

            entry.SelectAll(true);

            Assert.That(entry.IsAssemblySelected, Is.True);
            Assert.That(entry.Types.All(t => t.IsSelected), Is.True);
        }

        [Test]
        public void SelectAll_False_ClearsAllSelectionsRecursively()
        {
            AssemblyEntry entry = new AssemblyEntry(
                "Game.Core",
                AssemblySource.Project,
                "/path",
                new[]
                {
                    MakeType("Game", "A", selected: true),
                    MakeType("Util", "B", selected: true),
                });
            entry.IsAssemblySelected = true;

            entry.SelectAll(false);

            Assert.That(entry.IsAssemblySelected, Is.False);
            Assert.That(entry.Types.Any(t => t.IsSelected), Is.False);
        }

        private static AssemblyEntry MakeEmptyEntry()
        {
            return new AssemblyEntry("Game.Core", AssemblySource.Project, "/path", null);
        }
    }
}
