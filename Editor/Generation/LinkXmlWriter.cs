using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DTech.LinkGuard.Editor
{
    internal static class LinkXmlWriter
    {
        public const string DefaultPath = "Assets/link.xml";

        public static bool WriteWithConfirmation(string xml, string targetPath = DefaultPath)
        {
            string normalized = string.IsNullOrEmpty(targetPath) ? DefaultPath : targetPath;
            bool exists = File.Exists(normalized);

            string title = exists ? "Overwrite link.xml?" : "Create link.xml?";
            string message = exists
                ? $"File '{normalized}' will be overwritten with the generated content."
                : $"File '{normalized}' will be created.";

            if (!EditorUtility.DisplayDialog(title, message, "Yes", "Cancel"))
            {
                return false;
            }

            Write(xml, normalized);
            return true;
        }

        public static void Write(string xml, string targetPath = DefaultPath)
        {
            string normalized = string.IsNullOrEmpty(targetPath) ? DefaultPath : targetPath;
            string directory = Path.GetDirectoryName(normalized);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(normalized, xml);

            string assetPath = ToAssetPath(normalized);

            if (!string.IsNullOrEmpty(assetPath))
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            Debug.Log($"[LinkXmlGenerator] link.xml written to {normalized}");
        }

        private static string ToAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            string normalized = path.Replace('\\', '/');

            if (normalized.StartsWith("Assets/", StringComparison.Ordinal)
                || normalized.StartsWith("Packages/", StringComparison.Ordinal))
            {
                return normalized;
            }

            if (!Path.IsPathRooted(path))
            {
                return string.Empty;
            }

            string fullPath = Path.GetFullPath(path).Replace('\\', '/');
            DirectoryInfo projectDirectory = Directory.GetParent(Application.dataPath);

            if (projectDirectory == null)
            {
                return string.Empty;
            }

            string projectPath = projectDirectory.FullName.Replace('\\', '/');
            string assetsPath = projectPath + "/Assets/";
            string packagesPath = projectPath + "/Packages/";

            if (fullPath.StartsWith(assetsPath, StringComparison.Ordinal))
            {
                return "Assets/" + fullPath.Substring(assetsPath.Length);
            }

            if (fullPath.StartsWith(packagesPath, StringComparison.Ordinal))
            {
                return "Packages/" + fullPath.Substring(packagesPath.Length);
            }

            return string.Empty;
        }
    }
}
