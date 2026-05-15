using NUnit.Framework;

namespace DTech.LinkGuard.Editor.Tests
{
    [TestFixture]
    public sealed class SmokeTests
    {
        [Test]
        public void TestAssembly_CanReferenceInternalsOfEditorAssembly()
        {
            AssemblyEntry entry = new AssemblyEntry(
                "Assembly-CSharp",
                AssemblySource.Project,
                string.Empty,
                null);

            Assert.That(entry.Name, Is.EqualTo("Assembly-CSharp"));
            Assert.That(entry.Source, Is.EqualTo(AssemblySource.Project));
            Assert.That(entry.HasNamespaces, Is.False);
        }
    }
}
