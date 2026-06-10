using System;

namespace DTech.LinkGuard.Editor.ProGuard
{
    internal readonly struct ResolvedJavaClass
    {
        public readonly string Fullname;
        public readonly string Package;
        public readonly string SimpleName;
        public readonly bool IsInner;

        public ResolvedJavaClass(string fullname, string package, string simpleName, bool isInner)
        {
            Fullname = fullname;
            Package = package;
            SimpleName = simpleName;
            IsInner = isInner;
        }
    }

    internal static class JavaClassNameResolver
    {
        private const string ClassExtension = ".class";

        public static bool TryResolveClassEntry(string entryPath, out ResolvedJavaClass result)
        {
            result = default;

            if (string.IsNullOrEmpty(entryPath))
            {
                return false;
            }

            string normalized = entryPath.Replace('\\', '/');

            if (!normalized.EndsWith(ClassExtension, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string withoutExtension = normalized.Substring(0, normalized.Length - ClassExtension.Length);

            int lastSlash = withoutExtension.LastIndexOf('/');
            string fileName = lastSlash >= 0 ? withoutExtension.Substring(lastSlash + 1) : withoutExtension;

            if (fileName == "module-info" || fileName == "package-info")
            {
                return false;
            }

            string binaryName = withoutExtension.Replace('/', '.');

            int dollar = binaryName.IndexOf('$');
            bool isInner = dollar >= 0;
            string outerBinary = isInner ? binaryName.Substring(0, dollar) : binaryName;

            if (string.IsNullOrEmpty(outerBinary))
            {
                return false;
            }

            int lastDot = outerBinary.LastIndexOf('.');
            string package = lastDot >= 0 ? outerBinary.Substring(0, lastDot) : string.Empty;
            string simpleName = lastDot >= 0 ? outerBinary.Substring(lastDot + 1) : outerBinary;

            if (string.IsNullOrEmpty(simpleName))
            {
                return false;
            }

            result = new ResolvedJavaClass(outerBinary, package, simpleName, isInner);
            return true;
        }
    }
}
