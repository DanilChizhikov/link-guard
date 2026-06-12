using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor
{
    public sealed class LinkXmlPatchProviderReport
    {
        public string ProviderId { get; }
        public bool Success { get; }
        public bool ContributedContent { get; }
        public string Report { get; }
        public IReadOnlyList<string> Warnings { get; }

        public LinkXmlPatchProviderReport(
            string providerId,
            bool success,
            bool contributedContent,
            string report,
            IReadOnlyList<string> warnings)
        {
            ProviderId = providerId ?? string.Empty;
            Success = success;
            ContributedContent = contributedContent;
            Report = report ?? string.Empty;
            Warnings = warnings ?? Array.Empty<string>();
        }

        public override string ToString()
        {
            if (!Success)
            {
                return $"[{ProviderId}] FAILED: {Report}";
            }

            return ContributedContent
                ? $"[{ProviderId}] ok, merged"
                : $"[{ProviderId}] ok, no content";
        }
    }
}
