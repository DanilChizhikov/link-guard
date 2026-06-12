using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.TestTools;

namespace DTech.LinkGuard.Editor.Tests
{
    [TestFixture]
    internal sealed class LinkXmlPatcherTests
    {
        private string _tempDirectory;
        private string _outputPath;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "LinkGuardPatcherTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
            _outputPath = Path.Combine(_tempDirectory, "link.xml");
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_tempDirectory) && Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        [Test]
        public void Patch_TwoContentProviders_WritesMergedFile()
        {
            FakeLinkXmlMergeProvider a = ContentProvider("a", "AssemblyA");
            FakeLinkXmlMergeProvider b = ContentProvider("b", "AssemblyB");

            LinkXmlPatchReport report = LinkXmlPatcher.Patch(new[] { a, b }, _outputPath, throwOnError: false);

            Assert.That(report.Success, Is.True);
            Assert.That(report.Written, Is.True);
            Assert.That(File.Exists(_outputPath), Is.True);
            Assert.That(report.Providers, Has.Count.EqualTo(2));
            Assert.That(report.Providers.All(p => p.Success && p.ContributedContent), Is.True);

            string written = File.ReadAllText(_outputPath);
            Assert.That(written, Does.Contain("AssemblyA"));
            Assert.That(written, Does.Contain("AssemblyB"));
            Assert.That(report.Xml, Is.EqualTo(written));
        }

        [Test]
        public void Patch_FailureProvider_NoThrow_WritesRestAndMarksAggregateFailed()
        {
            FakeLinkXmlMergeProvider ok = ContentProvider("ok", "AssemblyA");
            FakeLinkXmlMergeProvider bad = new FakeLinkXmlMergeProvider(
                "bad", () => LinkXmlProviderResult.Failure("scan exploded"));

            LogAssert.Expect(LogType.Error, new Regex("scan exploded"));

            LinkXmlPatchReport report = LinkXmlPatcher.Patch(new[] { ok, bad }, _outputPath, throwOnError: false);

            Assert.That(report.Success, Is.False);
            Assert.That(report.Written, Is.True);
            Assert.That(File.Exists(_outputPath), Is.True);
            Assert.That(File.ReadAllText(_outputPath), Does.Contain("AssemblyA"));

            LinkXmlPatchProviderReport badReport = report.Providers.Single(p => p.ProviderId == "bad");
            Assert.That(badReport.Success, Is.False);
            Assert.That(badReport.ContributedContent, Is.False);
            Assert.That(badReport.Report, Does.Contain("scan exploded"));
        }

        [Test]
        public void Patch_ThrowingProvider_ThrowOnError_ThrowsAndDoesNotWrite()
        {
            FakeLinkXmlMergeProvider ok = ContentProvider("ok", "AssemblyA");
            FakeLinkXmlMergeProvider boom = new FakeLinkXmlMergeProvider(
                "boom", () => throw new InvalidOperationException("kaboom"));

            LogAssert.Expect(LogType.Error, new Regex("Merge provider 'boom' threw"));

            BuildFailedException exception = Assert.Throws<BuildFailedException>(
                () => LinkXmlPatcher.Patch(new[] { ok, boom }, _outputPath, throwOnError: true));

            Assert.That(exception!.Message, Does.Contain("boom"));
            Assert.That(File.Exists(_outputPath), Is.False);
        }

        [Test]
        public void Patch_ThrowingProvider_NoThrow_RecordsFailureAndMergesRest()
        {
            FakeLinkXmlMergeProvider ok = ContentProvider("ok", "AssemblyA");
            FakeLinkXmlMergeProvider boom = new FakeLinkXmlMergeProvider(
                "boom", () => throw new InvalidOperationException("kaboom"));

            LogAssert.Expect(LogType.Error, new Regex("Merge provider 'boom' threw"));

            LinkXmlPatchReport report = LinkXmlPatcher.Patch(new[] { ok, boom }, _outputPath, throwOnError: false);

            Assert.That(report.Success, Is.False);
            Assert.That(report.Written, Is.True);
            Assert.That(File.ReadAllText(_outputPath), Does.Contain("AssemblyA"));

            LinkXmlPatchProviderReport boomReport = report.Providers.Single(p => p.ProviderId == "boom");
            Assert.That(boomReport.Success, Is.False);
            Assert.That(boomReport.Report, Does.Contain("kaboom"));
        }

        [Test]
        public void Patch_NullResultProvider_RecordsFailure()
        {
            FakeLinkXmlMergeProvider nullish = new FakeLinkXmlMergeProvider("nullish", () => null);

            LogAssert.Expect(LogType.Error, new Regex("Merge provider 'nullish' returned no result"));

            LinkXmlPatchReport report = LinkXmlPatcher.Patch(new[] { nullish }, _outputPath, throwOnError: false);

            Assert.That(report.Success, Is.False);
            Assert.That(report.Written, Is.False);
            Assert.That(File.Exists(_outputPath), Is.False);
        }

        [Test]
        public void Patch_AllProvidersEmpty_NothingWritten_AggregateSuccess()
        {
            FakeLinkXmlMergeProvider a = new FakeLinkXmlMergeProvider("a", () => LinkXmlProviderResult.Empty());
            FakeLinkXmlMergeProvider b = new FakeLinkXmlMergeProvider("b", () => LinkXmlProviderResult.Empty());

            LinkXmlPatchReport report = LinkXmlPatcher.Patch(new[] { a, b }, _outputPath, throwOnError: true);

            Assert.That(report.Success, Is.True);
            Assert.That(report.Written, Is.False);
            Assert.That(report.Xml, Is.Empty);
            Assert.That(File.Exists(_outputPath), Is.False);
            Assert.That(report.Providers.All(p => p.Success && !p.ContributedContent), Is.True);
        }

        [Test]
        public void Patch_EmptyProviderList_NothingWritten_AggregateSuccess()
        {
            LinkXmlPatchReport report = LinkXmlPatcher.Patch(
                Array.Empty<ILinkXmlMergeProvider>(), _outputPath, throwOnError: true);

            Assert.That(report.Success, Is.True);
            Assert.That(report.Written, Is.False);
            Assert.That(File.Exists(_outputPath), Is.False);
        }

        [Test]
        public void Patch_MalformedProviderXml_DowngradesProviderToFailed()
        {
            FakeLinkXmlMergeProvider ok = ContentProvider("ok", "AssemblyA");
            FakeLinkXmlMergeProvider malformed = new FakeLinkXmlMergeProvider(
                "malformed",
                () => new LinkXmlProviderResult("<linker><broken", "report", null, true));

            LogAssert.Expect(LogType.Error, new Regex("skipped by the merger"));

            LinkXmlPatchReport report = LinkXmlPatcher.Patch(new[] { ok, malformed }, _outputPath, throwOnError: false);

            Assert.That(report.Success, Is.False);
            Assert.That(report.Written, Is.True);
            Assert.That(File.ReadAllText(_outputPath), Does.Contain("AssemblyA"));

            LinkXmlPatchProviderReport malformedReport = report.Providers.Single(p => p.ProviderId == "malformed");
            Assert.That(malformedReport.Success, Is.False);
            Assert.That(malformedReport.ContributedContent, Is.False);
            Assert.That(malformedReport.Report, Does.Contain("skipped by the merger"));
        }

        [Test]
        public void Patch_MalformedProviderXml_ThrowOnError_ThrowsAndDoesNotWrite()
        {
            FakeLinkXmlMergeProvider malformed = new FakeLinkXmlMergeProvider(
                "malformed",
                () => new LinkXmlProviderResult("<linker><broken", "report", null, true));

            LogAssert.Expect(LogType.Error, new Regex("skipped by the merger"));

            Assert.Throws<BuildFailedException>(
                () => LinkXmlPatcher.Patch(new[] { malformed }, _outputPath, throwOnError: true));

            Assert.That(File.Exists(_outputPath), Is.False);
        }

        [Test]
        public void Discover_DoesNotContainParameterizedCtorFakes()
        {
            Assert.That(
                LinkXmlMergeProviderRegistry.Discover().Any(p => p is FakeLinkXmlMergeProvider),
                Is.False);
        }

        private static FakeLinkXmlMergeProvider ContentProvider(string id, string assemblyName)
        {
            string xml = $"<linker><assembly fullname=\"{assemblyName}\" preserve=\"all\"/></linker>";
            return new FakeLinkXmlMergeProvider(
                id, () => new LinkXmlProviderResult(xml, "ok", null, true));
        }
    }
}
