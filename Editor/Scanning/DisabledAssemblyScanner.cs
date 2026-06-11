using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace DTech.LinkGuard.Editor
{
    internal static class DisabledAssemblyScanner
    {
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

            HashSet<string> currentDefines = GetCurrentDefines();
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
                catch (Exception)
                {
                    continue;
                }

                AssemblyDefinitionInfo info = AssemblyDefinitionInfo.Parse(json);

                if (info == null)
                {
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

        private static HashSet<string> GetCurrentDefines()
        {
            try
            {
                BuildTargetGroup group = EditorUserBuildSettings.selectedBuildTargetGroup;
                NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(group);

                return DefineConstraintEvaluator.ParseDefines(
                    PlayerSettings.GetScriptingDefineSymbols(namedTarget));
            }
            catch (Exception)
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }
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
