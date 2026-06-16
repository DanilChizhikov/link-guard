namespace DTech.LinkGuard.Editor
{
	internal interface IPrecompiledTypeResolver
	{
		bool IsKnownAssembly(string assemblyName);
		bool TryResolveType(string assemblyName, string linkerTypeFullname, out TypeEntry entry);
	}
}