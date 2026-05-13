#if LINKGUARD_ZENJECT_ENABLED
using System;

namespace DTech.LinkGuard.Editor.Zenject
{
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