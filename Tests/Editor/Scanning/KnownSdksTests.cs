using NUnit.Framework;

namespace DTech.LinkGuard.Editor.Tests
{
    [TestFixture]
    internal sealed class KnownSdksTests
    {
        [TestCase("Firebase")]
        [TestCase("Firebase.Auth")]
        [TestCase("MaxSdk")]
        [TestCase("MaxSdk.Scripts")]
        [TestCase("AppsFlyer")]
        [TestCase("AppMetrica.Core")]
        [TestCase("Google.Play.Common")]
        [TestCase("GoogleMobileAds")]
        [TestCase("FacebookSDK")]
        [TestCase("FacebookCore")]
        [TestCase("Facebook.Unity")]
        [TestCase("UCM")]
        [TestCase("Unity.Usercentrics")]
        [TestCase("Purchasing.Common")]
        [TestCase("UnityEngine.Purchasing")]
        [TestCase("Azur.Core")]
        [TestCase("com.dtech.link-guard")]
        [TestCase("AltTester")]
        [TestCase("StompyRobot.SRDebugger")]
        public void IsSdk_MatchesBuiltinPatterns(string assemblyName)
        {
            KnownSdks sdks = new KnownSdks();
            Assert.That(sdks.IsSdk(assemblyName), Is.True, $"Expected {assemblyName} to match a built-in SDK pattern.");
        }

        [TestCase("UnityEngine")]
        [TestCase("UnityEngine.UI")]
        [TestCase("Assembly-CSharp")]
        [TestCase("MyGame.Core")]
        [TestCase("FirebaseLike")]
        [TestCase("UCM_Extra")]
        public void IsSdk_RejectsNonMatching(string assemblyName)
        {
            KnownSdks sdks = new KnownSdks();
            Assert.That(sdks.IsSdk(assemblyName), Is.False, $"Expected {assemblyName} not to match any SDK pattern.");
        }

        [Test]
        public void IsSdk_NullOrEmpty_ReturnsFalse()
        {
            KnownSdks sdks = new KnownSdks();
            Assert.That(sdks.IsSdk(null), Is.False);
            Assert.That(sdks.IsSdk(string.Empty), Is.False);
        }

        [Test]
        public void IsSdk_CustomProvider_IsDiscoveredViaTypeCache()
        {
            KnownSdks sdks = new KnownSdks();
            Assert.That(sdks.IsSdk("Test_LinkGuard_Marker_42"), Is.True);
            Assert.That(sdks.IsSdk("Test_LinkGuard_Marker_X"), Is.False);
        }
    }
}
