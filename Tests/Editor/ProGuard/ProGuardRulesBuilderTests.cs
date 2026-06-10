using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DTech.LinkGuard.Editor.ProGuard.Tests
{
    [TestFixture]
    public sealed class ProGuardRulesBuilderTests
    {
        [Test]
        public void Build_NothingSelected_ReturnsHeaderOnly()
        {
            AndroidArtifactEntry entry = MakeEntry("lib.jar", AndroidArtifactSource.Jar, MakeClass("com.foo", "Bar"));

            string text = ProGuardRulesBuilder.Build(new[] { entry });

            Assert.That(Lines(text).First(), Is.EqualTo(ProGuardRulesBuilder.Header));
            Assert.That(text, Does.Not.Contain("-keep"));
        }

        [Test]
        public void Build_SelectedClass_EmitsKeepRule()
        {
            JavaClassEntry javaClass = MakeClass("com.foo", "Bar");
            javaClass.IsSelected = true;
            AndroidArtifactEntry entry = MakeEntry("lib.jar", AndroidArtifactSource.Jar, javaClass);

            string text = ProGuardRulesBuilder.Build(new[] { entry });

            Assert.That(Lines(text), Does.Contain("-keep class com.foo.Bar { *; }"));
            Assert.That(Lines(text), Does.Contain("# JAR plugins"));
        }

        [Test]
        public void Build_SelectedClassWithInnerClasses_EmitsNestedKeepRule()
        {
            JavaClassEntry javaClass = MakeClass("com.foo", "Bar", hasInner: true);
            javaClass.IsSelected = true;
            AndroidArtifactEntry entry = MakeEntry("lib.jar", AndroidArtifactSource.Jar, javaClass);

            List<string> lines = Lines(text: ProGuardRulesBuilder.Build(new[] { entry }));

            Assert.That(lines, Does.Contain("-keep class com.foo.Bar { *; }"));
            Assert.That(lines, Does.Contain("-keep class com.foo.Bar$** { *; }"));
        }

        [Test]
        public void Build_SelectedPackage_EmitsPackageWildcardRule()
        {
            AndroidArtifactEntry entry = MakeEntry("lib.aar", AndroidArtifactSource.Aar,
                MakeClass("com.foo", "A"), MakeClass("com.foo", "B"));
            entry.Packages.Single().IsSelected = true;

            List<string> lines = Lines(ProGuardRulesBuilder.Build(new[] { entry }));

            Assert.That(lines, Does.Contain("-keep class com.foo.** { *; }"));
            Assert.That(lines, Has.None.Contains("com.foo.A"));
        }

        [Test]
        public void Build_SelectedArtifact_CollapsesSubPackagesToRoot()
        {
            AndroidArtifactEntry entry = MakeEntry("lib.aar", AndroidArtifactSource.Aar,
                MakeClass("com.foo", "A"), MakeClass("com.foo.bar", "B"));
            entry.IsArtifactSelected = true;

            List<string> rules = Lines(ProGuardRulesBuilder.Build(new[] { entry }))
                .Where(l => l.StartsWith("-keep"))
                .ToList();

            Assert.That(rules, Is.EqualTo(new[] { "-keep class com.foo.** { *; }" }));
        }

        [Test]
        public void Build_ChildDeselectedAfterArtifactSelection_DoesNotEmitArtifactWideRule()
        {
            AndroidArtifactEntry entry = MakeEntry("lib.aar", AndroidArtifactSource.Aar,
                MakeClass("com.foo", "A"), MakeClass("com.foo", "B"));
            entry.SelectAll(true);
            entry.IsArtifactSelected = false;
            entry.Classes.Single(c => c.DisplayName == "A").SelectAll(false);

            List<string> rules = Lines(ProGuardRulesBuilder.Build(new[] { entry }))
                .Where(l => l.StartsWith("-keep"))
                .ToList();

            Assert.That(rules, Does.Not.Contain("-keep class com.foo.** { *; }"));
            Assert.That(rules, Does.Not.Contain("-keep class com.foo.A { *; }"));
            Assert.That(rules, Does.Contain("-keep class com.foo.B { *; }"));
        }

        [Test]
        public void Build_GroupsBySource_InAarThenJarOrder()
        {
            JavaClassEntry aarClass = MakeClass("com.aar", "A");
            aarClass.IsSelected = true;
            JavaClassEntry jarClass = MakeClass("com.jar", "J");
            jarClass.IsSelected = true;

            AndroidArtifactEntry jar = MakeEntry("z.jar", AndroidArtifactSource.Jar, jarClass);
            AndroidArtifactEntry aar = MakeEntry("a.aar", AndroidArtifactSource.Aar, aarClass);

            List<string> comments = Lines(ProGuardRulesBuilder.Build(new[] { jar, aar }))
                .Where(l => l.StartsWith("#") && l != ProGuardRulesBuilder.Header)
                .ToList();

            Assert.That(comments, Is.EqualTo(new[] { "# AAR plugins", "# JAR plugins" }));
        }

        [Test]
        public void Build_DuplicateRulesAcrossArtifacts_AreDeduplicated()
        {
            JavaClassEntry first = MakeClass("com.dup", "Same");
            first.IsSelected = true;
            JavaClassEntry second = MakeClass("com.dup", "Same");
            second.IsSelected = true;

            AndroidArtifactEntry a = MakeEntry("a.jar", AndroidArtifactSource.Jar, first);
            AndroidArtifactEntry b = MakeEntry("b.jar", AndroidArtifactSource.Jar, second);

            int count = Lines(ProGuardRulesBuilder.Build(new[] { a, b }))
                .Count(l => l == "-keep class com.dup.Same { *; }");

            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void CollapseToRoots_RemovesSubPackagesOfPresentAncestor()
        {
            IReadOnlyList<string> roots = ProGuardRulesBuilder.CollapseToRoots(
                new[] { "com.foo", "com.foo.bar", "com.foobar" });

            Assert.That(roots, Is.EquivalentTo(new[] { "com.foo", "com.foobar" }));
        }

        private static List<string> Lines(string text)
        {
            return text.Split('\n').Where(l => l.Length > 0).ToList();
        }

        private static JavaClassEntry MakeClass(string package, string name, bool hasInner = false)
        {
            string fullname = string.IsNullOrEmpty(package) ? name : package + "." + name;
            return new JavaClassEntry(package, fullname, name, hasInner);
        }

        private static AndroidArtifactEntry MakeEntry(string name, AndroidArtifactSource source,
            params JavaClassEntry[] classes)
        {
            return new AndroidArtifactEntry(name, source, "path/" + name, classes);
        }
    }
}
