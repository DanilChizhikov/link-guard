using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build;
using UnityEngine;

namespace DTech.LinkGuard.Editor
{
    /// <summary>
    /// Runs every discovered <see cref="ILinkXmlMergeProvider"/>, merges their output,
    /// and writes the combined link.xml to <c>Assets/link.xml</c>. Intended as a
    /// build-time entry point.
    /// </summary>
    public static class LinkXmlPatcher
    {
        /// <summary>
        /// Runs all discovered merge providers and writes the merged link.xml to the default path.
        /// </summary>
        /// <param name="throwOnError">
        /// When <c>true</c>, throws <see cref="UnityEditor.Build.BuildFailedException"/> if any
        /// provider fails; when <c>false</c>, failures are captured in the returned report.
        /// </param>
        /// <returns>A report of the written file and each provider's outcome.</returns>
        public static LinkXmlPatchReport Patch(bool throwOnError = false)
        {
            return Patch(LinkXmlMergeProviderRegistry.Discover(), LinkXmlWriter.DefaultPath, throwOnError);
        }

        internal static LinkXmlPatchReport Patch(
            IReadOnlyList<ILinkXmlMergeProvider> providers,
            string outputPath,
            bool throwOnError)
        {
            List<ProviderRun> runs = new List<ProviderRun>(providers.Count);
            List<LinkXmlMergeInput> contents = new List<LinkXmlMergeInput>(providers.Count);

            foreach (ILinkXmlMergeProvider provider in providers)
            {
                ProviderRun run = RunProvider(provider);
                runs.Add(run);

                if (run.Success && run.ContributedContent)
                {
                    contents.Add(new LinkXmlMergeInput(run.Id, run.Xml));
                }
            }

            LinkXmlMergeResult merge = contents.Count > 0 ? LinkXmlMerger.Merge(contents) : null;

            if (merge != null)
            {
                DowngradeSkippedProviders(runs, merge.SkippedFiles);
            }

            List<string> failedIds = runs
                .Where(r => !r.Success)
                .Select(r => r.Id)
                .ToList();

            if (throwOnError && failedIds.Count > 0)
            {
                throw new BuildFailedException(
                    $"[LinkXmlGenerator] {failedIds.Count} link.xml merge provider(s) failed: "
                    + $"{string.Join(", ", failedIds)}. See Console for details.");
            }

            bool hasMergedContent = merge != null && merge.FilesMerged > 0;

            if (hasMergedContent)
            {
                LinkXmlWriter.Write(merge.Xml, outputPath);
            }
            else
            {
                Debug.Log("[LinkXmlGenerator] No merge provider produced link.xml content; nothing written.");
            }

            LinkXmlPatchReport report = new LinkXmlPatchReport(
                outputPath,
                hasMergedContent ? merge.Xml : string.Empty,
                hasMergedContent,
                failedIds.Count == 0,
                merge?.DuplicatesCollapsed ?? 0,
                runs.Select(r => r.ToReport()).ToList());

            Debug.Log($"[LinkXmlGenerator] {report}");
            return report;
        }

        private static ProviderRun RunProvider(ILinkXmlMergeProvider provider)
        {
            LinkXmlProviderResult result;

            try
            {
                result = provider.Provide();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LinkXmlGenerator] Merge provider '{provider.Id}' threw: {ex}");
                return ProviderRun.Failed(provider.Id, ex.Message);
            }

            if (result == null)
            {
                Debug.LogError($"[LinkXmlGenerator] Merge provider '{provider.Id}' returned no result.");
                return ProviderRun.Failed(provider.Id, "Provider returned no result.");
            }

            List<string> warnings = new List<string>(result.Warnings);

            foreach (string warning in warnings)
            {
                Debug.LogWarning($"[LinkXmlGenerator] [{provider.Id}] {warning}");
            }

            if (!result.Success)
            {
                Debug.LogError($"[LinkXmlGenerator] [{provider.Id}] {result.Report}");
                return ProviderRun.Failed(provider.Id, result.Report, warnings);
            }

            return new ProviderRun(provider.Id, true, result.HasContent, result.Report, result.Xml, warnings);
        }

        private static void DowngradeSkippedProviders(
            List<ProviderRun> runs,
            IReadOnlyList<LinkXmlMergeSkippedFile> skippedFiles)
        {
            foreach (LinkXmlMergeSkippedFile skipped in skippedFiles)
            {
                ProviderRun run = runs.FirstOrDefault(r =>
                    string.Equals(r.Id, skipped.Path, StringComparison.Ordinal));

                if (run == null)
                {
                    continue;
                }

                run.MarkSkippedByMerger($"Provider XML was skipped by the merger: {skipped.Reason}");
                Debug.LogError($"[LinkXmlGenerator] [{run.Id}] {run.Report}");
            }
        }

        private sealed class ProviderRun
        {
            public string Id { get; }
            public bool Success { get; private set; }
            public bool ContributedContent { get; private set; }
            public string Report { get; private set; }
            public string Xml { get; }
            public List<string> Warnings { get; }

            public ProviderRun(
                string id,
                bool success,
                bool contributedContent,
                string report,
                string xml,
                List<string> warnings)
            {
                Id = id ?? string.Empty;
                Success = success;
                ContributedContent = contributedContent;
                Report = report ?? string.Empty;
                Xml = xml ?? string.Empty;
                Warnings = warnings ?? new List<string>();
            }

            public static ProviderRun Failed(string id, string report, List<string> warnings = null)
            {
                return new ProviderRun(id, false, false, report, string.Empty, warnings);
            }

            public void MarkSkippedByMerger(string reason)
            {
                Success = false;
                ContributedContent = false;
                Report = reason;
            }

            public LinkXmlPatchProviderReport ToReport()
            {
                return new LinkXmlPatchProviderReport(Id, Success, ContributedContent, Report, Warnings);
            }
        }
    }
}
