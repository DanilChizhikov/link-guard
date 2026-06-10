using UnityEditor;
using UnityEngine;

namespace DTech.LinkGuard.Editor.ProGuard
{
    internal static class ProGuardBuildSettings
    {
        public const string ProguardUserFilePath = "Assets/Plugins/Android/proguard-user.txt";

        private const string ProjectSettingsAssetPath = "ProjectSettings/ProjectSettings.asset";
        private const string UseCustomProguardFileProperty = "useCustomProguardFile";

        public static bool IsAndroidTarget()
        {
            return EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
        }

        public static bool IsMinifyEnabled()
        {
            return PlayerSettings.Android.minifyRelease || PlayerSettings.Android.minifyDebug;
        }

        public static bool EnableCustomProguardFile()
        {
            foreach (Object settings in AssetDatabase.LoadAllAssetsAtPath(ProjectSettingsAssetPath))
            {
                if (settings == null)
                {
                    continue;
                }

                SerializedObject serialized = new SerializedObject(settings);
                SerializedProperty property = serialized.FindProperty(UseCustomProguardFileProperty);

                if (property == null)
                {
                    continue;
                }

                if (property.boolValue)
                {
                    return false;
                }

                property.boolValue = true;
                serialized.ApplyModifiedProperties();
                return true;
            }

            Debug.LogWarning("[LinkGuard] [proguard] Could not toggle the custom proguard file setting. " +
                             "Enable 'Custom Proguard File' manually in Player Settings > Publishing Settings.");
            return false;
        }
    }
}
