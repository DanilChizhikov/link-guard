using NUnit.Framework;

namespace DTech.LinkGuard.Editor.ProGuard.Tests
{
    [TestFixture]
    internal sealed class JavaClassNameResolverTests
    {
        [Test]
        public void TryResolveClassEntry_TopLevelClass_ResolvesFullnameAndPackage()
        {
            bool ok = JavaClassNameResolver.TryResolveClassEntry("com/foo/Bar.class", out ResolvedJavaClass result);

            Assert.That(ok, Is.True);
            Assert.That(result.Fullname, Is.EqualTo("com.foo.Bar"));
            Assert.That(result.Package, Is.EqualTo("com.foo"));
            Assert.That(result.SimpleName, Is.EqualTo("Bar"));
            Assert.That(result.IsInner, Is.False);
        }

        [Test]
        public void TryResolveClassEntry_InnerClass_FoldsToOuterAndFlagsInner()
        {
            bool ok = JavaClassNameResolver.TryResolveClassEntry("com/foo/Bar$Baz.class", out ResolvedJavaClass result);

            Assert.That(ok, Is.True);
            Assert.That(result.Fullname, Is.EqualTo("com.foo.Bar"));
            Assert.That(result.SimpleName, Is.EqualTo("Bar"));
            Assert.That(result.IsInner, Is.True);
        }

        [Test]
        public void TryResolveClassEntry_AnonymousInnerClass_FoldsToOuter()
        {
            bool ok = JavaClassNameResolver.TryResolveClassEntry("com/foo/Bar$1.class", out ResolvedJavaClass result);

            Assert.That(ok, Is.True);
            Assert.That(result.Fullname, Is.EqualTo("com.foo.Bar"));
            Assert.That(result.IsInner, Is.True);
        }

        [Test]
        public void TryResolveClassEntry_DefaultPackage_HasEmptyPackage()
        {
            bool ok = JavaClassNameResolver.TryResolveClassEntry("Bar.class", out ResolvedJavaClass result);

            Assert.That(ok, Is.True);
            Assert.That(result.Fullname, Is.EqualTo("Bar"));
            Assert.That(result.Package, Is.EqualTo(string.Empty));
            Assert.That(result.SimpleName, Is.EqualTo("Bar"));
        }

        [Test]
        public void TryResolveClassEntry_NormalizesBackslashes()
        {
            bool ok = JavaClassNameResolver.TryResolveClassEntry("com\\foo\\Bar.class", out ResolvedJavaClass result);

            Assert.That(ok, Is.True);
            Assert.That(result.Fullname, Is.EqualTo("com.foo.Bar"));
        }

        [TestCase("module-info.class")]
        [TestCase("com/foo/package-info.class")]
        [TestCase("com/foo/Bar.txt")]
        [TestCase("com/foo/")]
        [TestCase("")]
        public void TryResolveClassEntry_NonClassOrMetadata_ReturnsFalse(string entryPath)
        {
            bool ok = JavaClassNameResolver.TryResolveClassEntry(entryPath, out _);

            Assert.That(ok, Is.False);
        }
    }
}
