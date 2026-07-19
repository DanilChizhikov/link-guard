using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DTech.LinkGuard.Editor.ProGuard
{
    internal static class ProGuardWriter
    {
        public const string DefaultPath = ProGuardBuildSettings.ProguardUserFilePath;

        public static bool WriteWithConfirmation(string text, string targetPath = DefaultPath)
        {
            string normalized = string.IsNullOrEmpty(targetPath) ? DefaultPath : targetPath;
            bool exists = File.Exists(normalized);

            string title = exists ? "Overwrite proguard-user.txt?" : "Create proguard-user.txt?";
            string body = exists
                ? $"File '{normalized}' will be overwritten with the generated rules."
                : $"File '{normalized}' will be created.";
            string message = body + "\n\nPlayer Settings 'Custom Proguard File' will be enabled.";

            if (!EditorUtility.DisplayDialog(title, message, "Yes", "Cancel"))
            {
                return false;
            }

            return Write(text, normalized);
        }

        public static bool Write(string text, string targetPath = DefaultPath)
        {
            string normalized = string.IsNullOrEmpty(targetPath) ? DefaultPath : targetPath;
            
            if (string.Equals(normalized, DefaultPath, StringComparison.Ordinal)
                && !ProGuardBuildSettings.EnableCustomProguardFile())
            {
                return false;
            }
            string directory = Path.GetDirectoryName(normalized);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(normalized, text);

            string assetPath = ToAssetPath(normalized);

            if (!string.IsNullOrEmpty(assetPath))
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            Debug.Log($"[LinkGuard] [proguard] rules written to {normalized}");
            return true;
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
