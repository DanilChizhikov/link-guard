using System;
using System.Collections.Generic;
using System.Linq;

namespace DTech.LinkGuard.Editor
{
    /// <summary>
    /// Outcome of a <see cref="LinkXmlSync.Sync(bool, bool, bool)"/> run: which entries were added to an
    /// existing link.xml so that newly written code stays covered, and whether the file changed.
    /// Sync never removes or narrows existing entries.
    /// </summary>
    public sealed class LinkXmlSyncReport
    {
        /// <summary>Path of the synchronized link.xml.</summary>
        public string OutputPath { get; }

        /// <summary>The synchronized (and possibly rewritten) link.xml content.</summary>
        public string Xml { get; }

        /// <summary>Whether the sync completed without a fatal error.</summary>
        public bool Success { get; }

        /// <summary>Reason the sync failed; empty on success.</summary>
        public string FailureReason { get; }

        /// <summary>Whether a link.xml file existed to synchronize.</summary>
        public bool FileExisted { get; }

        /// <summary>Whether the sync added anything to the link.xml content.</summary>
        public bool Changed { get; }

        /// <summary>Whether the changed content was written back to disk.</summary>
        public bool Written { get; }

        /// <summary>Assemblies that had no <c>&lt;assembly&gt;</c> entry at all and were added.</summary>
        public IReadOnlyList<string> AddedAssemblies { get; }

        /// <summary>Namespaces added as <c>preserve="all"</c> entries, grouped by owning assembly.</summary>
        public IReadOnlyList<LinkXmlSyncEntryGroup> AddedNamespaces { get; }

        /// <summary>Types added as <c>preserve="all"</c> entries, grouped by owning assembly.</summary>
        public IReadOnlyList<LinkXmlSyncEntryGroup> AddedTypes { get; }

        /// <summary>
        /// Assemblies sync deliberately left alone because their <c>&lt;assembly&gt;</c> element is
        /// explicitly narrowed (a <c>preserve</c> other than <c>all</c> and no child entries).
        /// Reported only; nothing is written for them.
        /// </summary>
        public IReadOnlyList<string> SkippedAssemblies { get; }

        /// <summary>Total number of added namespace entries across all groups.</summary>
        public int AddedNamespaceCount => AddedNamespaces.Sum(g => g.Names.Count);

        /// <summary>Total number of added type entries across all groups.</summary>
        public int AddedTypeCount => AddedTypes.Sum(g => g.Names.Count);

        /// <summary>
        /// Creates a sync report.
        /// </summary>
        /// <param name="outputPath">Path of the synchronized link.xml.</param>
        /// <param name="xml">The synchronized content.</param>
        /// <param name="success">Whether the sync succeeded.</param>
        /// <param name="failureReason">Failure reason, or empty on success.</param>
        /// <param name="fileExisted">Whether a link.xml existed to synchronize.</param>
        /// <param name="changed">Whether anything was added.</param>
        /// <param name="written">Whether the change was written back.</param>
        /// <param name="addedAssemblies">Assemblies that were missing from link.xml and were added.</param>
        /// <param name="addedNamespaces">Namespaces added, grouped by assembly.</param>
        /// <param name="addedTypes">Types added, grouped by assembly.</param>
        /// <param name="skippedAssemblies">Assemblies skipped because they are explicitly narrowed.</param>
        public LinkXmlSyncReport(
            string outputPath,
            string xml,
            bool success,
            string failureReason,
            bool fileExisted,
            bool changed,
            bool written,
            IReadOnlyList<string> addedAssemblies,
            IReadOnlyList<LinkXmlSyncEntryGroup> addedNamespaces,
            IReadOnlyList<LinkXmlSyncEntryGroup> addedTypes,
            IReadOnlyList<string> skippedAssemblies)
        {
            OutputPath = outputPath ?? string.Empty;
            Xml = xml ?? string.Empty;
            Success = success;
            FailureReason = failureReason ?? string.Empty;
            FileExisted = fileExisted;
            Changed = changed;
            Written = written;
            AddedAssemblies = addedAssemblies ?? Array.Empty<string>();
            AddedNamespaces = addedNamespaces ?? Array.Empty<LinkXmlSyncEntryGroup>();
            AddedTypes = addedTypes ?? Array.Empty<LinkXmlSyncEntryGroup>();
            SkippedAssemblies = skippedAssemblies ?? Array.Empty<string>();
        }

        /// <summary>Returns a one-line summary of the sync outcome.</summary>
        /// <returns>A human-readable summary string.</returns>
        public override string ToString()
        {
            return $"LinkXmlSyncReport[Path={OutputPath}, Success={Success}, "
                + $"Changed={Changed}, Written={Written}, "
                + $"AddedAssemblies={AddedAssemblies.Count}, AddedNamespaces={AddedNamespaceCount}, "
                + $"AddedTypes={AddedTypeCount}, Skipped={SkippedAssemblies.Count}]";
        }
    }
}
