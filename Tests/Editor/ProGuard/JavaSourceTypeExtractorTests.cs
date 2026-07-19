using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DTech.LinkGuard.Editor.ProGuard.Tests
{
    [TestFixture]
    internal sealed class JavaSourceTypeExtractorTests
    {
        [Test]
        public void Extract_TopLevelClass_ReturnsNameAndPackage()
        {
            const string source = "package com.example;\n\npublic class Foo {\n}\n";

            IReadOnlyList<JavaSourceType> types = JavaSourceTypeExtractor.Extract(source, out string package);

            Assert.That(package, Is.EqualTo("com.example"));
            Assert.That(types.Select(t => t.SimpleName), Is.EqualTo(new[] { "Foo" }));
            Assert.That(types[0].HasInnerClasses, Is.False);
        }

        [Test]
        public void Extract_NestedClass_NotEmitted_MarksOuterHasInnerClasses()
        {
            const string source =
                "package com.example;\n" +
                "public class Outer {\n" +
                "    public static class Inner {\n" +
                "    }\n" +
                "}\n";

            IReadOnlyList<JavaSourceType> types = JavaSourceTypeExtractor.Extract(source, out _);

            Assert.That(types.Select(t => t.SimpleName), Is.EqualTo(new[] { "Outer" }));
            Assert.That(types[0].HasInnerClasses, Is.True);
        }

        [Test]
        public void Extract_CommentedOutDeclaration_Ignored()
        {
            const string source =
                "package com.example;\n" +
                "// class Ghost {\n" +
                "/* class Phantom { } */\n" +
                "public class Real {\n}\n";

            IReadOnlyList<JavaSourceType> types = JavaSourceTypeExtractor.Extract(source, out _);

            Assert.That(types.Select(t => t.SimpleName), Is.EqualTo(new[] { "Real" }));
        }

        [Test]
        public void Extract_DeclarationInsideStringLiteral_Ignored()
        {
            const string source =
                "package com.example;\n" +
                "public class Real {\n" +
                "    String s = \"class Fake {\";\n" +
                "}\n";

            IReadOnlyList<JavaSourceType> types = JavaSourceTypeExtractor.Extract(source, out _);

            Assert.That(types.Select(t => t.SimpleName), Is.EqualTo(new[] { "Real" }));
        }

        [Test]
        public void Extract_KotlinObjectAndDataClass_Detected()
        {
            const string source =
                "package com.example\n" +
                "object Singleton {\n}\n" +
                "data class Payload(val x: Int)\n";

            IReadOnlyList<JavaSourceType> types = JavaSourceTypeExtractor.Extract(source, out string package);

            Assert.That(package, Is.EqualTo("com.example"));
            Assert.That(types.Select(t => t.SimpleName), Is.EqualTo(new[] { "Singleton", "Payload" }));
        }

        [Test]
        public void Extract_KotlinRawString_WithBraces_Ignored()
        {
            const string source =
                "package com.example\n" +
                "class Real {\n" +
                "    val json = \"\"\"{ \"class Fake {\" }\"\"\"\n" +
                "}\n";

            IReadOnlyList<JavaSourceType> types = JavaSourceTypeExtractor.Extract(source, out _);

            Assert.That(types.Select(t => t.SimpleName), Is.EqualTo(new[] { "Real" }));
            Assert.That(types[0].HasInnerClasses, Is.False);
        }

        [Test]
        public void Extract_TwoTopLevelTypes_NestedAttributedToPrecedingType()
        {
            const string source =
                "package com.example;\n" +
                "class First {\n" +
                "    class InnerOfFirst { }\n" +
                "}\n" +
                "class Second {\n" +
                "}\n";

            IReadOnlyList<JavaSourceType> types = JavaSourceTypeExtractor.Extract(source, out _);

            Assert.That(types.Select(t => t.SimpleName), Is.EqualTo(new[] { "First", "Second" }));
            Assert.That(types.First(t => t.SimpleName == "First").HasInnerClasses, Is.True);
            Assert.That(types.First(t => t.SimpleName == "Second").HasInnerClasses, Is.False);
        }

        [Test]
        public void Extract_KotlinEnumClass_ReturnsEnumName()
        {
            const string source =
                "package com.example\n" +
                "enum class Color {\n" +
                "    RED, GREEN\n" +
                "}\n";

            IReadOnlyList<JavaSourceType> types = JavaSourceTypeExtractor.Extract(source, out string package);

            Assert.That(package, Is.EqualTo("com.example"));
            Assert.That(types.Select(t => t.SimpleName), Is.EqualTo(new[] { "Color" }));
        }

        [Test]
        public void Extract_JavaEnum_ReturnsEnumName()
        {
            const string source =
                "package com.example;\n" +
                "public enum Foo {\n" +
                "    A, B\n" +
                "}\n";

            IReadOnlyList<JavaSourceType> types = JavaSourceTypeExtractor.Extract(source, out _);

            Assert.That(types.Select(t => t.SimpleName), Is.EqualTo(new[] { "Foo" }));
        }

        [Test]
        public void Extract_NoPackage_ReturnsEmptyPackage()
        {
            const string source = "class Standalone {\n}\n";

            IReadOnlyList<JavaSourceType> types = JavaSourceTypeExtractor.Extract(source, out string package);

            Assert.That(package, Is.EqualTo(string.Empty));
            Assert.That(types.Select(t => t.SimpleName), Is.EqualTo(new[] { "Standalone" }));
        }
    }
}
