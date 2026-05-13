#if LINKGUARD_ZENJECT_ENABLED
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEditor.Compilation;
using CompilationAssembly = UnityEditor.Compilation.Assembly;

namespace DTech.LinkGuard.Editor.Zenject
{
    internal static class ZenjectIlBindingExtractor
    {
        private const string InstallBindingsMethod = "InstallBindings";

        private static readonly HashSet<string> _bindMethodNames = new(StringComparer.Ordinal)
        {
            "Bind",
            "BindInterfacesTo",
            "BindInterfacesAndSelfTo",
            "BindFactory",
            "BindIFactory",
            "BindFactoryContract",
            "BindFactoryCustomInterface",
            "BindMemoryPool",
            "BindPool",
            "BindPoolableMemoryPool",
            "BindMemoryPoolCustomInterface",
            "To",
            "FromInstance",
            "FromComponentInNewPrefab",
            "FromComponentOn",
            "FromComponentOnRoot",
            "FromComponentOnNewGameObject",
            "FromComponentInNewPrefabResource",
            "FromSubContainerResolve",
        };

        private static readonly HashSet<string> _installMethodNames = new(StringComparer.Ordinal)
        {
            "Install",
            "InstallSubContainer",
            "InstallScriptableObjectInstaller",
            "InstallSubContainerInternal",
        };

        public static ZenjectExtractionResult Extract(IReadOnlyCollection<Type> installerTypes, List<string> warnings)
        {
            HashSet<TypeIdentifier> boundTypes = new HashSet<TypeIdentifier>();
            HashSet<Type> installEdges = new HashSet<Type>();

            if (installerTypes == null || installerTypes.Count == 0)
            {
                return new ZenjectExtractionResult(boundTypes, installEdges);
            }

            using AssemblyContext context = AssemblyContext.Create();

            foreach (Type installerType in installerTypes)
            {
                if (installerType == null)
                {
                    continue;
                }

                TypeDefinition typeDef = context.ResolveType(installerType, warnings);
                if (typeDef == null)
                {
                    continue;
                }

                MethodDefinition installBindings = FindInstallBindings(typeDef);
                if (installBindings == null)
                {
                    warnings.Add($"Installer '{installerType.FullName}' has no InstallBindings() method to analyze.");
                    continue;
                }

                HashSet<MethodDefinition> visited = new HashSet<MethodDefinition>();
                WalkMethod(installBindings, typeDef, visited, boundTypes, installEdges, warnings, installerType);
            }

            return new ZenjectExtractionResult(boundTypes, installEdges);
        }

        private static MethodDefinition FindInstallBindings(TypeDefinition typeDef)
        {
            TypeDefinition cursor = typeDef;
            while (cursor != null)
            {
                MethodDefinition method = cursor.Methods.FirstOrDefault(m =>
                    string.Equals(m.Name, InstallBindingsMethod, StringComparison.Ordinal)
                    && m.HasBody);

                if (method != null)
                {
                    return method;
                }

                cursor = cursor.BaseType?.Resolve();
            }

            return null;
        }

        private static void WalkMethod(
            MethodDefinition method,
            TypeDefinition installerType,
            HashSet<MethodDefinition> visited,
            HashSet<TypeIdentifier> boundTypes,
            HashSet<Type> installEdges,
            List<string> warnings,
            Type installerSystemType)
        {
            if (method == null || !visited.Add(method) || !method.HasBody)
            {
                return;
            }

            IList<Instruction> instructions = method.Body.Instructions;

            for (int i = 0; i < instructions.Count; i++)
            {
                Instruction instruction = instructions[i];

                if (instruction.OpCode != OpCodes.Call
                    && instruction.OpCode != OpCodes.Callvirt
                    && instruction.OpCode != OpCodes.Calli)
                {
                    continue;
                }

                if (instruction.Operand is not MethodReference target)
                {
                    continue;
                }

                if (_installMethodNames.Contains(target.Name))
                {
                    foreach (TypeReference arg in EnumerateGenericArgs(target))
                    {
                        TryRecordInstallEdge(arg, installEdges, warnings, installerSystemType);
                    }

                    foreach (TypeReference arg in EnumerateTypeofOperands(instructions, i))
                    {
                        TryRecordInstallEdge(arg, installEdges, warnings, installerSystemType);
                    }

                    continue;
                }

                if (_bindMethodNames.Contains(target.Name))
                {
                    foreach (TypeReference arg in EnumerateGenericArgs(target))
                    {
                        TryRecordBound(arg, boundTypes, installerType, warnings);
                    }

                    foreach (TypeReference arg in EnumerateTypeofOperands(instructions, i))
                    {
                        TryRecordBound(arg, boundTypes, installerType, warnings);
                    }

                    continue;
                }

                if (target.DeclaringType?.FullName == installerType.FullName)
                {
                    MethodDefinition resolved = SafeResolve(target, warnings, installerSystemType);
                    if (resolved != null)
                    {
                        WalkMethod(resolved, installerType, visited, boundTypes, installEdges, warnings, installerSystemType);
                    }
                }
            }
        }

        private static IEnumerable<TypeReference> EnumerateGenericArgs(MethodReference reference)
        {
            if (reference is GenericInstanceMethod generic && generic.HasGenericArguments)
            {
                foreach (TypeReference arg in generic.GenericArguments)
                {
                    yield return arg;
                }
            }
        }

        private static IEnumerable<TypeReference> EnumerateTypeofOperands(
            IList<Instruction> instructions,
            int callIndex)
        {
            for (int j = callIndex - 1; j >= 0 && j >= callIndex - 8; j--)
            {
                Instruction prev = instructions[j];
                if (prev.OpCode == OpCodes.Ldtoken && prev.Operand is TypeReference typeRef)
                {
                    yield return typeRef;
                }

                if (prev.OpCode == OpCodes.Call || prev.OpCode == OpCodes.Callvirt)
                {
                    break;
                }
            }
        }

        private static void TryRecordBound(
            TypeReference reference,
            HashSet<TypeIdentifier> boundTypes,
            TypeDefinition installerType,
            List<string> warnings)
        {
            TypeIdentifier identifier = TypeIdentifier.From(reference);

            if (identifier == null)
            {
                return;
            }

            if (identifier.IsGenericParameter)
            {
                warnings.Add($"Installer '{installerType.FullName}' binds a generic parameter '{reference.FullName}' — skipping.");
                return;
            }

            boundTypes.Add(identifier);
        }

        private static void TryRecordInstallEdge(
            TypeReference reference,
            HashSet<Type> installEdges,
            List<string> warnings,
            Type installerSystemType)
        {
            if (reference == null || reference.IsGenericParameter)
            {
                return;
            }

            Type runtime = TypeReferenceLoader.LoadRuntime(reference);
            if (runtime == null)
            {
                warnings.Add($"Installer '{installerSystemType.FullName}' invokes Install on '{reference.FullName}' but the type could not be resolved at runtime.");
                return;
            }

            installEdges.Add(runtime);
        }

        private static MethodDefinition SafeResolve(MethodReference reference, List<string> warnings, Type installerSystemType)
        {
            try
            {
                return reference.Resolve();
            }
            catch (Exception ex)
            {
                warnings.Add($"Installer '{installerSystemType.FullName}': failed to resolve helper '{reference.FullName}': {ex.Message}");
                return null;
            }
        }

        private sealed class AssemblyContext : IDisposable
        {
            private readonly DefaultAssemblyResolver _resolver = new DefaultAssemblyResolver();
            private readonly Dictionary<string, AssemblyDefinition> _byName = new(StringComparer.Ordinal);

            public static AssemblyContext Create()
            {
                AssemblyContext ctx = new AssemblyContext();

                foreach (CompilationAssembly assembly in CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies))
                {
                    if (string.IsNullOrEmpty(assembly.outputPath))
                    {
                        continue;
                    }

                    string directory = Path.GetDirectoryName(assembly.outputPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        ctx._resolver.AddSearchDirectory(directory);
                    }
                }

                return ctx;
            }

            public TypeDefinition ResolveType(Type runtimeType, List<string> warnings)
            {
                string assemblyName = runtimeType.Assembly.GetName().Name;

                if (!_byName.TryGetValue(assemblyName, out AssemblyDefinition asmDef))
                {
                    string path = TryFindAssemblyPath(runtimeType.Assembly.Location, assemblyName);
                    if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    {
                        warnings.Add($"Mono.Cecil: assembly file not found for '{assemblyName}'.");
                        return null;
                    }

                    try
                    {
                        ReaderParameters parameters = new ReaderParameters
                        {
                            ReadSymbols = false,
                            AssemblyResolver = _resolver,
                            ReadingMode = ReadingMode.Deferred,
                        };
                        asmDef = AssemblyDefinition.ReadAssembly(path, parameters);
                        _byName[assemblyName] = asmDef;
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Mono.Cecil: failed to read '{path}': {ex.Message}");
                        return null;
                    }
                }

                string cecilFullName = (runtimeType.FullName ?? string.Empty).Replace('+', '/');

                foreach (ModuleDefinition module in asmDef.Modules)
                {
                    TypeDefinition td = module.GetType(cecilFullName);
                    if (td != null)
                    {
                        return td;
                    }
                }

                warnings.Add($"Mono.Cecil: type '{cecilFullName}' not found in '{assemblyName}'.");
                return null;
            }

            private static string TryFindAssemblyPath(string runtimeLocation, string assemblyName)
            {
                if (!string.IsNullOrEmpty(runtimeLocation) && File.Exists(runtimeLocation))
                {
                    return runtimeLocation;
                }

                foreach (CompilationAssembly assembly in CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies))
                {
                    if (string.Equals(assembly.name, assemblyName, StringComparison.Ordinal)
                        && !string.IsNullOrEmpty(assembly.outputPath)
                        && File.Exists(assembly.outputPath))
                    {
                        return assembly.outputPath;
                    }
                }

                return null;
            }

            public void Dispose()
            {
                foreach (AssemblyDefinition asm in _byName.Values)
                {
                    try
                    {
                        asm.Dispose();
                    }
                    catch
                    {
                        // ignored
                    }
                }
                _byName.Clear();

                try
                {
                    _resolver.Dispose();
                }
                catch
                {
                    // ignored
                }
            }
        }

        private static class TypeReferenceLoader
        {
            public static Type LoadRuntime(TypeReference reference)
            {
                if (reference == null || reference.IsGenericParameter)
                {
                    return null;
                }

                string cecilName = reference.FullName.Replace('/', '+');
                int genericMark = cecilName.IndexOf('<');
                if (genericMark >= 0)
                {
                    cecilName = cecilName.Substring(0, genericMark);
                }

                string assemblyName = reference.Scope?.Name ?? string.Empty;
                if (assemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    assemblyName = assemblyName.Substring(0, assemblyName.Length - 4);
                }

                foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!string.IsNullOrEmpty(assemblyName)
                        && !string.Equals(asm.GetName().Name, assemblyName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Type t = asm.GetType(cecilName, throwOnError: false);
                    if (t != null)
                    {
                        return t;
                    }
                }

                return null;
            }
        }

    }

    internal sealed class ZenjectExtractionResult
    {
        public IReadOnlyCollection<TypeIdentifier> BoundTypes { get; }
        public IReadOnlyCollection<Type> InstallEdges { get; }

        public ZenjectExtractionResult(
            IReadOnlyCollection<TypeIdentifier> boundTypes,
            IReadOnlyCollection<Type> installEdges)
        {
            BoundTypes = boundTypes ?? Array.Empty<TypeIdentifier>();
            InstallEdges = installEdges ?? Array.Empty<Type>();
        }
    }

    internal sealed class TypeIdentifier : IEquatable<TypeIdentifier>
    {
        public string AssemblyName { get; }
        public string TypeFullname { get; }
        public bool IsGenericParameter { get; }

        private TypeIdentifier(string assemblyName, string typeFullname, bool isGenericParameter)
        {
            AssemblyName = assemblyName ?? string.Empty;
            TypeFullname = typeFullname ?? string.Empty;
            IsGenericParameter = isGenericParameter;
        }

        public static TypeIdentifier From(string assemblyName, string typeFullname)
        {
            if (string.IsNullOrEmpty(assemblyName) || string.IsNullOrEmpty(typeFullname))
            {
                return null;
            }

            return new TypeIdentifier(assemblyName, typeFullname, false);
        }

        public static TypeIdentifier From(TypeReference reference)
        {
            if (reference == null)
            {
                return null;
            }

            if (reference.IsGenericParameter)
            {
                return new TypeIdentifier(string.Empty, reference.FullName, true);
            }

            TypeReference normalized = reference;
            while (normalized is GenericInstanceType git)
            {
                normalized = git.ElementType;
            }

            string assemblyName = normalized.Scope?.Name ?? string.Empty;
            if (assemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                assemblyName = assemblyName.Substring(0, assemblyName.Length - 4);
            }

            string typeFullname = normalized.FullName;
            int genericMark = typeFullname.IndexOf('<');
            if (genericMark >= 0)
            {
                typeFullname = typeFullname.Substring(0, genericMark);
            }

            return new TypeIdentifier(assemblyName, typeFullname, false);
        }

        public bool Equals(TypeIdentifier other)
        {
            if (other is null) return false;
            return string.Equals(AssemblyName, other.AssemblyName, StringComparison.Ordinal)
                && string.Equals(TypeFullname, other.TypeFullname, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as TypeIdentifier);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((AssemblyName?.GetHashCode() ?? 0) * 397) ^ (TypeFullname?.GetHashCode() ?? 0);
            }
        }

        public override string ToString() => $"{TypeFullname}@{AssemblyName}";
    }
}
#endif
