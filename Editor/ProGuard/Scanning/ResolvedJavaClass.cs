namespace DTech.LinkGuard.Editor.ProGuard
{
	internal readonly struct ResolvedJavaClass
	{
		public readonly string Fullname;
		public readonly string Package;
		public readonly string SimpleName;
		public readonly bool IsInner;

		public ResolvedJavaClass(string fullname, string package, string simpleName, bool isInner)
		{
			Fullname = fullname;
			Package = package;
			SimpleName = simpleName;
			IsInner = isInner;
		}
	}
}