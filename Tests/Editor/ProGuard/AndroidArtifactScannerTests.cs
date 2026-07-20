using System.Collections.Generic;
using NUnit.Framework;

namespace DTech.LinkGuard.Editor.ProGuard.Tests
{
    [TestFixture]
    internal sealed class AndroidArtifactScannerTests
    {
        private const string ProjectRoot = "/proj";

        private static readonly List<AndroidArtifactScanner.SearchRoot> _roots = new()
        {
            new AndroidArtifactScanner.SearchRoot("/proj/Assets", "Assets"),
            new AndroidArtifactScanner.SearchRoot("/proj/Packages", "Packages"),
            new AndroidArtifactScanner.SearchRoot(
                "/proj/Library/PackageCache/com.vendor.sdk@1.2.3-abc123",
                "Packages/com.vendor.sdk"),
        };

        [Test]
        public void ResolveStableOrigin_AssetsPath_ReturnsProjectRelative()
        {
            string origin = AndroidArtifactScanner.ResolveStableOrigin(
                "/proj/Assets/Plugins/Android/lib.aar", _roots, ProjectRoot);

            Assert.That(origin, Is.EqualTo("Assets/Plugins/Android/lib.aar"));
        }

        [Test]
        public void ResolveStableOrigin_PackageCachePath_MapsToStablePackageName()
        {
            string origin = AndroidArtifactScanner.ResolveStableOrigin(
                "/proj/Library/PackageCache/com.vendor.sdk@1.2.3-abc123/Plugins/Android/vendor.aar",
                _roots,
                ProjectRoot);

            Assert.That(origin, Is.EqualTo("Packages/com.vendor.sdk/Plugins/Android/vendor.aar"));
        }

        [Test]
        public void ResolveStableOrigin_BackslashInput_Normalized()
        {
            string origin = AndroidArtifactScanner.ResolveStableOrigin(
                "/proj/Assets\\Plugins\\lib.jar", _roots, ProjectRoot);

            Assert.That(origin, Is.EqualTo("Assets/Plugins/lib.jar"));
        }

        [Test]
        public void ResolveStableOrigin_OutsideAllRoots_FallsBackToProjectRelative()
        {
            string origin = AndroidArtifactScanner.ResolveStableOrigin(
                "/proj/Library/Other/x.jar", _roots, ProjectRoot);

            Assert.That(origin, Is.EqualTo("Library/Other/x.jar"));
        }
    }
}
