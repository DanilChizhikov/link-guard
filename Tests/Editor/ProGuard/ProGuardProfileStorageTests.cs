using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DTech.LinkGuard.Editor.ProGuard.Tests
{
    [TestFixture]
    public sealed class ProGuardProfileStorageTests
    {
        [Test]
        public void ApplyProfile_UsesStableKey_WhenArtifactNamesAreDuplicated()
        {
            AndroidArtifactEntry first = MakeEntry("classes.jar",
                AndroidArtifactSource.Jar,
                "Assets/Plugins/A/classes.jar",
                MakeClass("com.first", "KeepMe"));
            first.Classes.Single().SelectAll(true);

            AndroidArtifactEntry second = MakeEntry("classes.jar",
                AndroidArtifactSource.Aar,
                "Assets/Plugins/B/classes.jar",
                MakeClass("com.second", "KeepAll"));
            second.SelectAll(true);

            ProGuardProfile profile = ProGuardProfileStorage.ToProfile(new[] { first, second });

            AndroidArtifactEntry restoredFirst = MakeEntry("classes.jar", AndroidArtifactSource.Jar,
                "Assets/Plugins/A/classes.jar", MakeClass("com.first", "KeepMe"));
            AndroidArtifactEntry restoredSecond = MakeEntry("classes.jar", AndroidArtifactSource.Aar,
                "Assets/Plugins/B/classes.jar", MakeClass("com.second", "KeepAll"));

            ProGuardProfileStorage.ApplyProfile(profile,
                new List<AndroidArtifactEntry> { restoredFirst, restoredSecond });

            Assert.That(restoredFirst.Classes.Single().IsSelected, Is.True);
            Assert.That(restoredFirst.IsArtifactSelected, Is.False);
            Assert.That(restoredSecond.IsArtifactSelected, Is.True);
            Assert.That(restoredSecond.Classes.Single().IsSelected, Is.True);
        }

        [Test]
        public void ApplyProfile_LoadsLegacyArtifactName_WhenNameIsUnique()
        {
            ProGuardProfile profile = new ProGuardProfile
            {
                Selections = new List<ProGuardSelection>
                {
                    new ProGuardSelection
                    {
                        Artifact = "unique.jar",
                        Classes = new List<string> { "com.unique.KeepMe" }
                    }
                }
            };
            AndroidArtifactEntry entry = MakeEntry("unique.jar",
                AndroidArtifactSource.Jar,
                "Assets/Plugins/unique.jar",
                MakeClass("com.unique", "KeepMe"));

            ProGuardProfileStorage.ApplyProfile(profile, new List<AndroidArtifactEntry> { entry });

            Assert.That(entry.Classes.Single().IsSelected, Is.True);
        }

        [Test]
        public void ApplyProfile_SkipsLegacyArtifactName_WhenNameIsDuplicated()
        {
            ProGuardProfile profile = new ProGuardProfile
            {
                Selections = new List<ProGuardSelection>
                {
                    new ProGuardSelection
                    {
                        Artifact = "classes.jar",
                        Classes = new List<string> { "com.first.KeepMe" }
                    }
                }
            };
            AndroidArtifactEntry first = MakeEntry("classes.jar",
                AndroidArtifactSource.Jar,
                "Assets/Plugins/A/classes.jar",
                MakeClass("com.first", "KeepMe"));
            AndroidArtifactEntry second = MakeEntry("classes.jar",
                AndroidArtifactSource.Jar,
                "Assets/Plugins/B/classes.jar",
                MakeClass("com.second", "KeepMe"));

            ProGuardProfileStorage.ApplyProfile(profile, new List<AndroidArtifactEntry> { first, second });

            Assert.That(first.ProducesEntry, Is.False);
            Assert.That(second.ProducesEntry, Is.False);
        }

        private static JavaClassEntry MakeClass(string package, string name)
        {
            string fullname = string.IsNullOrEmpty(package) ? name : package + "." + name;
            return new JavaClassEntry(package, fullname, name);
        }

        private static AndroidArtifactEntry MakeEntry(string name,
            AndroidArtifactSource source,
            string originPath,
            params JavaClassEntry[] classes)
        {
            return new AndroidArtifactEntry(name, source, originPath, classes);
        }
    }
}
