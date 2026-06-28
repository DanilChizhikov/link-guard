using System;
using System.Collections.Generic;
using System.Linq;

namespace DTech.LinkGuard.Editor
{
    public sealed class LinkXmlPatchReport
    {
        public string OutputPath { get; }
        public string Xml { get; }
        public bool Written { get; }
        public bool Success { get; }
        public int DuplicatesCollapsedCount { get; }
        public IReadOnlyList<LinkXmlPatchProviderReport> Providers { get; }

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
