namespace DTech.LinkGuard.Editor.ProGuard
{
    internal sealed class ProGuardTreeNode
    {
        public ProGuardTreeNodeKind Kind { get; }
        public AndroidArtifactSource Group { get; }
        public AndroidArtifactEntry Artifact { get; }
        public JavaPackageEntry Package { get; }
        public JavaClassEntry Class { get; }

        private ProGuardTreeNode(ProGuardTreeNodeKind kind,
            AndroidArtifactSource group,
            AndroidArtifactEntry artifact,
            JavaPackageEntry package,
            JavaClassEntry javaClass)
        {
            Kind = kind;
            Group = group;
            Artifact = artifact;
            Package = package;
            Class = javaClass;
        }

        public static ProGuardTreeNode ForGroup(AndroidArtifactSource group)
        {
            return new ProGuardTreeNode(ProGuardTreeNodeKind.Group, group, null, null, null);
        }

        public static ProGuardTreeNode ForArtifact(AndroidArtifactEntry artifact)
        {
            return new ProGuardTreeNode(ProGuardTreeNodeKind.Artifact, artifact.Source, artifact, null, null);
        }

        public static ProGuardTreeNode ForPackage(AndroidArtifactEntry artifact, JavaPackageEntry package)
        {
            return new ProGuardTreeNode(ProGuardTreeNodeKind.Package, artifact.Source, artifact, package, null);
        }

        public static ProGuardTreeNode ForClass(AndroidArtifactEntry artifact, JavaPackageEntry package,
            JavaClassEntry javaClass)
        {
            return new ProGuardTreeNode(ProGuardTreeNodeKind.Class, artifact.Source, artifact, package, javaClass);
        }
    }
}
