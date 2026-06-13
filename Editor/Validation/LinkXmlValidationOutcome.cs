using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor
{
    internal sealed class LinkXmlValidationOutcome
    {
        public bool Success { get; }
        public string FailureReason { get; }
        public string Xml { get; }
        public bool Changed { get; }
        public IReadOnlyList<string> RemovedAssemblies { get; }
        public IReadOnlyList<LinkXmlValidationTypeGroup> RemovedTypes { get; }
        public IReadOnlyList<string> KeptIgnoreIfMissing { get; }
        public IReadOnlyList<LinkXmlValidationSkippedEntry> KeptUnknown { get; }

        private LinkXmlValidationOutcome(
            bool success,
            string failureReason,
            string xml,
            bool changed,
            IReadOnlyList<string> removedAssemblies,
            IReadOnlyList<LinkXmlValidationTypeGroup> removedTypes,
            IReadOnlyList<string> keptIgnoreIfMissing,
            IReadOnlyList<LinkXmlValidationSkippedEntry> keptUnknown)
        {
            Success = success;
            FailureReason = failureReason ?? string.Empty;
            Xml = xml ?? string.Empty;
            Changed = changed;
            RemovedAssemblies = removedAssemblies ?? Array.Empty<string>();
            RemovedTypes = removedTypes ?? Array.Empty<LinkXmlValidationTypeGroup>();
            KeptIgnoreIfMissing = keptIgnoreIfMissing ?? Array.Empty<string>();
            KeptUnknown = keptUnknown ?? Array.Empty<LinkXmlValidationSkippedEntry>();
        }

        public static LinkXmlValidationOutcome Failed(string reason, string xml)
        {
            return new LinkXmlValidationOutcome(false, reason, xml, false, null, null, null, null);
        }

        public static LinkXmlValidationOutcome Completed(
            string xml,
            bool changed,
            IReadOnlyList<string> removedAssemblies,
            IReadOnlyList<LinkXmlValidationTypeGroup> removedTypes,
            IReadOnlyList<string> keptIgnoreIfMissing,
            IReadOnlyList<LinkXmlValidationSkippedEntry> keptUnknown)
        {
            return new LinkXmlValidationOutcome(
                true,
                string.Empty,
                xml,
                changed,
                removedAssemblies,
                removedTypes,
                keptIgnoreIfMissing,
                keptUnknown);
        }
    }
}
