namespace DTech.LinkGuard.Editor
{
    internal sealed class AssemblyTreeNode
    {
        public AssemblyTreeNodeKind Kind { get; }
        public AssemblySource Group { get; }
        public AssemblyEntry Assembly { get; }
        public NamespaceEntry Namespace { get; }
        public TypeEntry Type { get; }
        public MethodEntry Method { get; }

        private AssemblyTreeNode(AssemblyTreeNodeKind kind,
            AssemblySource group,
            AssemblyEntry assembly,
            NamespaceEntry namespaceEntry,
            TypeEntry type,
            MethodEntry method)
        {
            Kind = kind;
            Group = group;
            Assembly = assembly;
            Namespace = namespaceEntry;
            Type = type;
            Method = method;
        }

        public static AssemblyTreeNode ForGroup(AssemblySource group)
        {
            return new AssemblyTreeNode(AssemblyTreeNodeKind.Group, group, null, null, null, null);
        }

        public static AssemblyTreeNode ForAssembly(AssemblyEntry entry)
        {
            return new AssemblyTreeNode(AssemblyTreeNodeKind.Assembly, entry.Source, entry, null, null, null);
        }

        public static AssemblyTreeNode ForNamespace(AssemblyEntry entry, NamespaceEntry ns)
        {
            return new AssemblyTreeNode(AssemblyTreeNodeKind.Namespace, entry.Source, entry, ns, null, null);
        }

        public static AssemblyTreeNode ForType(AssemblyEntry entry, NamespaceEntry ns, TypeEntry type)
        {
            return new AssemblyTreeNode(AssemblyTreeNodeKind.Type, entry.Source, entry, ns, type, null);
        }

        public static AssemblyTreeNode ForMethod(AssemblyEntry entry, NamespaceEntry ns, TypeEntry type, MethodEntry method)
        {
            return new AssemblyTreeNode(AssemblyTreeNodeKind.Method, entry.Source, entry, ns, type, method);
        }
    }
}
