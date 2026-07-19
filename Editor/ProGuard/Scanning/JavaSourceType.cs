namespace DTech.LinkGuard.Editor.ProGuard
{
	internal readonly struct JavaSourceType
	{
		public readonly string SimpleName;
		public readonly bool HasInnerClasses;

		public JavaSourceType(string simpleName, bool hasInnerClasses)
		{
			SimpleName = simpleName;
			HasInnerClasses = hasInnerClasses;
		}
	}
}