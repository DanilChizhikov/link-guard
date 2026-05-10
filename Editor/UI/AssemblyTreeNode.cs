namespace DTech.LinkGuard.Editor
{
    internal sealed class AssemblyTreeNode
    {
        public AssemblyTreeNodeKind Kind { get; }
        public AssemblySource Group { get; }
        public AssemblyEntry Assembly { get; }
        public NamespaceEntry Namespace { get; }
        public GlobalTypeEntry GlobalType { get; }

        private AssemblyTreeNode(AssemblyTreeNodeKind kind,
            AssemblySource group,
            AssemblyEntry assembly,
            NamespaceEntry namespaceEntry,
            GlobalTypeEntry globalType)
        {
            Kind = kind;
            Group = group;
            Assembly = assembly;
            Namespace = namespaceEntry;
            GlobalType = globalType;
        }

        public static AssemblyTreeNode ForGroup(AssemblySource group)
        {
            return new AssemblyTreeNode(AssemblyTreeNodeKind.Group, group, null, null, null);
        }

        public static AssemblyTreeNode ForAssembly(AssemblyEntry entry)
        {
            return new AssemblyTreeNode(AssemblyTreeNodeKind.Assembly, entry.Source, entry, null, null);
        }

        public static AssemblyTreeNode ForNamespace(AssemblyEntry entry, NamespaceEntry ns)
        {
            return new AssemblyTreeNode(AssemblyTreeNodeKind.Namespace, entry.Source, entry, ns, null);
        }

        public static AssemblyTreeNode ForGlobalNamespace(AssemblyEntry entry)
        {
            return new AssemblyTreeNode(AssemblyTreeNodeKind.GlobalNamespace, entry.Source, entry, null, null);
        }

        public static AssemblyTreeNode ForGlobalType(AssemblyEntry entry, GlobalTypeEntry type)
        {
            return new AssemblyTreeNode(AssemblyTreeNodeKind.GlobalType, entry.Source, entry, null, type);
        }
    }
}
