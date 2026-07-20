using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor
{
    /// <summary>
    /// Result of a single <see cref="ILinkXmlMergeProvider"/> within a
    /// <see cref="LinkXmlPatchReport"/>.
    /// </summary>
    public sealed class LinkXmlPatchProviderReport
    {
        /// <summary>Identifier of the provider this report describes.</summary>
        public string ProviderId { get; }

        /// <summary>Whether the provider ran without a fatal error.</summary>
        public bool Success { get; }

        /// <summary>Whether the provider contributed content to the merge.</summary>
        public bool ContributedContent { get; }

        /// <summary>Human-readable report text produced by the provider.</summary>
        public string Report { get; }

        /// <summary>Non-fatal warnings raised by the provider.</summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>
        /// Creates a per-provider report.
        /// </summary>
        /// <param name="providerId">Identifier of the provider.</param>
        /// <param name="success">Whether the provider succeeded.</param>
        /// <param name="contributedContent">Whether the provider contributed content.</param>
        /// <param name="report">Report text produced by the provider.</param>
        /// <param name="warnings">Non-fatal warnings, or <c>null</c> for none.</param>
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

        /// <summary>Returns a one-line summary of the provider's outcome.</summary>
        /// <returns>A human-readable summary string.</returns>
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
