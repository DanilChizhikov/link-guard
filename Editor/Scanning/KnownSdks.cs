using System.Text.RegularExpressions;

namespace DTech.LinkGuard.Editor
{
    public static class KnownSdks
    {
        private static readonly Regex[] _sdkPatterns =
        {
            new(@"^Firebase(\..+)?$", RegexOptions.Compiled),
            new(@"^MaxSdk(\..+)?$", RegexOptions.Compiled),
            new(@"^AppsFlyer(\..+)?$", RegexOptions.Compiled),
            new(@"^AppMetrica(\..+)?$", RegexOptions.Compiled),
            new(@"^Google\.Play(\..+)?$", RegexOptions.Compiled),
            new(@"^GoogleMobileAds(\..+)?$", RegexOptions.Compiled),
            new(@"^FacebookSDK(\..+)?$", RegexOptions.Compiled),
            new(@"^FacebookCore(\..+)?$", RegexOptions.Compiled),
            new(@"^Facebook\.Unity(\..+)?$", RegexOptions.Compiled),
            new(@"^UCM$", RegexOptions.Compiled),
            new(@"^Unity\.Usercentrics$", RegexOptions.Compiled),
            new(@"^Purchasing(\..+)?$", RegexOptions.Compiled),
            new(@"^UnityEngine\.Purchasing(\..+)?$", RegexOptions.Compiled),
            new(@"^Azur(\..+)?$", RegexOptions.Compiled),
            new(@"^com\.dtech(\..+)?$", RegexOptions.Compiled),
            new(@"^AltTester(\..+)?$", RegexOptions.Compiled),
            new(@"^StompyRobot(\..+)?$", RegexOptions.Compiled)
        };

        public static bool IsSdk(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                return false;
            }

            foreach (Regex pattern in _sdkPatterns)
            {
                if (pattern.IsMatch(assemblyName))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
