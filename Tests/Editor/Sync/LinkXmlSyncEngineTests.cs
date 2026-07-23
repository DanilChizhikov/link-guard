using System.Linq;
using NUnit.Framework;

namespace DTech.LinkGuard.Editor.Tests
{
    [TestFixture]
    internal sealed class LinkXmlSyncEngineTests
    {
        [Test]
        public void Sync_TrackedNamespaceWithNewType_AddsCollapsedNamespace()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"B\">\n"
                + "        <type fullname=\"B.Foo\" preserve=\"all\" />\n"
                + "    </assembly>\n"
                + "</linker>\n";

            FakeProjectTypeSource source = new FakeProjectTypeSource()
                .Assembly("B", "B.Foo", "B.Bar");

            LinkXmlSyncOutcome outcome = LinkXmlSyncEngine.Sync(xml, source);

            Assert.That(outcome.Success, Is.True);
            Assert.That(outcome.Changed, Is.True);
            Assert.That(outcome.Xml, Does.Contain("<namespace fullname=\"B\" preserve=\"all\" />"));
            Assert.That(outcome.Xml, Does.Contain("<type fullname=\"B.Foo\" preserve=\"all\" />"));
            Assert.That(outcome.AddedNamespaces.Single().AssemblyName, Is.EqualTo("B"));
            Assert.That(outcome.AddedNamespaces.Single().Names, Is.EqualTo(new[] { "B" }));
            Assert.That(outcome.AddedTypes, Is.Empty);
        }

        [Test]
        public void Sync_EveryTypeAlreadyListed_ReturnsInputVerbatim()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"B\">\n"
                + "        <type fullname=\"B.Foo\" preserve=\"all\" />\n"
                + "        <type fullname=\"B.Bar\" preserve=\"all\" />\n"
                + "    </assembly>\n"
                + "</linker>\n";

            FakeProjectTypeSource source = new FakeProjectTypeSource()
                .Assembly("B", "B.Foo", "B.Bar");

            LinkXmlSyncOutcome outcome = LinkXmlSyncEngine.Sync(xml, source);

            Assert.That(outcome.Changed, Is.False);
            Assert.That(outcome.Xml, Is.EqualTo(xml));
        }

        [Test]
        public void Sync_NamespaceAlreadyCollapsed_NoChange()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"B\">\n"
                + "        <namespace fullname=\"B\" preserve=\"all\" />\n"
                + "        <type fullname=\"B.Foo\" preserve=\"all\" />\n"
                + "    </assembly>\n"
                + "</linker>\n";

            FakeProjectTypeSource source = new FakeProjectTypeSource()
                .Assembly("B", "B.Foo", "B.Bar");

            LinkXmlSyncOutcome outcome = LinkXmlSyncEngine.Sync(xml, source);

            Assert.That(outcome.Changed, Is.False);
            Assert.That(outcome.Xml, Is.EqualTo(xml));
        }

        [Test]
        public void Sync_WildcardTypeEntry_CountsAsNamespaceCoverage()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"B\">\n"
                + "        <type fullname=\"B.*\" preserve=\"all\" />\n"
                + "        <type fullname=\"B.Foo\" preserve=\"all\" />\n"
                + "    </assembly>\n"
                + "</linker>\n";

            FakeProjectTypeSource source = new FakeProjectTypeSource()
                .Assembly("B", "B.Foo", "B.Bar");

            LinkXmlSyncOutcome outcome = LinkXmlSyncEngine.Sync(xml, source);

            Assert.That(outcome.Changed, Is.False);
        }

        [Test]
        public void Sync_AssemblyPreservesAll_LeftUntouched()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"B\" preserve=\"all\" />\n"
                + "    <assembly fullname=\"C\" />\n"
                + "</linker>\n";

            FakeProjectTypeSource source = new FakeProjectTypeSource()
                .Assembly("B", "B.Foo", "B.Bar")
                .Assembly("C", "C.Foo");

            LinkXmlSyncOutcome outcome = LinkXmlSyncEngine.Sync(xml, source);

            Assert.That(outcome.Changed, Is.False);
            Assert.That(outcome.Xml, Is.EqualTo(xml));
        }

        [Test]
        public void Sync_NarrowPreserveOnly_NamespaceIsNotTracked()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"B\">\n"
                + "        <type fullname=\"B.Foo\" preserve=\"fields\" />\n"
                + "    </assembly>\n"
                + "</linker>\n";

            FakeProjectTypeSource source = new FakeProjectTypeSource()
                .Assembly("B", "B.Foo", "B.Bar");

            LinkXmlSyncOutcome outcome = LinkXmlSyncEngine.Sync(xml, source);

            Assert.That(outcome.Changed, Is.False);
            Assert.That(outcome.Xml, Is.EqualTo(xml));
        }

        [Test]
        public void Sync_TrackedNamespaceWithNarrowSibling_AddsExplicitTypesInstead()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"B\">\n"
                + "        <type fullname=\"B.Foo\" preserve=\"all\" />\n"
                + "        <type fullname=\"B.Bar\" preserve=\"fields\" />\n"
                + "    </assembly>\n"
                + "</linker>\n";

            FakeProjectTypeSource source = new FakeProjectTypeSource()
                .Assembly("B", "B.Foo", "B.Bar", "B.Baz");

            LinkXmlSyncOutcome outcome = LinkXmlSyncEngine.Sync(xml, source);

            Assert.That(outcome.Changed, Is.True);
            Assert.That(outcome.Xml, Does.Not.Contain("<namespace"));
            Assert.That(outcome.Xml, Does.Contain("<type fullname=\"B.Baz\" preserve=\"all\" />"));
            Assert.That(outcome.Xml, Does.Contain("<type fullname=\"B.Bar\" preserve=\"fields\" />"));
            Assert.That(outcome.AddedTypes.Single().Names, Is.EqualTo(new[] { "B.Baz" }));
        }

        [Test]
        public void Sync_GlobalNamespace_AddsExplicitTypes()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"B\">\n"
                + "        <type fullname=\"Foo\" preserve=\"all\" />\n"
                + "    </assembly>\n"
                + "</linker>\n";

            FakeProjectTypeSource source = new FakeProjectTypeSource()
                .Assembly("B", "Foo", "Bar");

            LinkXmlSyncOutcome outcome = LinkXmlSyncEngine.Sync(xml, source);

            Assert.That(outcome.Changed, Is.True);
            Assert.That(outcome.Xml, Does.Not.Contain("<namespace"));
            Assert.That(outcome.Xml, Does.Contain("<type fullname=\"Bar\" preserve=\"all\" />"));
        }

        [Test]
        public void Sync_NestedTypeEntry_TracksDeclaringNamespace()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"B\">\n"
                + "        <type fullname=\"B.Outer/Inner\" preserve=\"all\" />\n"
                + "    </assembly>\n"
                + "</linker>\n";

            FakeProjectTypeSource source = new FakeProjectTypeSource()
                .Assembly("B", "B.Outer/Inner", "B.Other");

            LinkXmlSyncOutcome outcome = LinkXmlSyncEngine.Sync(xml, source);

            Assert.That(outcome.Changed, Is.True);
            Assert.That(outcome.Xml, Does.Contain("<namespace fullname=\"B\" preserve=\"all\" />"));
        }

        [Test]
        public void Sync_AssemblyMissingFromScan_LeftUntouched()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"Unknown\">\n"
                + "        <type fullname=\"Unknown.Foo\" preserve=\"all\" />\n"
                + "    </assembly>\n"
                + "</linker>\n";

            FakeProjectTypeSource source = new FakeProjectTypeSource()
                .Assembly("B", "B.Foo");

            LinkXmlSyncOutcome outcome = LinkXmlSyncEngine.Sync(xml, source);

            Assert.That(outcome.Changed, Is.False);
            Assert.That(outcome.Xml, Is.EqualTo(xml));
        }

        [Test]
        public void Sync_KeepsCommentsAndCustomAttributes()
        {
            string xml =
                "<linker>\n"
                + "    <!-- Project assemblies -->\n"
                + "    <assembly fullname=\"B\" ignoreIfMissing=\"1\" custom=\"value\">\n"
                + "        <type fullname=\"B.Foo\" preserve=\"all\" note=\"keep\" />\n"
                + "    </assembly>\n"
                + "</linker>\n";

            FakeProjectTypeSource source = new FakeProjectTypeSource()
                .Assembly("B", "B.Foo", "B.Bar");

            LinkXmlSyncOutcome outcome = LinkXmlSyncEngine.Sync(xml, source);

            Assert.That(outcome.Changed, Is.True);
            Assert.That(outcome.Xml, Does.Contain("<!-- Project assemblies -->"));
            Assert.That(outcome.Xml, Does.Contain("ignoreIfMissing=\"1\""));
            Assert.That(outcome.Xml, Does.Contain("custom=\"value\""));
            Assert.That(outcome.Xml, Does.Contain("note=\"keep\""));
        }

        [Test]
        public void Sync_UntrackedProjectAssembly_ReportedWithoutWriting()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"B\" preserve=\"all\" />\n"
                + "</linker>\n";

            FakeProjectTypeSource source = new FakeProjectTypeSource()
                .Assembly("B", "B.Foo")
                .ProjectAssembly("Game.NewFeature", "Game.NewFeature.Boot");

            LinkXmlSyncOutcome outcome = LinkXmlSyncEngine.Sync(xml, source);

            Assert.That(outcome.Changed, Is.False);
            Assert.That(outcome.UntrackedAssemblies, Is.EqualTo(new[] { "Game.NewFeature" }));
        }

        [Test]
        public void Sync_ScopePattern_CreatesMissingAssemblyEntry()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"B\" preserve=\"all\" />\n"
                + "</linker>\n";

            FakeProjectTypeSource source = new FakeProjectTypeSource()
                .Assembly("B", "B.Foo")
                .ProjectAssembly("Game.NewFeature", "Game.NewFeature.Boot");

            LinkXmlSyncOutcome outcome = LinkXmlSyncEngine.Sync(xml, source, new[] { "Game.*" });

            Assert.That(outcome.Changed, Is.True);
            Assert.That(outcome.AddedAssemblies, Is.EqualTo(new[] { "Game.NewFeature" }));
            Assert.That(outcome.Xml, Does.Contain("<assembly fullname=\"Game.NewFeature\" preserve=\"all\" />"));
        }

        [Test]
        public void Sync_ScopePatternOnNamespace_AddsNamespaceToExistingAssembly()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"B\">\n"
                + "        <type fullname=\"B.Foo\" preserve=\"all\" />\n"
                + "    </assembly>\n"
                + "</linker>\n";

            FakeProjectTypeSource source = new FakeProjectTypeSource()
                .Assembly("B", "B.Foo", "B.Feature.Entry");

            LinkXmlSyncOutcome outcome = LinkXmlSyncEngine.Sync(xml, source, new[] { "B.Feature*" });

            Assert.That(outcome.Changed, Is.True);
            Assert.That(outcome.Xml, Does.Contain("<namespace fullname=\"B.Feature\" preserve=\"all\" />"));
        }

        [Test]
        public void Sync_RunTwice_IsIdempotent()
        {
            string xml =
                "<linker>\n"
                + "    <assembly fullname=\"B\">\n"
                + "        <type fullname=\"B.Foo\" preserve=\"all\" />\n"
                + "    </assembly>\n"
                + "</linker>\n";

            FakeProjectTypeSource source = new FakeProjectTypeSource()
                .Assembly("B", "B.Foo", "B.Bar");

            LinkXmlSyncOutcome first = LinkXmlSyncEngine.Sync(xml, source);
            LinkXmlSyncOutcome second = LinkXmlSyncEngine.Sync(first.Xml, source);

            Assert.That(first.Changed, Is.True);
            Assert.That(second.Changed, Is.False);
            Assert.That(second.Xml, Is.EqualTo(first.Xml));
        }

        [Test]
        public void Sync_MalformedXml_FailsWithoutContent()
        {
            FakeProjectTypeSource source = new FakeProjectTypeSource().Assembly("B", "B.Foo");

            LinkXmlSyncOutcome outcome = LinkXmlSyncEngine.Sync("<linker><assembly", source);

            Assert.That(outcome.Success, Is.False);
            Assert.That(outcome.Changed, Is.False);
            Assert.That(outcome.FailureReason, Is.Not.Empty);
        }

        [Test]
        public void Sync_RootIsNotLinker_Fails()
        {
            FakeProjectTypeSource source = new FakeProjectTypeSource().Assembly("B", "B.Foo");

            LinkXmlSyncOutcome outcome = LinkXmlSyncEngine.Sync("<root />", source);

            Assert.That(outcome.Success, Is.False);
            Assert.That(outcome.FailureReason, Does.Contain("linker"));
        }
    }
}
