using System;
using UnityEngine;

namespace DTech.LinkGuard.Editor
{
    [Serializable]
    internal sealed class AssemblyDefinitionInfo
    {
        public string name;
        public string[] references;
        public string[] includePlatforms;
        public string[] excludePlatforms;
        public string[] defineConstraints;
        public AssemblyVersionDefine[] versionDefines;

        public bool IsEditorOnly =>
            includePlatforms != null
            && includePlatforms.Length == 1
            && string.Equals(includePlatforms[0], "Editor", StringComparison.Ordinal);

        public bool IsTestOnly
        {
            get
            {
                if (defineConstraints == null)
                {
                    return false;
                }

                foreach (string constraint in defineConstraints)
                {
                    if (string.Equals(constraint, "UNITY_INCLUDE_TESTS", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public static AssemblyDefinitionInfo Parse(string json)
        {
            return TryParse(json, out AssemblyDefinitionInfo info, out _) ? info : null;
        }

        public static bool TryParse(string json, out AssemblyDefinitionInfo info, out string reason)
        {
            if (string.IsNullOrEmpty(json))
            {
                info = null;
                reason = "JSON is empty.";
                return false;
            }

            try
            {
                info = JsonUtility.FromJson<AssemblyDefinitionInfo>(json);

                if (string.IsNullOrEmpty(info?.name))
                {
                    reason = "Assembly definition name is missing.";
                    return false;
                }

                reason = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                info = null;
                reason = ex.Message;
                return false;
            }
        }
    }
}
