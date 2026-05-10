using System.IO;
using UnityEditor;
using UnityEngine;

namespace DTech.LinkGuard.Editor
{
    public static class LinkXmlWriter
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

            string directory = Path.GetDirectoryName(normalized);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(normalized, xml);

            if (normalized.StartsWith("Assets/"))
            {
                AssetDatabase.ImportAsset(normalized, ImportAssetOptions.ForceUpdate);
            }
            else
            {
                AssetDatabase.Refresh();
            }

            Debug.Log($"[LinkXmlGenerator] link.xml written to {normalized}");
            return true;
        }
    }
}
