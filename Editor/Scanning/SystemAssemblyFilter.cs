using System;

namespace DTech.LinkGuard.Editor
{
    internal static class SystemAssemblyFilter
    {
        private static readonly string[] _excludedPrefixes =
        {
            "System",
            "mscorlib",
            "netstandard",
            "Mono.",
            "Microsoft.",
            "nunit",
            "NUnit",
            "JetBrains.",
            "ExCSS.",
            "WindowsBase",
            "PresentationCore",
            "PresentationFramework"
        };

        private static readonly string[] _excludedSuffixes =
        {
            ".Tests",
            ".Test",
            ".Editor.Tests",
            ".EditorTests"
        };

        private static readonly string[] _excludedNames =
        {
            "Bee.BeeDriver",
            "ExCSS.Unity",
            "PsdPlugin",
            "ReportGeneratorMerged",
            "Unity.SourceGenerators"
        };

        public static bool ShouldExclude(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                return true;
            }

            foreach (string prefix in _excludedPrefixes)
            {
                if (assemblyName.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            foreach (string suffix in _excludedSuffixes)
            {
                if (assemblyName.EndsWith(suffix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            foreach (string exact in _excludedNames)
            {
                if (string.Equals(assemblyName, exact, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
