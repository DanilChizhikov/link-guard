using System;
using System.Collections.Generic;
using System.Linq;

namespace DTech.LinkGuard.Editor.Tests
{
    internal sealed class FakeProjectTypeSource : IProjectTypeSource
    {
        private readonly Dictionary<string, IReadOnlyList<NamespaceEntry>> _assemblies =
            new Dictionary<string, IReadOnlyList<NamespaceEntry>>(StringComparer.Ordinal);

        private readonly HashSet<string> _projectAssemblies = new HashSet<string>(StringComparer.Ordinal);

        public IReadOnlyList<string> AssemblyNames =>
            _assemblies.Keys.OrderBy(n => n, StringComparer.Ordinal).ToList();

        /// <summary>Registers project code, the default for the sync tests.</summary>
        public FakeProjectTypeSource Assembly(string assemblyName, params string[] typeFullnames)
        {
            _projectAssemblies.Add(assemblyName);
            return ExternalAssembly(assemblyName, typeFullnames);
        }

        public FakeProjectTypeSource ProjectAssembly(string assemblyName, params string[] typeFullnames)
        {
            return Assembly(assemblyName, typeFullnames);
        }

        /// <summary>Registers a plugin, UPM package, SDK, or Unity assembly — anything but project code.</summary>
        public FakeProjectTypeSource ExternalAssembly(string assemblyName, params string[] typeFullnames)
        {
            _assemblies[assemblyName] = BuildNamespaces(typeFullnames);
            return this;
        }

        public bool IsProjectAssembly(string assemblyName)
        {
            return _projectAssemblies.Contains(assemblyName);
        }

        public bool TryGetNamespaces(string assemblyName, out IReadOnlyList<NamespaceEntry> namespaces)
        {
            return _assemblies.TryGetValue(assemblyName, out namespaces);
        }

        private static IReadOnlyList<NamespaceEntry> BuildNamespaces(IReadOnlyList<string> typeFullnames)
        {
            return typeFullnames
                .Select(CreateType)
                .GroupBy(t => t.Namespace, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new NamespaceEntry(g.Key, g))
                .ToList();
        }

        private static TypeEntry CreateType(string typeFullname)
        {
            int slashIndex = typeFullname.IndexOf('/');
            string rootType = slashIndex < 0 ? typeFullname : typeFullname.Substring(0, slashIndex);
            int dotIndex = rootType.LastIndexOf('.');

            string namespaceName = dotIndex < 0 ? string.Empty : rootType.Substring(0, dotIndex);
            string displayName = dotIndex < 0 ? typeFullname : typeFullname.Substring(dotIndex + 1);

            return new TypeEntry(namespaceName, typeFullname, typeFullname, displayName);
        }
    }
}
