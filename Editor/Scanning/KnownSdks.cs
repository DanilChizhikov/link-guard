using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;

namespace DTech.LinkGuard.Editor
{
    internal sealed class KnownSdks
    {
        private static readonly Regex[] _backedSdkPatterns =
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

        private readonly List<Regex> _sdkPatterns;

        public KnownSdks()
        {
            _sdkPatterns = new List<Regex>(_backedSdkPatterns);
            Regex[] customPatterns = GetCustomPatterns();
            _sdkPatterns.AddRange(customPatterns);
        }

        public bool IsSdk(string assemblyName)
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

        private static Regex[] GetCustomPatterns()
        {
            var result = new List<Regex>();
            TypeCache.TypeCollection collection = TypeCache.GetTypesDerivedFrom<IKnownSdkProvider>();
            Type[] types = collection.Where(t => !t.IsAbstract && !t.IsInterface).ToArray();
            foreach (Type type in types)
            {
                if (type == null)
                {
                    continue;
                }

                var provider = (IKnownSdkProvider)Activator.CreateInstance(type);
                result.AddRange(provider.GetSdkPatterns());
            }

            return result.ToArray();
        }
    }
}
