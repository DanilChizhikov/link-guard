using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DTech.LinkGuard.Editor.Tests
{
    [TestFixture]
    public sealed class SourceTypeExtractorTests
    {
        private static List<string> LinkerNames(string source)
        {
            return SourceTypeExtractor.ExtractFromSource(source)
                .Select(t => t.LinkerFullname)
                .OrderBy(n => n)
                .ToList();
        }

        [Test]
        public void ExtractFromSource_NamespacedClass_UsesFullName()
        {
            const string source = "namespace Foo.Bar { public class Baz { } }";

            Assert.That(LinkerNames(source), Is.EqualTo(new[] { "Foo.Bar.Baz" }));
        }

        [Test]
        public void ExtractFromSource_FileScopedNamespace_IsApplied()
        {
            const string source = "namespace A.B;\npublic sealed class C { }";

            List<TypeEntry> types = SourceTypeExtractor.ExtractFromSource(source);

            Assert.That(types, Has.Count.EqualTo(1));
            Assert.That(types[0].Namespace, Is.EqualTo("A.B"));
            Assert.That(types[0].LinkerFullname, Is.EqualTo("A.B.C"));
        }

        [Test]
        public void ExtractFromSource_GlobalNamespace_HasEmptyNamespace()
        {
            const string source = "public class Root { }";

            List<TypeEntry> types = SourceTypeExtractor.ExtractFromSource(source);

            Assert.That(types, Has.Count.EqualTo(1));
            Assert.That(types[0].Namespace, Is.EqualTo(string.Empty));
            Assert.That(types[0].LinkerFullname, Is.EqualTo("Root"));
        }

        [Test]
        public void ExtractFromSource_AllTypeKinds_AreCaptured()
        {
            const string source =
                "namespace N {" +
                " class C {}" +
                " struct S {}" +
                " interface I {}" +
                " enum E { A, B }" +
                " record R(int X);" +
                " record struct RS(int Y);" +
                "}";

            Assert.That(LinkerNames(source), Is.EqualTo(new[]
            {
                "N.C",
                "N.E",
                "N.I",
                "N.R",
                "N.RS",
                "N.S",
            }));
        }

        [Test]
        public void ExtractFromSource_NestedTypes_AreSkipped()
        {
            const string source = "namespace N { public class Outer { private class Inner { } } }";

            Assert.That(LinkerNames(source), Is.EqualTo(new[] { "N.Outer" }));
        }

        [Test]
        public void ExtractFromSource_NestedNamespaces_AreComposed()
        {
            const string source = "namespace A { namespace B { class C { } } }";

            Assert.That(LinkerNames(source), Is.EqualTo(new[] { "A.B.C" }));
        }

        [Test]
        public void ExtractFromSource_KeywordsInStringsAndComments_AreIgnored()
        {
            const string source =
                "namespace N {\n" +
                "  // class Commented { }\n" +
                "  /* struct Blocked { } */\n" +
                "  public class Real {\n" +
                "    string s = \"class Fake { }\";\n" +
                "  }\n" +
                "}";

            Assert.That(LinkerNames(source), Is.EqualTo(new[] { "N.Real" }));
        }

        [Test]
        public void ExtractFromSource_MultipleTopLevelTypes_AllCaptured()
        {
            const string source = "namespace N { class A { } class B { } }";

            Assert.That(LinkerNames(source), Is.EqualTo(new[] { "N.A", "N.B" }));
        }

        [Test]
        public void ExtractFromSource_NullOrEmpty_ReturnsEmpty()
        {
            Assert.That(SourceTypeExtractor.ExtractFromSource(null), Is.Empty);
            Assert.That(SourceTypeExtractor.ExtractFromSource(string.Empty), Is.Empty);
        }
    }
}
