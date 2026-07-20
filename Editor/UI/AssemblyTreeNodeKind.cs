namespace DTech.LinkGuard.Editor
{
	/// <summary>
	/// Identifies the level a node occupies in the Link Guard assembly tree.
	/// </summary>
	public enum AssemblyTreeNodeKind : byte
	{
		/// <summary>A grouping node (for example, a known SDK) that contains assemblies.</summary>
		Group = 0,

		/// <summary>An assembly node.</summary>
		Assembly = 1,

		/// <summary>A namespace node within an assembly.</summary>
		Namespace = 2,

		/// <summary>A single type node.</summary>
		Type = 3,
	}
}
