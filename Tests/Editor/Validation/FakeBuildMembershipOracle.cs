using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor.Tests
{
    internal sealed class FakeBuildMembershipOracle : IBuildMembershipOracle
    {
        private readonly Dictionary<string, BuildPresence> _assemblies =
            new Dictionary<string, BuildPresence>(StringComparer.Ordinal);
        private readonly Dictionary<string, BuildPresence> _types =
            new Dictionary<string, BuildPresence>(StringComparer.Ordinal);

        public List<string> AssemblyQueries { get; } = new List<string>();
        public List<(string assembly, string type)> TypeQueries { get; } = new List<(string, string)>();

        public BuildPresence DefaultAssembly { get; set; } = BuildPresence.Present;
        public BuildPresence DefaultType { get; set; } = BuildPresence.Present;

        public BuildPresence ResolveAssembly(string assemblyName)
        {
            AssemblyQueries.Add(assemblyName);

            return _assemblies.TryGetValue(assemblyName, out BuildPresence presence)
                ? presence
                : DefaultAssembly;
        }

        public BuildPresence ResolveType(string assemblyName, string linkerTypeFullname)
        {
            TypeQueries.Add((assemblyName, linkerTypeFullname));

            return _types.TryGetValue(TypeKey(assemblyName, linkerTypeFullname), out BuildPresence presence)
                ? presence
                : DefaultType;
        }

        public FakeBuildMembershipOracle Assembly(string name, BuildPresence presence)
        {
            _assemblies[name] = presence;
            return this;
        }

        public FakeBuildMembershipOracle Type(string assemblyName, string linkerTypeFullname, BuildPresence presence)
        {
            _types[TypeKey(assemblyName, linkerTypeFullname)] = presence;
            return this;
        }

        private static string TypeKey(string assemblyName, string linkerTypeFullname)
        {
            return assemblyName + "|" + linkerTypeFullname;
        }
    }
}
