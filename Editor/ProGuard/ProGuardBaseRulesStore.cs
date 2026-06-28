using System;
using System.IO;
using UnityEngine;

namespace DTech.LinkGuard.Editor.ProGuard
{
    internal static class ProGuardBaseRulesStore
    {
        private const string RelativePath = "ProjectSettings/LinkGuardProGuardBaseRules.txt";

        public static string Load()
        {
            try
            {
                string path = FullPath();
                return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LinkGuard] [proguard] Failed to read base rules: {ex.Message}");
                return string.Empty;
            }
        }

        public static void Save(string text)
        {
            try
            {
                string path = FullPath();
                string directory = Path.GetDirectoryName(path);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, text ?? string.Empty);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LinkGuard] [proguard] Failed to save base rules: {ex.Message}");
            }
        }

        private static string FullPath()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), RelativePath);
        }
    }
}
