using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor
{
    internal sealed class LinkXmlSyncOutcome
    {
        public bool Success { get; }
        public string FailureReason { get; }
        public string Xml { get; }
        public bool Changed { get; }
        public IReadOnlyList<string> AddedAssemblies { get; }
        public IReadOnlyList<LinkXmlSyncEntryGroup> AddedNamespaces { get; }
        public IReadOnlyList<LinkXmlSyncEntryGroup> AddedTypes { get; }
        public IReadOnlyList<string> UntrackedAssemblies { get; }

        private LinkXmlSyncOutcome(
            bool success,
            string failureReason,
            string xml,
            bool changed,
            IReadOnlyList<string> addedAssemblies,
            IReadOnlyList<LinkXmlSyncEntryGroup> addedNamespaces,
            IReadOnlyList<LinkXmlSyncEntryGroup> addedTypes,
            IReadOnlyList<string> untrackedAssemblies)
        {
            Success = success;
            FailureReason = failureReason ?? string.Empty;
            Xml = xml ?? string.Empty;
            Changed = changed;
            AddedAssemblies = addedAssemblies ?? Array.Empty<string>();
            AddedNamespaces = addedNamespaces ?? Array.Empty<LinkXmlSyncEntryGroup>();
            AddedTypes = addedTypes ?? Array.Empty<LinkXmlSyncEntryGroup>();
            UntrackedAssemblies = untrackedAssemblies ?? Array.Empty<string>();
        }

        public static LinkXmlSyncOutcome Failed(string reason, string xml)
        {
            return new LinkXmlSyncOutcome(false, reason, xml, false, null, null, null, null);
        }

        public static LinkXmlSyncOutcome Completed(
            string xml,
            bool changed,
            IReadOnlyList<string> addedAssemblies,
            IReadOnlyList<LinkXmlSyncEntryGroup> addedNamespaces,
            IReadOnlyList<LinkXmlSyncEntryGroup> addedTypes,
            IReadOnlyList<string> untrackedAssemblies)
        {
            return new LinkXmlSyncOutcome(
                true,
                string.Empty,
                xml,
                changed,
                addedAssemblies,
                addedNamespaces,
                addedTypes,
                untrackedAssemblies);
        }
    }
}
