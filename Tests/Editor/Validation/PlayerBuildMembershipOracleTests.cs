using NUnit.Framework;

namespace DTech.LinkGuard.Editor.Tests
{
    [TestFixture]
    internal sealed class PlayerBuildMembershipOracleTests
    {
        private const string EditorAssembly = "com.dtech.linkguard.editor";
        private const string RuntimeAssembly = "com.dtech.linkguard";

        private PlayerBuildMembershipOracle _oracle;

        [SetUp]
        public void SetUp()
        {
            _oracle = new PlayerBuildMembershipOracle();
        }

        [Test]
        public void ResolveAssembly_Mscorlib_Present()
        {
            Assert.That(_oracle.ResolveAssembly("mscorlib"), Is.EqualTo(BuildPresence.Present));
        }

        [Test]
        public void ResolveAssembly_SystemAndNetstandard_Present()
        {
            Assert.That(_oracle.ResolveAssembly("System.Xml"), Is.EqualTo(BuildPresence.Present));
            Assert.That(_oracle.ResolveAssembly("netstandard"), Is.EqualTo(BuildPresence.Present));
        }

        [Test]
        public void ResolveAssembly_UnityEditor_Missing()
        {
            Assert.That(_oracle.ResolveAssembly("UnityEditor"), Is.EqualTo(BuildPresence.Missing));
            Assert.That(_oracle.ResolveAssembly("UnityEditor.CoreModule"), Is.EqualTo(BuildPresence.Missing));
        }

        [Test]
        public void ResolveAssembly_EditorOnlyAsmdef_Missing()
        {
            Assert.That(_oracle.ResolveAssembly(EditorAssembly), Is.EqualTo(BuildPresence.Missing));
        }

        [Test]
        public void ResolveAssembly_RuntimeAsmdef_Present()
        {
            Assert.That(_oracle.ResolveAssembly(RuntimeAssembly), Is.EqualTo(BuildPresence.Present));
        }

        [Test]
        public void ResolveAssembly_Nonexistent_Missing()
        {
            Assert.That(_oracle.ResolveAssembly("Totally.Fake.Assembly.XYZ"), Is.EqualTo(BuildPresence.Missing));
        }

        [Test]
        public void ResolveType_RealType_Present()
        {
            BuildPresence presence = _oracle.ResolveType(EditorAssembly, "DTech.LinkGuard.Editor.LinkXmlValidator");

            Assert.That(presence, Is.EqualTo(BuildPresence.Present));
        }

        [Test]
        public void ResolveType_NestedType_SlashConverted_Present()
        {
            BuildPresence presence = _oracle.ResolveType(
                EditorAssembly, "DTech.LinkGuard.Editor.LinkXmlPatcher/ProviderRun");

            Assert.That(presence, Is.EqualTo(BuildPresence.Present));
        }

        [Test]
        public void ResolveType_BacktickGenericDefinition_Present()
        {
            BuildPresence presence = _oracle.ResolveType("mscorlib", "System.Collections.Generic.List`1");

            Assert.That(presence, Is.EqualTo(BuildPresence.Present));
        }

        [Test]
        public void ResolveType_NonexistentType_Missing()
        {
            BuildPresence presence = _oracle.ResolveType(EditorAssembly, "DTech.LinkGuard.Editor.NoSuchType123");

            Assert.That(presence, Is.EqualTo(BuildPresence.Missing));
        }

        [Test]
        public void ResolveType_ConstructedGeneric_Unknown()
        {
            BuildPresence presence = _oracle.ResolveType(RuntimeAssembly, "X.Foo<System.Int32>");

            Assert.That(presence, Is.EqualTo(BuildPresence.Unknown));
        }
    }
}
