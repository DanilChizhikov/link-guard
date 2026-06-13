using System;
using System.Collections.Generic;
using System.Linq;

namespace DTech.LinkGuard.Editor
{
    public sealed class LinkXmlValidationReport
    {
        public string OutputPath { get; }
        public string Xml { get; }
        public bool Success { get; }
        public string FailureReason { get; }
        public bool FileExisted { get; }
        public bool Changed { get; }
        public bool Written { get; }
        public IReadOnlyList<string> RemovedAssemblies { get; }
        public IReadOnlyList<LinkXmlValidationTypeGroup> RemovedTypes { get; }
        public IReadOnlyList<string> KeptIgnoreIfMissing { get; }
        public IReadOnlyList<LinkXmlValidationSkippedEntry> KeptUnknown { get; }

        public int RemovedTypeCount => RemovedTypes.Sum(g => g.TypeNames.Count);

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

        public override string ToString()
        {
            return $"LinkXmlValidationReport[Path={OutputPath}, Success={Success}, "
                + $"Changed={Changed}, Written={Written}, "
                + $"RemovedAssemblies={RemovedAssemblies.Count}, RemovedTypes={RemovedTypeCount}, "
                + $"KeptIgnoreIfMissing={KeptIgnoreIfMissing.Count}, Unknown={KeptUnknown.Count}]";
        }
    }
}
