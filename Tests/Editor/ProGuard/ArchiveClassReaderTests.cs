using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using NUnit.Framework;

namespace DTech.LinkGuard.Editor.ProGuard.Tests
{
    [TestFixture]
    public sealed class ArchiveClassReaderTests
    {
        [Test]
        public void ReadClassEntryPaths_Jar_ReturnsOnlyClassEntries()
        {
            byte[] jar = CreateZip(
                ("com/foo/Bar.class", null),
                ("com/foo/Bar$Inner.class", null),
                ("META-INF/MANIFEST.MF", null));

            List<string> classes = ArchiveClassReader.ReadClassEntryPaths(new MemoryStream(jar), isAar: false);

            Assert.That(classes, Is.EquivalentTo(new[] { "com/foo/Bar.class", "com/foo/Bar$Inner.class" }));
        }

        [Test]
        public void ReadClassEntryPaths_Aar_ReadsNestedClassesJarAndLibs()
        {
            byte[] classesJar = CreateZip(("com/foo/A.class", null), ("META-INF/x", null));
            byte[] libJar = CreateZip(("com/bar/B.class", null));

            byte[] aar = CreateZip(
                ("classes.jar", classesJar),
                ("libs/extra.jar", libJar),
                ("AndroidManifest.xml", null),
                ("res/values/values.xml", null));

            List<string> classes = ArchiveClassReader.ReadClassEntryPaths(new MemoryStream(aar), isAar: true);

            Assert.That(classes, Is.EquivalentTo(new[] { "com/foo/A.class", "com/bar/B.class" }));
        }

        private static byte[] CreateZip(params (string name, byte[] content)[] entries)
        {
            using MemoryStream stream = new MemoryStream();

            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach ((string name, byte[] content) in entries)
                {
                    ZipArchiveEntry entry = archive.CreateEntry(name);

                    if (content == null)
                    {
                        continue;
                    }

                    using Stream entryStream = entry.Open();
                    entryStream.Write(content, 0, content.Length);
                }
            }

            return stream.ToArray();
        }
    }
}
