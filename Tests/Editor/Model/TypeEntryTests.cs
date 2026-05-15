using System.Xml.Linq;
using NUnit.Framework;

namespace DTech.LinkGuard.Editor.Tests
{
    [TestFixture]
    public sealed class TypeEntryTests
    {
        [Test]
        public void Ctor_DefaultsToNotSelectedAndNonSynthetic()
        {
            TypeEntry type = new TypeEntry("Ns", "Ns.Foo", "Ns.Foo", "Foo");

            Assert.That(type.IsSelected, Is.False);
            Assert.That(type.IsSynthetic, Is.False);
            Assert.That(type.LinkXmlAttributes, Is.Empty);
            Assert.That(type.LinkXmlChildren, Is.Empty);
            Assert.That(type.ProducesEntry, Is.False);
        }

        [Test]
        public void Ctor_NullNamespace_NormalizesToEmptyString()
        {
            TypeEntry type = new TypeEntry(null, "Foo", "Foo", "Foo");

            Assert.That(type.Namespace, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Ctor_SyntheticFlag_IsHonored()
        {
            TypeEntry type = new TypeEntry("Ns", "Ns.Foo", "Ns.Foo", "Foo", isSynthetic: true);

            Assert.That(type.IsSynthetic, Is.True);
        }

        [Test]
        public void ProducesEntry_TrueWhenSelected()
        {
            TypeEntry type = new TypeEntry("Ns", "Ns.Foo", "Ns.Foo", "Foo");
            type.IsSelected = true;

            Assert.That(type.ProducesEntry, Is.True);
        }

        [Test]
        public void ProducesEntry_TrueWhenCarryingLinkXmlAttribute()
        {
            TypeEntry type = new TypeEntry("Ns", "Ns.Foo", "Ns.Foo", "Foo");
            type.LinkXmlAttributes.Add(new XAttribute("custom", "1"));

            Assert.That(type.ProducesEntry, Is.True);
        }

        [Test]
        public void ProducesEntry_TrueWhenCarryingLinkXmlChild()
        {
            TypeEntry type = new TypeEntry("Ns", "Ns.Foo", "Ns.Foo", "Foo");
            type.LinkXmlChildren.Add(new XElement("method", new XAttribute("signature", "void Run()")));

            Assert.That(type.ProducesEntry, Is.True);
        }

        [Test]
        public void SelectAll_TogglesIsSelected()
        {
            TypeEntry type = new TypeEntry("Ns", "Ns.Foo", "Ns.Foo", "Foo");

            type.SelectAll(true);
            Assert.That(type.IsSelected, Is.True);

            type.SelectAll(false);
            Assert.That(type.IsSelected, Is.False);
        }
    }
}
