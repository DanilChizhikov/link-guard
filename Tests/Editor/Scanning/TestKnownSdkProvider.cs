using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DTech.LinkGuard.Editor.Tests
{
    /// <summary>
    /// Need for KnownSdksTests.IsSdk_CustomProvider_IsDiscoveredViaTypeCache
    /// </summary>
    internal sealed class TestKnownSdkProvider : IKnownSdkProvider
    {
        public IEnumerable<Regex> GetSdkPatterns()
        {
            yield return new Regex(@"^Test_LinkGuard_Marker_\d+$");
        }
    }
}