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

            Write(text, normalized);
            return true;
        }

        public static void Write(string text, string targetPath = DefaultPath)
        {
            if (!ProGuardBuildSettings.EnableCustomProguardFile())
            {
                return;
            }
            
            string normalized = string.IsNullOrEmpty(targetPath) ? DefaultPath : targetPath;
            string directory = Path.GetDirectoryName(normalized);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(normalized, text);

            if (normalized.StartsWith("Assets/"))
            {
                AssetDatabase.ImportAsset(normalized, ImportAssetOptions.ForceUpdate);
            }
            else
            {
                AssetDatabase.Refresh();
            }

            Debug.Log($"[LinkGuard] [proguard] rules written to {normalized}");
        }
    }
}
