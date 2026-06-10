using System;
using System.Linq;

namespace DTech.LinkGuard.Editor
{
    internal static class TypeNameResolver
    {
        public static string GetLinkerTypeName(Type type)
        {
            if (type == typeof(void))
            {
                return "System.Void";
            }

            if (type.IsByRef)
            {
                return $"{GetLinkerTypeName(type.GetElementType())}&";
            }

            if (type.IsPointer)
            {
                return $"{GetLinkerTypeName(type.GetElementType())}*";
            }

            if (type.IsArray)
            {
                return $"{GetLinkerTypeName(type.GetElementType())}{GetArrayRankSuffix(type.GetArrayRank())}";
            }

            if (type.IsGenericParameter)
            {
                return type.Name;
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                Type definition = type.GetGenericTypeDefinition();
                string definitionName = GetNonGenericLinkerTypeName(definition);
                string arguments = string.Join(",", type.GetGenericArguments().Select(GetLinkerTypeName));

                return $"{definitionName}<{arguments}>";
            }

            return GetNonGenericLinkerTypeName(type);
        }

        private static string GetNonGenericLinkerTypeName(Type type)
        {
            string fullname = type.FullName ?? type.Name;

            int genericArgumentStart = fullname.IndexOf("[[", StringComparison.Ordinal);
            if (genericArgumentStart >= 0)
            {
                fullname = fullname.Substring(0, genericArgumentStart);
            }

            return fullname.Replace('+', '/');
        }

        private static string GetArrayRankSuffix(int rank)
        {
            if (rank <= 1)
            {
                return "[]";
            }

            return $"[{new string(',', rank - 1)}]";
        }
    }
}
