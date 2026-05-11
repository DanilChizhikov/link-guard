using System.Collections.Generic;

namespace DTech.LinkGuard.Editor
{
    internal sealed class LinkXmlMergeResult
    {
        public string Xml { get; }
        public int FilesFound { get; }
        public int FilesMerged { get; }
        public int DuplicatesCollapsed { get; }
        public IReadOnlyList<LinkXmlMergeSkippedFile> SkippedFiles { get; }

        public LinkXmlMergeResult(
            string xml,
            int filesFound,
            int filesMerged,
            int duplicatesCollapsed,
            IReadOnlyList<LinkXmlMergeSkippedFile> skippedFiles)
        {
            Xml = xml;
            FilesFound = filesFound;
            FilesMerged = filesMerged;
            DuplicatesCollapsed = duplicatesCollapsed;
            SkippedFiles = skippedFiles;
        }
    }
}
