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
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                AssemblyDefinitionInfo info = JsonUtility.FromJson<AssemblyDefinitionInfo>(json);

                return string.IsNullOrEmpty(info?.name) ? null : info;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
