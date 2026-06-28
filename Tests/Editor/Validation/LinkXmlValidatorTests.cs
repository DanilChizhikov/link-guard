using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.TestTools;

namespace DTech.LinkGuard.Editor.Tests
{
    [TestFixture]
    internal sealed class LinkXmlValidatorTests
    {
        private string _tempDirectory;
        private string _path;

        private const string ChangedXml =
            "<linker>\n"
            + "    <assembly fullname=\"Gone\" preserve=\"all\" />\n"
            + "    <assembly fullname=\"Keep\" preserve=\"all\" />\n"
            + "</linker>\n";

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "LinkGuardValidatorTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
            _path = Path.Combine(_tempDirectory, "link.xml");
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
        public void Validate_FileMissing_SuccessNotWritten_NoThrowEvenWithThrowOnError()
        {
            LinkXmlValidationReport report = LinkXmlValidator.Validate(
                new FakeBuildMembershipOracle(), _path, apply: true, throwOnError: true);

            Assert.That(report.Success, Is.True);
            Assert.That(report.FileExisted, Is.False);
            Assert.That(report.Changed, Is.False);
            Assert.That(report.Written, Is.False);
            Assert.That(File.Exists(_path), Is.False);
        }

        [Test]
        public void Validate_MalformedFile_NoThrow_ReportsFailure()
        {
            File.WriteAllText(_path, "<linker><broken");
            LogAssert.Expect(LogType.Error, new Regex("validation failed"));

            LinkXmlValidationReport report = LinkXmlValidator.Validate(
                new FakeBuildMembershipOracle(), _path, apply: true, throwOnError: false);

            Assert.That(report.Success, Is.False);
            Assert.That(report.Written, Is.False);
            Assert.That(report.FailureReason, Is.Not.Empty);
        }

        [Test]
        public void Validate_MalformedFile_ThrowOnError_ThrowsAndDoesNotWrite()
        {
            File.WriteAllText(_path, "<linker><broken");
            LogAssert.Expect(LogType.Error, new Regex("validation failed"));

            Assert.Throws<BuildFailedException>(() => LinkXmlValidator.Validate(
                new FakeBuildMembershipOracle(), _path, apply: true, throwOnError: true));

            Assert.That(File.ReadAllText(_path), Is.EqualTo("<linker><broken"));
        }

        [Test]
        public void Validate_ApplyFalse_Changed_FileUntouched()
        {
            File.WriteAllText(_path, ChangedXml);

            FakeBuildMembershipOracle oracle = new FakeBuildMembershipOracle()
                .Assembly("Gone", BuildPresence.Missing)
                .Assembly("Keep", BuildPresence.Present);

            LinkXmlValidationReport report = LinkXmlValidator.Validate(oracle, _path, apply: false, throwOnError: false);

            Assert.That(report.Changed, Is.True);
            Assert.That(report.Written, Is.False);
            Assert.That(File.ReadAllText(_path), Is.EqualTo(ChangedXml));
        }

        [Test]
        public void Validate_ApplyTrue_WritesFile_ReportXmlMatchesDisk()
        {
            File.WriteAllText(_path, ChangedXml);

            FakeBuildMembershipOracle oracle = new FakeBuildMembershipOracle()
                .Assembly("Gone", BuildPresence.Missing)
                .Assembly("Keep", BuildPresence.Present);

            LinkXmlValidationReport report = LinkXmlValidator.Validate(oracle, _path, apply: true, throwOnError: false);

            Assert.That(report.Written, Is.True);
            string disk = File.ReadAllText(_path);
            Assert.That(disk, Is.EqualTo(report.Xml));
            Assert.That(disk, Does.Not.Contain("Gone"));
            Assert.That(disk, Does.Contain("Keep"));
        }

        [Test]
        public void Validate_ApplyTrue_NoChanges_FileNotRewritten()
        {
            string clean = "<linker>\n    <assembly fullname=\"Keep\" preserve=\"all\" />\n</linker>\n";
            File.WriteAllText(_path, clean);

            FakeBuildMembershipOracle oracle = new FakeBuildMembershipOracle()
                .Assembly("Keep", BuildPresence.Present);

            LinkXmlValidationReport report = LinkXmlValidator.Validate(oracle, _path, apply: true, throwOnError: false);

            Assert.That(report.Changed, Is.False);
            Assert.That(report.Written, Is.False);
            Assert.That(File.ReadAllText(_path), Is.EqualTo(clean));
        }

        [Test]
        public void Apply_WritesReportXmlExactly()
        {
            File.WriteAllText(_path, ChangedXml);

            FakeBuildMembershipOracle oracle = new FakeBuildMembershipOracle()
                .Assembly("Gone", BuildPresence.Missing)
                .Assembly("Keep", BuildPresence.Present);

            LinkXmlValidationReport report = LinkXmlValidator.Validate(oracle, _path, apply: false, throwOnError: false);
            Assert.That(File.ReadAllText(_path), Is.EqualTo(ChangedXml));

            LinkXmlValidator.Apply(report);

            Assert.That(File.ReadAllText(_path), Is.EqualTo(report.Xml));
        }
    }
}
