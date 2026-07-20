using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor
{
    /// <summary>
    /// Result returned by an <see cref="ILinkXmlMergeProvider"/>: the produced link.xml,
    /// a human-readable report, warnings, and whether the run succeeded.
    /// </summary>
    public sealed class LinkXmlProviderResult
    {
        /// <summary>The link.xml content produced by the provider; empty when nothing was generated.</summary>
        public string Xml { get; }

        /// <summary>Human-readable report describing what the provider did.</summary>
        public string Report { get; }

        /// <summary>Non-fatal warnings raised while producing the result.</summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>Whether the provider completed without a fatal error.</summary>
        public bool Success { get; }

        /// <summary>Whether <see cref="Xml"/> contains mergeable content.</summary>
        public bool HasContent => !string.IsNullOrWhiteSpace(Xml);

        /// <summary>
        /// Creates a provider result.
        /// </summary>
        /// <param name="xml">The produced link.xml content.</param>
        /// <param name="report">Human-readable report text.</param>
        /// <param name="warnings">Non-fatal warnings, or <c>null</c> for none.</param>
        /// <param name="success">Whether the provider succeeded.</param>
        public LinkXmlProviderResult(string xml, string report, IReadOnlyList<string> warnings, bool success)
        {
            Xml = xml ?? string.Empty;
            Report = report ?? string.Empty;
            Warnings = warnings ?? Array.Empty<string>();
            Success = success;
        }

        /// <summary>
        /// Creates a successful result that produced no content.
        /// </summary>
        /// <param name="report">Report text explaining why nothing was produced.</param>
        /// <returns>An empty, successful result.</returns>
        public static LinkXmlProviderResult Empty(string report = "Nothing to merge.")
        {
            return new LinkXmlProviderResult(string.Empty, report, Array.Empty<string>(), true);
        }

        /// <summary>
        /// Creates a failed result with no content.
        /// </summary>
        /// <param name="report">Report text describing the failure.</param>
        /// <param name="warnings">Optional warnings raised before failing.</param>
        /// <returns>A failed result.</returns>
        public static LinkXmlProviderResult Failure(string report, IReadOnlyList<string> warnings = null)
        {
            return new LinkXmlProviderResult(string.Empty, report, warnings, false);
        }
    }
}
