using System.Collections.Generic;

namespace DTech.LinkGuard.Editor
{
    internal sealed class AssemblyTreeNode
    {
        public AssemblyTreeNodeKind Kind { get; }
        public AssemblySource Group { get; }
        public AssemblyEntry Assembly { get; }
        public TypeEntry Type { get; }

        public string SegmentLabel { get; }
        public string SegmentPath { get; }
        public NamespaceEntry OwnNamespace { get; }
        public IReadOnlyList<NamespaceEntry> SubtreeNamespaces { get; }

        private AssemblyTreeNode(AssemblyTreeNodeKind kind,
            AssemblySource group,
            AssemblyEntry assembly,
            TypeEntry type,
            string segmentLabel,
            string segmentPath,
            NamespaceEntry ownNamespace,
            IReadOnlyList<NamespaceEntry> subtreeNamespaces)
        {
            Kind = kind;
            Group = group;
            Assembly = assembly;
            Type = type;
            SegmentLabel = segmentLabel;
            SegmentPath = segmentPath;
            OwnNamespace = ownNamespace;
            SubtreeNamespaces = subtreeNamespaces;
        }

        public static AssemblyTreeNode ForGroup(AssemblySource group)
        {
            return new AssemblyTreeNode(AssemblyTreeNodeKind.Group, group, null, null, null, null, null, null);
        }

        public static AssemblyTreeNode ForAssembly(AssemblyEntry entry)
        {
            return new AssemblyTreeNode(AssemblyTreeNodeKind.Assembly, entry.Source, entry, null, null, null, null,
                null);
        }

        public static AssemblyTreeNode ForNamespaceSegment(AssemblyEntry entry,
            string segmentLabel,
            string segmentPath,
            NamespaceEntry ownNamespace,
            IReadOnlyList<NamespaceEntry> subtreeNamespaces)
        {
            return new AssemblyTreeNode(AssemblyTreeNodeKind.Namespace, entry.Source, entry, null,
                segmentLabel, segmentPath, ownNamespace, subtreeNamespaces);
        }

        public static AssemblyTreeNode ForType(AssemblyEntry entry, TypeEntry type)
        {
            return new AssemblyTreeNode(AssemblyTreeNodeKind.Type, entry.Source, entry, type, null, null, null, null);
        }
    }
}
