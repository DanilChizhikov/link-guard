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
            string firstSource = assembly.sourceFiles != null && assembly.sourceFiles.Length > 0
                ? assembly.sourceFiles[0]
                : string.Empty;

            return Resolve(assembly.name, firstSource);
        }

        /// <summary>
        /// Resolves the source group from an assembly name and a representative source path (e.g. the
        /// first source file, or the .asmdef path for assemblies that are not currently compiled).
        /// </summary>
        public AssemblySource Resolve(string assemblyName, string firstSourcePath)
        {
            if (_sdks.IsSdk(assemblyName))
            {
                return AssemblySource.Sdk;
            }

            string firstSource = string.IsNullOrEmpty(firstSourcePath)
                ? string.Empty
                : firstSourcePath.Replace('\\', '/');

            if (firstSource.StartsWith("Packages/", System.StringComparison.OrdinalIgnoreCase))
            {
                return AssemblySource.UpmPackage;
            }

            if (firstSource.StartsWith("Assets/Plugins/", System.StringComparison.OrdinalIgnoreCase))
            {
                return AssemblySource.Plugin;
            }

            if (assemblyName != null
                && (assemblyName.StartsWith("Unity.", System.StringComparison.Ordinal)
                    || assemblyName.StartsWith("UnityEngine.", System.StringComparison.Ordinal)
                    || assemblyName.StartsWith("UnityEditor.", System.StringComparison.Ordinal)))
            {
                return AssemblySource.Unity;
            }

            return AssemblySource.Project;
        }
    }
}
