using NUnit.Framework;

namespace DTech.LinkGuard.Editor.Tests
{
    [TestFixture]
    internal sealed class SystemAssemblyFilterTests
    {
        [TestCase("System")]
        [TestCase("System.Linq")]
        [TestCase("mscorlib")]
        [TestCase("netstandard")]
        [TestCase("Mono.Cecil")]
        [TestCase("Microsoft.Bcl.AsyncInterfaces")]
        [TestCase("nunit.framework")]
        [TestCase("NUnit3.TestRunner")]
        [TestCase("JetBrains.Annotations")]
        [TestCase("ExCSS.Lexer")]
        [TestCase("WindowsBase")]
        [TestCase("PresentationCore")]
        [TestCase("PresentationFramework")]
        public void ShouldExclude_ExcludesPrefixedAssemblies(string name)
        {
            Assert.That(SystemAssemblyFilter.ShouldExclude(name), Is.True);
        }

        [TestCase("Foo.Tests")]
        [TestCase("Foo.Test")]
        [TestCase("Foo.Editor.Tests")]
        [TestCase("Foo.EditorTests")]
        public void ShouldExclude_ExcludesTestSuffixedAssemblies(string name)
        {
            Assert.That(SystemAssemblyFilter.ShouldExclude(name), Is.True);
        }

        [TestCase("Bee.BeeDriver")]
        [TestCase("ExCSS.Unity")]
        [TestCase("PsdPlugin")]
        [TestCase("ReportGeneratorMerged")]
        [TestCase("Unity.SourceGenerators")]
        public void ShouldExclude_ExcludesExactNameMatches(string name)
        {
            Assert.That(SystemAssemblyFilter.ShouldExclude(name), Is.True);
        }

        [TestCase("Assembly-CSharp")]
        [TestCase("UnityEngine")]
        [TestCase("UnityEngine.UI")]
        [TestCase("DTech.LinkGuard.Editor")]
        [TestCase("Firebase.App")]
        [TestCase("MyGame.Core")]
        public void ShouldExclude_AllowsRegularAssemblies(string name)
        {
            Assert.That(SystemAssemblyFilter.ShouldExclude(name), Is.False);
        }

        [TestCase("")]
        [TestCase(null)]
        public void ShouldExclude_NullOrEmpty_ReturnsTrue(string name)
        {
            Assert.That(SystemAssemblyFilter.ShouldExclude(name), Is.True);
        }

        [TestCase("Systematic")]
        [TestCase("Systems")]
        [TestCase("Systems.Core")]
        public void ShouldExclude_UserAssembliesSharingSystemPrefix_AreAllowed(string name)
        {
            Assert.That(SystemAssemblyFilter.ShouldExclude(name), Is.False);
        }
    }
}
