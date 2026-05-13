using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor
{
    public sealed class LinkXmlProviderResult
    {
        public string Xml { get; }
        public string Report { get; }
        public IReadOnlyList<string> Warnings { get; }
        public bool Success { get; }
        public bool HasContent => !string.IsNullOrWhiteSpace(Xml);

        public LinkXmlProviderResult(string xml, string report, IReadOnlyList<string> warnings, bool success)
        {
            Xml = xml ?? string.Empty;
            Report = report ?? string.Empty;
            Warnings = warnings ?? Array.Empty<string>();
            Success = success;
        }

        public static LinkXmlProviderResult Empty(string report = "Nothing to merge.")
        {
            return new LinkXmlProviderResult(string.Empty, report, Array.Empty<string>(), true);
        }

        public static LinkXmlProviderResult Failure(string report, IReadOnlyList<string> warnings = null)
        {
            return new LinkXmlProviderResult(string.Empty, report, warnings, false);
        }
    }
}
