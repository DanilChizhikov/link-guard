using System.Collections.Generic;
using NUnit.Framework;

namespace DTech.LinkGuard.Editor.Tests
{
    [TestFixture]
    internal sealed class NamespaceEntryTests
    {
        [Test]
        public void Ctor_NullFullname_NormalizesToEmptyString()
        {
            NamespaceEntry ns = new NamespaceEntry(null, new[] { MakeType("Foo") });

            Assert.That(ns.Fullname, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Ctor_NullTypes_ProducesEmptyList()
        {
            NamespaceEntry ns = new NamespaceEntry("Ns", null);

            Assert.That(ns.Types, Is.Empty);
        }

        [Test]
        public void Ctor_SortsTypesByLinkerFullname()
        {
            NamespaceEntry ns = new NamespaceEntry(
                "Ns",
                new[] { MakeType("Ns.Charlie"), MakeType("Ns.Alpha"), MakeType("Ns.Bravo") });

            List<string> names = ns.Types.ConvertAll(t => t.LinkerFullname);
            Assert.That(names, Is.EqualTo(new[] { "Ns.Alpha", "Ns.Bravo", "Ns.Charlie" }));
        }

        [Test]
        public void IsSelected_True_OnlyWhenAllTypesSelected()
        {
            NamespaceEntry ns = new NamespaceEntry(
                "Ns",
                new[] { MakeType("Ns.A", selected: true), MakeType("Ns.B", selected: true) });

            Assert.That(ns.IsSelected, Is.True);
        }

        [Test]
        public void IsSelected_False_WhenAnyTypeUnselected()
        {
            NamespaceEntry ns = new NamespaceEntry(
                "Ns",
                new[] { MakeType("Ns.A", selected: true), MakeType("Ns.B", selected: false) });

            Assert.That(ns.IsSelected, Is.False);
        }

        [Test]
        public void IsSelected_EmptyTypes_ReturnsFalse()
        {
            NamespaceEntry ns = new NamespaceEntry("Ns", null);

            Assert.That(ns.IsSelected, Is.False);
        }

        [Test]
        public void IsSelected_Setter_PropagatesToAllTypes()
        {
            TypeEntry a = MakeType("Ns.A");
            TypeEntry b = MakeType("Ns.B");
            NamespaceEntry ns = new NamespaceEntry("Ns", new[] { a, b });

            ns.IsSelected = true;
            Assert.That(a.IsSelected, Is.True);
            Assert.That(b.IsSelected, Is.True);

            ns.IsSelected = false;
            Assert.That(a.IsSelected, Is.False);
            Assert.That(b.IsSelected, Is.False);
        }

        [Test]
        public void ProducesEntry_TrueWhenAnyTypeProducesEntry()
        {
            NamespaceEntry ns = new NamespaceEntry(
                "Ns",
                new[] { MakeType("Ns.A"), MakeType("Ns.B", selected: true) });

            Assert.That(ns.ProducesEntry, Is.True);
        }

        [Test]
        public void ProducesEntry_FalseWhenNoTypeProducesEntry()
        {
            NamespaceEntry ns = new NamespaceEntry(
                "Ns",
                new[] { MakeType("Ns.A"), MakeType("Ns.B") });

            Assert.That(ns.ProducesEntry, Is.False);
        }

        [Test]
        public void SelectedTypeCount_CountsOnlySelected()
        {
            NamespaceEntry ns = new NamespaceEntry(
                "Ns",
                new[]
                {
                    MakeType("Ns.A", selected: true),
                    MakeType("Ns.B"),
                    MakeType("Ns.C", selected: true),
                });

            Assert.That(ns.SelectedTypeCount, Is.EqualTo(2));
        }
        
        private static TypeEntry MakeType(string linkerFullname, bool selected = false)
        {
            TypeEntry type = new TypeEntry("Ns", linkerFullname, linkerFullname, linkerFullname)
            {
                IsSelected = selected
            };
            return type;
        }
    }
}
