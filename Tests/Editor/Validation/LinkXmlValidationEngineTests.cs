using System.Linq;
using NUnit.Framework;

namespace DTech.LinkGuard.Editor.Tests
{
    [TestFixture]
    internal sealed class LinkXmlValidationEngineTests
    {
        [Test]
        public void Validate_AllPresent_NoChanges_ReturnsInputVerbatim()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"A\" preserve=\"all\" />\n"
                + "    <!-- note -->\n"
                + "    <assembly fullname=\"B\">\n"
                + "        <type fullname=\"B.Foo\" preserve=\"all\" />\n"
                + "    </assembly>\n"
                + "</linker>\n";

            FakeBuildMembershipOracle oracle = new FakeBuildMembershipOracle();

            LinkXmlValidationOutcome outcome = LinkXmlValidationEngine.Validate(xml, oracle);

            Assert.That(outcome.Success, Is.True);
            Assert.That(outcome.Changed, Is.False);
            Assert.That(outcome.Xml, Is.EqualTo(xml));
        }

        [Test]
        public void Validate_MissingAssembly_RemovesWholeElement()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"Gone\" preserve=\"all\" />\n"
                + "    <assembly fullname=\"Keep\" preserve=\"all\" />\n"
                + "</linker>\n";

            FakeBuildMembershipOracle oracle = new FakeBuildMembershipOracle()
                .Assembly("Gone", BuildPresence.Missing)
                .Assembly("Keep", BuildPresence.Present);

            LinkXmlValidationOutcome outcome = LinkXmlValidationEngine.Validate(xml, oracle);

            Assert.That(outcome.Changed, Is.True);
            Assert.That(outcome.RemovedAssemblies, Does.Contain("Gone"));
            Assert.That(outcome.Xml, Does.Not.Contain("Gone"));
            Assert.That(outcome.Xml, Does.Contain("Keep"));
            Assert.That(outcome.Xml, Does.Not.Contain("\n\n"));
        }

        [Test]
        public void Validate_MissingAssembly_IgnoreIfMissing_Kept([Values("1", "true")] string flag)
        {
            string xml = $"<linker>\n    <assembly fullname=\"X\" ignoreIfMissing=\"{flag}\" preserve=\"all\" />\n</linker>\n";

            FakeBuildMembershipOracle oracle = new FakeBuildMembershipOracle()
                .Assembly("X", BuildPresence.Missing);

            LinkXmlValidationOutcome outcome = LinkXmlValidationEngine.Validate(xml, oracle);

            Assert.That(outcome.Changed, Is.False);
            Assert.That(outcome.KeptIgnoreIfMissing, Does.Contain("X"));
            Assert.That(outcome.Xml, Is.EqualTo(xml));
        }

        [Test]
        public void Validate_IgnoreIfMissing_AssemblyPresent_TypeChildrenStillValidated()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"X\" ignoreIfMissing=\"1\">\n"
                + "        <type fullname=\"X.Gone\" />\n"
                + "        <type fullname=\"X.Keep\" />\n"
                + "    </assembly>\n"
                + "</linker>\n";

            FakeBuildMembershipOracle oracle = new FakeBuildMembershipOracle()
                .Assembly("X", BuildPresence.Present)
                .Type("X", "X.Gone", BuildPresence.Missing)
                .Type("X", "X.Keep", BuildPresence.Present);

            LinkXmlValidationOutcome outcome = LinkXmlValidationEngine.Validate(xml, oracle);

            Assert.That(outcome.Changed, Is.True);
            Assert.That(outcome.Xml, Does.Not.Contain("X.Gone"));
            Assert.That(outcome.Xml, Does.Contain("X.Keep"));
        }

        [Test]
        public void Validate_MissingType_RemovesTypeKeepsSiblingsAndAssembly()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"X\" preserve=\"all\">\n"
                + "        <type fullname=\"X.Gone\" />\n"
                + "        <type fullname=\"X.Keep\" />\n"
                + "    </assembly>\n"
                + "</linker>\n";

            FakeBuildMembershipOracle oracle = new FakeBuildMembershipOracle()
                .Assembly("X", BuildPresence.Present)
                .Type("X", "X.Gone", BuildPresence.Missing)
                .Type("X", "X.Keep", BuildPresence.Present);

            LinkXmlValidationOutcome outcome = LinkXmlValidationEngine.Validate(xml, oracle);

            Assert.That(outcome.Changed, Is.True);
            Assert.That(outcome.RemovedTypes, Has.Count.EqualTo(1));
            Assert.That(outcome.RemovedTypes[0].AssemblyName, Is.EqualTo("X"));
            Assert.That(outcome.RemovedTypes[0].TypeNames, Does.Contain("X.Gone"));
            Assert.That(outcome.Xml, Does.Contain("X.Keep"));
            Assert.That(outcome.Xml, Does.Contain("<assembly fullname=\"X\""));
        }

        [Test]
        public void Validate_AllTypesRemoved_NoPreserve_RemovesAssembly()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"X\">\n"
                + "        <type fullname=\"X.Gone\" />\n"
                + "    </assembly>\n"
                + "</linker>\n";

            FakeBuildMembershipOracle oracle = new FakeBuildMembershipOracle()
                .Assembly("X", BuildPresence.Present)
                .Type("X", "X.Gone", BuildPresence.Missing);

            LinkXmlValidationOutcome outcome = LinkXmlValidationEngine.Validate(xml, oracle);

            Assert.That(outcome.Changed, Is.True);
            Assert.That(outcome.RemovedAssemblies, Does.Contain("X"));
            Assert.That(outcome.RemovedTypes, Is.Empty);
            Assert.That(outcome.Xml, Does.Not.Contain("<assembly"));
        }

        [Test]
        public void Validate_AllTypesRemoved_PreserveAttrPresent_KeepsAssembly()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"X\" preserve=\"all\">\n"
                + "        <type fullname=\"X.Gone\" />\n"
                + "    </assembly>\n"
                + "</linker>\n";

            FakeBuildMembershipOracle oracle = new FakeBuildMembershipOracle()
                .Assembly("X", BuildPresence.Present)
                .Type("X", "X.Gone", BuildPresence.Missing);

            LinkXmlValidationOutcome outcome = LinkXmlValidationEngine.Validate(xml, oracle);

            Assert.That(outcome.RemovedAssemblies, Is.Empty);
            Assert.That(outcome.RemovedTypes, Has.Count.EqualTo(1));
            Assert.That(outcome.Xml, Does.Contain("<assembly fullname=\"X\""));
            Assert.That(outcome.Xml, Does.Not.Contain("X.Gone"));
        }

        [Test]
        public void Validate_AllTypesRemoved_SurvivingNonTypeChild_KeepsAssembly()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"X\">\n"
                + "        <type fullname=\"X.Gone\" />\n"
                + "        <namespace fullname=\"X.Ns\" />\n"
                + "    </assembly>\n"
                + "</linker>\n";

            FakeBuildMembershipOracle oracle = new FakeBuildMembershipOracle()
                .Assembly("X", BuildPresence.Present)
                .Type("X", "X.Gone", BuildPresence.Missing);

            LinkXmlValidationOutcome outcome = LinkXmlValidationEngine.Validate(xml, oracle);

            Assert.That(outcome.RemovedAssemblies, Is.Empty);
            Assert.That(outcome.Xml, Does.Contain("<namespace fullname=\"X.Ns\""));
            Assert.That(outcome.Xml, Does.Not.Contain("X.Gone"));
        }

        [Test]
        public void Validate_WildcardType_AlwaysKept_NeverQueried()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"X\" preserve=\"all\">\n"
                + "        <type fullname=\"X.*\" />\n"
                + "    </assembly>\n"
                + "</linker>\n";

            FakeBuildMembershipOracle oracle = new FakeBuildMembershipOracle { DefaultType = BuildPresence.Missing }
                .Assembly("X", BuildPresence.Present);

            LinkXmlValidationOutcome outcome = LinkXmlValidationEngine.Validate(xml, oracle);

            Assert.That(outcome.Changed, Is.False);
            Assert.That(outcome.Xml, Is.EqualTo(xml));
            Assert.That(oracle.TypeQueries.Any(q => q.type == "X.*"), Is.False);
        }

        [Test]
        public void Validate_NestedType_QueriedInLinkerFormat()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"X\" preserve=\"all\">\n"
                + "        <type fullname=\"X.Outer/Inner\" />\n"
                + "    </assembly>\n"
                + "</linker>\n";

            FakeBuildMembershipOracle oracle = new FakeBuildMembershipOracle()
                .Assembly("X", BuildPresence.Present)
                .Type("X", "X.Outer/Inner", BuildPresence.Missing);

            LinkXmlValidationOutcome outcome = LinkXmlValidationEngine.Validate(xml, oracle);

            Assert.That(oracle.TypeQueries, Does.Contain(("X", "X.Outer/Inner")));
            Assert.That(outcome.Changed, Is.True);
            Assert.That(outcome.Xml, Does.Not.Contain("X.Outer/Inner"));
        }

        [Test]
        public void Validate_UnknownAssembly_Kept_Recorded()
        {
            string xml = "<linker>\n    <assembly fullname=\"X\" preserve=\"all\" />\n</linker>\n";

            FakeBuildMembershipOracle oracle = new FakeBuildMembershipOracle()
                .Assembly("X", BuildPresence.Unknown);

            LinkXmlValidationOutcome outcome = LinkXmlValidationEngine.Validate(xml, oracle);

            Assert.That(outcome.Changed, Is.False);
            Assert.That(outcome.KeptUnknown.Any(e => e.AssemblyName == "X" && e.TypeName == string.Empty), Is.True);
        }

        [Test]
        public void Validate_UnknownType_Kept_Recorded()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"X\" preserve=\"all\">\n"
                + "        <type fullname=\"X.Generic\" />\n"
                + "    </assembly>\n"
                + "</linker>\n";

            FakeBuildMembershipOracle oracle = new FakeBuildMembershipOracle()
                .Assembly("X", BuildPresence.Present)
                .Type("X", "X.Generic", BuildPresence.Unknown);

            LinkXmlValidationOutcome outcome = LinkXmlValidationEngine.Validate(xml, oracle);

            Assert.That(outcome.Changed, Is.False);
            Assert.That(outcome.KeptUnknown.Any(e => e.TypeName == "X.Generic"), Is.True);
            Assert.That(outcome.Xml, Is.EqualTo(xml));
        }

        [Test]
        public void Validate_PreserveAllAssemblyNoChildren_DoesNotQueryTypes()
        {
            string xml = "<linker>\n    <assembly fullname=\"X\" preserve=\"all\" />\n</linker>\n";

            FakeBuildMembershipOracle oracle = new FakeBuildMembershipOracle { DefaultType = BuildPresence.Missing }
                .Assembly("X", BuildPresence.Present);

            LinkXmlValidationOutcome outcome = LinkXmlValidationEngine.Validate(xml, oracle);

            Assert.That(oracle.TypeQueries, Is.Empty);
            Assert.That(outcome.Changed, Is.False);
        }

        [Test]
        public void Validate_KeptTypeWithMethodAndCustomAttrs_SurvivesSerialization()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"X\" preserve=\"all\">\n"
                + "        <type fullname=\"X.Gone\" />\n"
                + "        <type fullname=\"X.Keep\" feature=\"foo\">\n"
                + "            <method signature=\"bar\" />\n"
                + "        </type>\n"
                + "    </assembly>\n"
                + "</linker>\n";

            FakeBuildMembershipOracle oracle = new FakeBuildMembershipOracle()
                .Assembly("X", BuildPresence.Present)
                .Type("X", "X.Gone", BuildPresence.Missing)
                .Type("X", "X.Keep", BuildPresence.Present);

            LinkXmlValidationOutcome outcome = LinkXmlValidationEngine.Validate(xml, oracle);

            Assert.That(outcome.Changed, Is.True);
            Assert.That(outcome.Xml, Does.Not.Contain("X.Gone"));
            Assert.That(outcome.Xml, Does.Contain("X.Keep"));
            Assert.That(outcome.Xml, Does.Contain("feature=\"foo\""));
            Assert.That(outcome.Xml, Does.Contain("<method signature=\"bar\""));
        }

        [Test]
        public void Validate_CommentsAndRootAttributes_SurviveSiblingRemoval()
        {
            string xml =
                "<linker x=\"y\">\n"
                + "    <!-- group -->\n"
                + "    <assembly fullname=\"Gone\" preserve=\"all\" />\n"
                + "    <assembly fullname=\"Keep\" preserve=\"all\" />\n"
                + "</linker>\n";

            FakeBuildMembershipOracle oracle = new FakeBuildMembershipOracle()
                .Assembly("Gone", BuildPresence.Missing)
                .Assembly("Keep", BuildPresence.Present);

            LinkXmlValidationOutcome outcome = LinkXmlValidationEngine.Validate(xml, oracle);

            Assert.That(outcome.Xml, Does.Contain("<!-- group -->"));
            Assert.That(outcome.Xml, Does.Contain("x=\"y\""));
            Assert.That(outcome.Xml, Does.Contain("Keep"));
            Assert.That(outcome.Xml, Does.Not.Contain("Gone"));
            Assert.That(outcome.Xml, Does.Not.Contain("\n\n"));
        }

        [Test]
        public void Validate_AssemblyWithoutFullname_Kept_Recorded()
        {
            string xml = "<linker>\n    <assembly preserve=\"all\" />\n</linker>\n";

            FakeBuildMembershipOracle oracle = new FakeBuildMembershipOracle { DefaultAssembly = BuildPresence.Missing };

            LinkXmlValidationOutcome outcome = LinkXmlValidationEngine.Validate(xml, oracle);

            Assert.That(outcome.Changed, Is.False);
            Assert.That(outcome.KeptUnknown, Has.Count.EqualTo(1));
            Assert.That(outcome.Xml, Is.EqualTo(xml));
        }

        [Test]
        public void Validate_MalformedXml_Fails()
        {
            LinkXmlValidationOutcome outcome = LinkXmlValidationEngine.Validate("<linker><broken", new FakeBuildMembershipOracle());

            Assert.That(outcome.Success, Is.False);
            Assert.That(outcome.FailureReason, Is.Not.Empty);
        }

        [Test]
        public void Validate_RootNotLinker_Fails()
        {
            LinkXmlValidationOutcome outcome = LinkXmlValidationEngine.Validate("<root />", new FakeBuildMembershipOracle());

            Assert.That(outcome.Success, Is.False);
        }

        [Test]
        public void Validate_EmptyLinker_SuccessNoChange()
        {
            LinkXmlValidationOutcome outcome = LinkXmlValidationEngine.Validate("<linker />", new FakeBuildMembershipOracle());

            Assert.That(outcome.Success, Is.True);
            Assert.That(outcome.Changed, Is.False);
        }

        [Test]
        public void Validate_XmlDeclarationAndTrailingNewline_Preserved()
        {
            string xml =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
                + "<linker>\n"
                + "    <assembly fullname=\"Gone\" preserve=\"all\" />\n"
                + "    <assembly fullname=\"Keep\" preserve=\"all\" />\n"
                + "</linker>\n";

            FakeBuildMembershipOracle oracle = new FakeBuildMembershipOracle()
                .Assembly("Gone", BuildPresence.Missing)
                .Assembly("Keep", BuildPresence.Present);

            LinkXmlValidationOutcome outcome = LinkXmlValidationEngine.Validate(xml, oracle);

            Assert.That(outcome.Changed, Is.True);
            Assert.That(outcome.Xml, Does.StartWith("<?xml"));
            Assert.That(outcome.Xml, Does.EndWith("\n"));
            Assert.That(outcome.Xml, Does.Contain("Keep"));
            Assert.That(outcome.Xml, Does.Not.Contain("Gone"));
        }
    }
}
