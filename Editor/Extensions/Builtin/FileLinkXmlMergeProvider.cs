using System.Collections.Generic;
using System.Text;

namespace DTech.LinkGuard.Editor
{
    internal sealed class FileLinkXmlMergeProvider : ILinkXmlMergeProvider
    {
        public string Id => "file";
        public string ButtonLabel => "Merge link.xml";
        public string Tooltip => "Merge existing link.xml files found in Assets and Packages.";

        public LinkXmlProviderResult Provide()
        {
            IReadOnlyList<string> paths = LinkXmlMergeScanner.FindLinkXmlFiles();

            if (paths.Count == 0)
            {
                return LinkXmlProviderResult.Empty(
                    "No link.xml files were found in Assets or Packages.");
            }

            LinkXmlMergeResult result = LinkXmlMerger.Merge(paths);
            string report = BuildReport(result);
            List<string> warnings = new List<string>(result.SkippedFiles.Count);

            foreach (LinkXmlMergeSkippedFile skipped in result.SkippedFiles)
            {
                warnings.Add($"Skipped {skipped.Path}: {skipped.Reason}");
            }

            if (result.FilesMerged == 0)
            {
                return new LinkXmlProviderResult(string.Empty, report, warnings, true);
            }

            return new LinkXmlProviderResult(result.Xml, report, warnings, true);
        }

        private static string BuildReport(LinkXmlMergeResult result)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Files found: {result.FilesFound}");
            builder.AppendLine($"Files merged: {result.FilesMerged}");
            builder.AppendLine($"Skipped invalid files: {result.SkippedFiles.Count}");
            builder.AppendLine($"Duplicate entries collapsed: {result.DuplicatesCollapsed}");
            builder.AppendLine($"Output pending: press Generate link.xml to write {LinkXmlWriter.DefaultPath}");

            if (result.SkippedFiles.Count == 0)
            {
                return builder.ToString();
            }

            builder.AppendLine();
            builder.AppendLine("Skipped files:");

            foreach (LinkXmlMergeSkippedFile skippedFile in result.SkippedFiles)
            {
                builder.AppendLine($"{skippedFile.Path}: {skippedFile.Reason}");
            }

            return builder.ToString();
        }
    }
}
