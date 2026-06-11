using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace DTech.LinkGuard.Editor
{
    internal static class DisabledAssemblyScanner
    {
        private static readonly Regex _versionPattern = new Regex(
            @"(?<major>\d+)(?:\.(?<minor>\d+))?(?:\.(?<patch>\d+))?",
            RegexOptions.Compiled);

        public static List<AssemblyEntry> Scan(
            ISet<string> compiledNames,
            AssemblySourceResolver sourceResolver,
            Action<string, float> reportProgress = null)
        {
            List<AssemblyEntry> result = new List<AssemblyEntry>();
            List<AsmdefRecord> records = LoadAsmdefRecords();

            if (records.Count == 0)
            {
                return result;
            }

            HashSet<string> baseDefines = GetCurrentDefines();
            IReadOnlyDictionary<string, string> packageVersions = GetInstalledPackageVersions();
            List<string> allDirs = records.Select(r => r.FullDir).Distinct(StringComparer.Ordinal).ToList();

            reportProgress?.Invoke("Scanning disabled assemblies...", 0.96f);

            foreach (AsmdefRecord record in records)
            {
                AssemblyDefinitionInfo info = record.Info;

                if (compiledNames != null && compiledNames.Contains(info.name))
                {
                    continue;
                }

                if (SystemAssemblyFilter.ShouldExclude(info.name) || info.IsEditorOnly || info.IsTestOnly)
                {
                    continue;
                }

                HashSet<string> currentDefines = BuildDefinesForAssembly(
                    baseDefines,
                    info,
                    packageVersions,
                    record.AssetPath);

                List<string> unsatisfied = DefineConstraintEvaluator.GetUnsatisfied(
                    info.defineConstraints,
                    currentDefines);

                if (unsatisfied.Count == 0)
                {
                    continue;
                }

                List<string> csFiles = CollectOwnedCsFiles(record.FullDir, allDirs);
                List<TypeEntry> types = SourceTypeExtractor.CollectFromFiles(csFiles);
                AssemblySource source = sourceResolver.Resolve(info.name, record.AssetPath);

                AssemblyEntry entry = new AssemblyEntry(info.name, source, record.FullDir, types)
                {
                    IsDisabledByDefine = true,
                    RequiredDefines = unsatisfied,
                };

                result.Add(entry);
            }

            return result;
        }

        private static List<AsmdefRecord> LoadAsmdefRecords()
        {
            List<AsmdefRecord> records = new List<AsmdefRecord>();
            string[] guids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                string json;
                string fullPath;

                try
                {
                    fullPath = Path.GetFullPath(assetPath);
                    json = File.ReadAllText(fullPath);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LinkXmlGenerator] Failed to read asmdef at {assetPath}: {ex.Message}");
                    continue;
                }

                if (!AssemblyDefinitionInfo.TryParse(json, out AssemblyDefinitionInfo info, out string reason))
                {
                    Debug.LogWarning($"[LinkXmlGenerator] Failed to parse asmdef at {assetPath}: {reason}");
                    continue;
                }

                string fullDir = NormalizeDirectory(Path.GetDirectoryName(fullPath));

                if (string.IsNullOrEmpty(fullDir))
                {
                    continue;
                }

                records.Add(new AsmdefRecord(info, assetPath, fullDir));
            }

            return records;
        }

        private static List<string> CollectOwnedCsFiles(string asmdefDir, IReadOnlyList<string> allDirs)
        {
            List<string> nestedDirs = allDirs
                .Where(d => !string.Equals(d, asmdefDir, StringComparison.Ordinal)
                    && d.StartsWith(asmdefDir + "/", StringComparison.Ordinal))
                .ToList();

            string[] files;

            try
            {
                files = Directory.GetFiles(asmdefDir, "*.cs", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                return new List<string>();
            }

            List<string> owned = new List<string>(files.Length);

            foreach (string file in files)
            {
                string normalized = file.Replace('\\', '/');
                bool insideNested = nestedDirs.Any(nested =>
                    normalized.StartsWith(nested + "/", StringComparison.Ordinal));

                if (!insideNested)
                {
                    owned.Add(normalized);
                }
            }

            return owned;
        }

        internal static HashSet<string> BuildDefinesForAssembly(
            ISet<string> baseDefines,
            AssemblyDefinitionInfo info,
            IReadOnlyDictionary<string, string> packageVersions,
            string assetPath)
        {
            HashSet<string> defines = baseDefines == null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(baseDefines, StringComparer.Ordinal);

            AddVersionDefines(defines, info, packageVersions, assetPath);

            return defines;
        }

        internal static bool IsVersionExpressionSatisfied(string installedVersion, string expression)
        {
            if (!TryParseVersion(installedVersion, out Version installed))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(expression))
            {
                return true;
            }

            string trimmed = expression.Trim();

            if (trimmed.StartsWith("[", StringComparison.Ordinal) || trimmed.StartsWith("(", StringComparison.Ordinal))
            {
                return IsRangeSatisfied(installed, trimmed);
            }

            return TryParseVersion(trimmed, out Version minimum) && installed.CompareTo(minimum) >= 0;
        }

        private static void AddVersionDefines(
            HashSet<string> defines,
            AssemblyDefinitionInfo info,
            IReadOnlyDictionary<string, string> packageVersions,
            string assetPath)
        {
            if (info?.versionDefines == null || packageVersions == null)
            {
                return;
            }

            foreach (AssemblyVersionDefine versionDefine in info.versionDefines)
            {
                if (versionDefine == null
                    || string.IsNullOrWhiteSpace(versionDefine.name)
                    || string.IsNullOrWhiteSpace(versionDefine.define))
                {
                    continue;
                }

                if (!packageVersions.TryGetValue(versionDefine.name, out string installedVersion))
                {
                    continue;
                }

                if (IsVersionExpressionSatisfied(installedVersion, versionDefine.expression))
                {
                    defines.Add(versionDefine.define.Trim());
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(versionDefine.expression)
                    && !IsVersionExpressionValid(versionDefine.expression))
                {
                    Debug.LogWarning(
                        $"[LinkXmlGenerator] Failed to evaluate versionDefine at {assetPath}: "
                        + $"define '{versionDefine.define}', package '{versionDefine.name}', "
                        + $"expression '{versionDefine.expression}'.");
                }
            }
        }

        private static HashSet<string> GetCurrentDefines()
        {
            HashSet<string> defines = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                BuildTargetGroup group = EditorUserBuildSettings.selectedBuildTargetGroup;
                BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
                NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(group);

                foreach (string define in DefineConstraintEvaluator.ParseDefines(
                    PlayerSettings.GetScriptingDefineSymbols(namedTarget)))
                {
                    defines.Add(define);
                }

                AddBuiltInDefines(defines, group, target, namedTarget);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LinkXmlGenerator] Failed to collect scripting defines: {ex.Message}");
            }

            return defines;
        }

        private static void AddBuiltInDefines(
            HashSet<string> defines,
            BuildTargetGroup group,
            BuildTarget target,
            NamedBuildTarget namedTarget)
        {
            defines.Add("UNITY_EDITOR");
            defines.Add($"UNITY_{group.ToString().ToUpperInvariant()}");
            AddBuildTargetDefine(defines, target);
            AddUnityVersionDefines(defines, Application.unityVersion);
            AddScriptingBackendDefine(defines, namedTarget);
        }

        private static void AddBuildTargetDefine(HashSet<string> defines, BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.Android:
                    defines.Add("UNITY_ANDROID");
                    break;
                case BuildTarget.iOS:
                    defines.Add("UNITY_IOS");
                    break;
                case BuildTarget.WebGL:
                    defines.Add("UNITY_WEBGL");
                    break;
                case BuildTarget.StandaloneOSX:
                    defines.Add("UNITY_STANDALONE_OSX");
                    break;
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    defines.Add("UNITY_STANDALONE_WIN");
                    break;
                case BuildTarget.StandaloneLinux64:
                    defines.Add("UNITY_STANDALONE_LINUX");
                    break;
            }
        }

        private static void AddUnityVersionDefines(HashSet<string> defines, string unityVersion)
        {
            Match match = _versionPattern.Match(unityVersion ?? string.Empty);

            if (!match.Success)
            {
                return;
            }

            string major = match.Groups["major"].Value;
            string minor = GetVersionPart(match, "minor");
            string patch = GetVersionPart(match, "patch");

            defines.Add($"UNITY_{major}");
            defines.Add($"UNITY_{major}_{minor}");
            defines.Add($"UNITY_{major}_{minor}_{patch}");
        }

        private static void AddScriptingBackendDefine(HashSet<string> defines, NamedBuildTarget namedTarget)
        {
            try
            {
                ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(namedTarget);

                if (backend == ScriptingImplementation.IL2CPP)
                {
                    defines.Add("ENABLE_IL2CPP");
                }
                else
                {
                    defines.Add("ENABLE_MONO");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LinkXmlGenerator] Failed to collect scripting backend define: {ex.Message}");
            }
        }

        private static IReadOnlyDictionary<string, string> GetInstalledPackageVersions()
        {
            Dictionary<string, string> versions = new Dictionary<string, string>(StringComparer.Ordinal);

            try
            {
                foreach (PackageInfo package in PackageInfo.GetAllRegisteredPackages())
                {
                    if (package == null || string.IsNullOrEmpty(package.name))
                    {
                        continue;
                    }

                    versions[package.name] = package.version;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LinkXmlGenerator] Failed to collect package version defines: {ex.Message}");
            }

            return versions;
        }

        private static bool IsRangeSatisfied(Version installed, string expression)
        {
            if (!TryParseRange(expression, out Version minimum, out bool includeMinimum, out Version maximum, out bool includeMaximum))
            {
                return false;
            }

            if (minimum != null)
            {
                int minimumComparison = installed.CompareTo(minimum);

                if (minimumComparison < 0 || (minimumComparison == 0 && !includeMinimum))
                {
                    return false;
                }
            }

            if (maximum != null)
            {
                int maximumComparison = installed.CompareTo(maximum);

                if (maximumComparison > 0 || (maximumComparison == 0 && !includeMaximum))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsVersionExpressionValid(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return true;
            }

            string trimmed = expression.Trim();

            if (trimmed.StartsWith("[", StringComparison.Ordinal) || trimmed.StartsWith("(", StringComparison.Ordinal))
            {
                return TryParseRange(trimmed, out _, out _, out _, out _);
            }

            return TryParseVersion(trimmed, out _);
        }

        private static bool TryParseRange(
            string expression,
            out Version minimum,
            out bool includeMinimum,
            out Version maximum,
            out bool includeMaximum)
        {
            minimum = null;
            maximum = null;
            includeMinimum = false;
            includeMaximum = false;

            if (string.IsNullOrWhiteSpace(expression) || expression.Length < 2)
            {
                return false;
            }

            string trimmed = expression.Trim();
            includeMinimum = trimmed[0] == '[';
            includeMaximum = trimmed[trimmed.Length - 1] == ']';

            if (!includeMinimum && trimmed[0] != '(')
            {
                return false;
            }

            if (!includeMaximum && trimmed[trimmed.Length - 1] != ')')
            {
                return false;
            }

            string content = trimmed.Substring(1, trimmed.Length - 2);
            string[] parts = content.Split(',');

            if (parts.Length != 2)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(parts[0]) && !TryParseVersion(parts[0].Trim(), out minimum))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(parts[1]) && !TryParseVersion(parts[1].Trim(), out maximum))
            {
                return false;
            }

            return true;
        }

        private static bool TryParseVersion(string value, out Version version)
        {
            version = null;
            string trimmed = (value ?? string.Empty).Trim();
            Match match = _versionPattern.Match(trimmed);

            if (!match.Success || match.Index != 0)
            {
                return false;
            }

            if (match.Length < trimmed.Length && trimmed[match.Length] != '-' && trimmed[match.Length] != '+')
            {
                return false;
            }

            int major = int.Parse(match.Groups["major"].Value);
            int minor = int.Parse(GetVersionPart(match, "minor"));
            int patch = int.Parse(GetVersionPart(match, "patch"));
            version = new Version(major, minor, patch);

            return true;
        }

        private static string GetVersionPart(Match match, string name)
        {
            return match.Groups[name].Success && !string.IsNullOrEmpty(match.Groups[name].Value)
                ? match.Groups[name].Value
                : "0";
        }

        private static string NormalizeDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            string normalized = path.Replace('\\', '/');

            return normalized.EndsWith("/", StringComparison.Ordinal)
                ? normalized.Substring(0, normalized.Length - 1)
                : normalized;
        }

        private readonly struct AsmdefRecord
        {
            public AssemblyDefinitionInfo Info { get; }
            public string AssetPath { get; }
            public string FullDir { get; }
            
            public AsmdefRecord(AssemblyDefinitionInfo info, string assetPath, string fullDir)
            {
                Info = info;
                AssetPath = assetPath;
                FullDir = fullDir;
            }
        }
    }
}
