using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace DTech.LinkGuard.Editor.Tests
{
    [TestFixture]
    internal sealed class DisabledAssemblyScannerTests
    {
        [Test]
        public void IsVersionExpressionSatisfied_EmptyExpression_RequiresInstalledPackage()
        {
            Assert.That(DisabledAssemblyScanner.IsVersionExpressionSatisfied("1.2.3", string.Empty), Is.True);
        }

        [Test]
        public void IsVersionExpressionSatisfied_MinimumVersion_RequiresInstalledVersionAtLeastMinimum()
        {
            Assert.That(DisabledAssemblyScanner.IsVersionExpressionSatisfied("2.0.0", "1.5.0"), Is.True);
            Assert.That(DisabledAssemblyScanner.IsVersionExpressionSatisfied("1.0.0", "1.5.0"), Is.False);
        }

        [Test]
        public void IsVersionExpressionSatisfied_Range_RespectsInclusiveAndExclusiveBounds()
        {
            Assert.That(DisabledAssemblyScanner.IsVersionExpressionSatisfied("1.5.0", "[1.0.0,2.0.0)"), Is.True);
            Assert.That(DisabledAssemblyScanner.IsVersionExpressionSatisfied("2.0.0", "[1.0.0,2.0.0)"), Is.False);
            Assert.That(DisabledAssemblyScanner.IsVersionExpressionSatisfied("2.0.0", "(1.0.0,2.0.0]"), Is.True);
            Assert.That(DisabledAssemblyScanner.IsVersionExpressionSatisfied("1.0.0", "(1.0.0,2.0.0]"), Is.False);
        }

        [Test]
        public void BuildDefinesForAssembly_MergesBaseDefinesAndSatisfiedVersionDefines()
        {
            AssemblyDefinitionInfo info = new AssemblyDefinitionInfo
            {
                name = "TestAssembly",
                versionDefines = new[]
                {
                    new AssemblyVersionDefine
                    {
                        name = "com.test.package",
                        expression = "1.0.0",
                        define = "HAS_TEST_PACKAGE"
                    }
                }
            };
            Dictionary<string, string> packages = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "com.test.package", "1.2.0" }
            };

            HashSet<string> result = DisabledAssemblyScanner.BuildDefinesForAssembly(
                Defines("CUSTOM_DEFINE", "UNITY_ANDROID", "ENABLE_IL2CPP"),
                info,
                packages,
                "Assets/Test.asmdef");

            Assert.That(result, Is.EquivalentTo(new[]
            {
                "CUSTOM_DEFINE",
                "UNITY_ANDROID",
                "ENABLE_IL2CPP",
                "HAS_TEST_PACKAGE"
            }));
        }

        private static HashSet<string> Defines(params string[] symbols)
        {
            return new HashSet<string>(symbols, StringComparer.Ordinal);
        }
    }
}
