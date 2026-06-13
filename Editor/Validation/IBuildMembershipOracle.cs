namespace DTech.LinkGuard.Editor
{
    internal interface IBuildMembershipOracle
    {
        BuildPresence ResolveAssembly(string assemblyName);
        BuildPresence ResolveType(string assemblyName, string linkerTypeFullname);
    }
}
