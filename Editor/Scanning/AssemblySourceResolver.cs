using UnityEditor.Compilation;
using CompilationAssembly = UnityEditor.Compilation.Assembly;

namespace DTech.LinkGuard.Editor
{
    internal sealed class AssemblySourceResolver
    {
        private readonly KnownSdks _sdks;

        public AssemblySourceResolver(KnownSdks sdks)
        {
            _sdks = sdks;
        }

        public AssemblySource Resolve(CompilationAssembly assembly)
        {
            if (_sdks.IsSdk(assembly.name))
            {
                return AssemblySource.Sdk;
            }

            string firstSource = assembly.sourceFiles != null && assembly.sourceFiles.Length > 0
                ? assembly.sourceFiles[0].Replace('\\', '/')
                : string.Empty;

            if (firstSource.StartsWith("Packages/", System.StringComparison.OrdinalIgnoreCase))
            {
                return AssemblySource.UpmPackage;
            }

            if (firstSource.StartsWith("Assets/Plugins/", System.StringComparison.OrdinalIgnoreCase))
            {
                return AssemblySource.Plugin;
            }

            if (assembly.name.StartsWith("Unity.", System.StringComparison.Ordinal)
                || assembly.name.StartsWith("UnityEngine.", System.StringComparison.Ordinal)
                || assembly.name.StartsWith("UnityEditor.", System.StringComparison.Ordinal))
            {
                return AssemblySource.Unity;
            }

            return AssemblySource.Project;
        }
    }
}
