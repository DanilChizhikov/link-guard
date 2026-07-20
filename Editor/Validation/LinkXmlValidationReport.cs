using System;
using System.Collections.Generic;
using System.Linq;

namespace DTech.LinkGuard.Editor
{
    /// <summary>
    /// Outcome of a <see cref="LinkXmlValidator.Validate(bool, bool)"/> run: what was
    /// removed, kept, or flagged in an existing link.xml, and whether the file changed.
    /// </summary>
    public sealed class LinkXmlValidationReport
    {
        /// <summary>Path of the validated link.xml.</summary>
        public string OutputPath { get; }

        /// <summary>The validated (and possibly rewritten) link.xml content.</summary>
        public string Xml { get; }

        /// <summary>Whether validation completed without a fatal error.</summary>
        public bool Success { get; }

        /// <summary>Reason validation failed; empty on success.</summary>
        public string FailureReason { get; }

        /// <summary>Whether a link.xml file existed to validate.</summary>
        public bool FileExisted { get; }

        /// <summary>Whether validation changed the link.xml content.</summary>
        public bool Changed { get; }

        /// <summary>Whether the changed content was written back to disk.</summary>
        public bool Written { get; }

        /// <summary>Assemblies removed because they are not present in the build.</summary>
        public IReadOnlyList<string> RemovedAssemblies { get; }

        /// <summary>Types removed, grouped by owning assembly.</summary>
        public IReadOnlyList<LinkXmlValidationTypeGroup> RemovedTypes { get; }

        /// <summary>Assemblies kept because they were marked <c>ignoreIfMissing</c>.</summary>
        public IReadOnlyList<string> KeptIgnoreIfMissing { get; }

        /// <summary>Entries kept without verification because build membership was unknown.</summary>
        public IReadOnlyList<LinkXmlValidationSkippedEntry> KeptUnknown { get; }

        /// <summary>Total number of removed types across all groups.</summary>
        public int RemovedTypeCount => RemovedTypes.Sum(g => g.TypeNames.Count);

        /// <summary>
        /// Creates a validation report.
        /// </summary>
        /// <param name="outputPath">Path of the validated link.xml.</param>
        /// <param name="xml">The validated content.</param>
        /// <param name="success">Whether validation succeeded.</param>
        /// <param name="failureReason">Failure reason, or empty on success.</param>
        /// <param name="fileExisted">Whether a link.xml existed to validate.</param>
        /// <param name="changed">Whether the content changed.</param>
        /// <param name="written">Whether the change was written back.</param>
        /// <param name="removedAssemblies">Assemblies removed as not built.</param>
        /// <param name="removedTypes">Types removed, grouped by assembly.</param>
        /// <param name="keptIgnoreIfMissing">Assemblies kept via <c>ignoreIfMissing</c>.</param>
        /// <param name="keptUnknown">Entries kept with unknown build membership.</param>
        public LinkXmlValidationReport(
            string outputPath,
            string xml,
            bool success,
            string failureReason,
            bool fileExisted,
            bool changed,
            bool written,
            IReadOnlyList<string> removedAssemblies,
            IReadOnlyList<LinkXmlValidationTypeGroup> removedTypes,
            IReadOnlyList<string> keptIgnoreIfMissing,
            IReadOnlyList<LinkXmlValidationSkippedEntry> keptUnknown)
        {
            OutputPath = outputPath ?? string.Empty;
            Xml = xml ?? string.Empty;
            Success = success;
            FailureReason = failureReason ?? string.Empty;
            FileExisted = fileExisted;
            Changed = changed;
            Written = written;
            RemovedAssemblies = removedAssemblies ?? Array.Empty<string>();
            RemovedTypes = removedTypes ?? Array.Empty<LinkXmlValidationTypeGroup>();
            KeptIgnoreIfMissing = keptIgnoreIfMissing ?? Array.Empty<string>();
            KeptUnknown = keptUnknown ?? Array.Empty<LinkXmlValidationSkippedEntry>();
        }

        /// <summary>Returns a one-line summary of the validation outcome.</summary>
        /// <returns>A human-readable summary string.</returns>
        public override string ToString()
        {
            return $"LinkXmlValidationReport[Path={OutputPath}, Success={Success}, "
                + $"Changed={Changed}, Written={Written}, "
                + $"RemovedAssemblies={RemovedAssemblies.Count}, RemovedTypes={RemovedTypeCount}, "
                + $"KeptIgnoreIfMissing={KeptIgnoreIfMissing.Count}, Unknown={KeptUnknown.Count}]";
        }
    }
}
