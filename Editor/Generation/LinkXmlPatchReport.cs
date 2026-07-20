using System;
using System.Collections.Generic;
using System.Linq;

namespace DTech.LinkGuard.Editor
{
    /// <summary>
    /// Outcome of a <see cref="LinkXmlPatcher.Patch(bool)"/> run: the written file,
    /// its merged content, and the result of each contributing provider.
    /// </summary>
    public sealed class LinkXmlPatchReport
    {
        /// <summary>Path the merged link.xml was (or would be) written to.</summary>
        public string OutputPath { get; }

        /// <summary>The merged link.xml content; empty when nothing was written.</summary>
        public string Xml { get; }

        /// <summary>Whether the merged file was written to disk.</summary>
        public bool Written { get; }

        /// <summary>Whether every provider succeeded.</summary>
        public bool Success { get; }

        /// <summary>Number of duplicate entries collapsed while merging.</summary>
        public int DuplicatesCollapsedCount { get; }

        /// <summary>Per-provider results that made up this run.</summary>
        public IReadOnlyList<LinkXmlPatchProviderReport> Providers { get; }

        /// <summary>
        /// Creates a patch report.
        /// </summary>
        /// <param name="outputPath">Path the merged link.xml targets.</param>
        /// <param name="xml">The merged link.xml content.</param>
        /// <param name="written">Whether the file was written.</param>
        /// <param name="success">Whether every provider succeeded.</param>
        /// <param name="duplicatesCollapsedCount">Number of duplicate entries collapsed.</param>
        /// <param name="providers">Per-provider results.</param>
        public LinkXmlPatchReport(
            string outputPath,
            string xml,
            bool written,
            bool success,
            int duplicatesCollapsedCount,
            IReadOnlyList<LinkXmlPatchProviderReport> providers)
        {
            OutputPath = outputPath ?? string.Empty;
            Xml = xml ?? string.Empty;
            Written = written;
            Success = success;
            DuplicatesCollapsedCount = duplicatesCollapsedCount;
            Providers = providers ?? Array.Empty<LinkXmlPatchProviderReport>();
        }

        /// <summary>Returns a one-line summary of the patch outcome.</summary>
        /// <returns>A human-readable summary string.</returns>
        public override string ToString()
        {
            int merged = Providers.Count(p => p.ContributedContent);
            int failed = Providers.Count(p => !p.Success);

            return $"LinkXmlPatchReport[Path={OutputPath}, Written={Written}, "
                + $"Providers={merged}/{Providers.Count} merged, "
                + $"Duplicates={DuplicatesCollapsedCount}, Failed={failed}]";
        }
    }
}
