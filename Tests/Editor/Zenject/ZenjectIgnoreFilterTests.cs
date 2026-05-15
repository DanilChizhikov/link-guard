#if LINKGUARD_ZENJECT_ENABLED
using DTech.LinkGuard;
using NUnit.Framework;

namespace DTech.LinkGuard.Editor.Zenject.Tests
{
    [TestFixture]
    public sealed class ZenjectIgnoreFilterTests
    {
        private class PlainType
        {
        }

        [LinkGuardIgnore]
        private class IgnoredType
        {
        }

        private sealed class DerivedFromIgnored : IgnoredType
        {
        }

        [Test]
        public void IsIgnored_Null_ReturnsFalse()
        {
            Assert.That(ZenjectIgnoreFilter.IsIgnored(null), Is.False);
        }

        [Test]
        public void IsIgnored_TypeWithoutAttribute_ReturnsFalse()
        {
            Assert.That(ZenjectIgnoreFilter.IsIgnored(typeof(PlainType)), Is.False);
        }

        [Test]
        public void IsIgnored_TypeWithAttribute_ReturnsTrue()
        {
            Assert.That(ZenjectIgnoreFilter.IsIgnored(typeof(IgnoredType)), Is.True);
        }

        [Test]
        public void IsIgnored_DerivedType_InheritsAttribute_ReturnsTrue()
        {
            Assert.That(ZenjectIgnoreFilter.IsIgnored(typeof(DerivedFromIgnored)), Is.True);
        }
    }
}
#endif
