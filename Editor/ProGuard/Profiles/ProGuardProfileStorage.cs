using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DTech.LinkGuard.Editor.ProGuard
{
    internal static class ProGuardProfileStorage
    {
        private const string DefaultDirectory = "ProjectSettings";
        private const string DefaultFileName = "ProGuardProfile.json";

        public static bool Save(IReadOnlyList<AndroidArtifactEntry> entries, out string path)
        {
            string directory = Path.Combine(Directory.GetCurrentDirectory(), DefaultDirectory);

            if (!Directory.Exists(directory))
            {
                directory = Directory.GetCurrentDirectory();
            }

            path = EditorUtility.SaveFilePanel("Save ProGuard Profile", directory, DefaultFileName, "json");

            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            ProGuardProfile profile = ToProfile(entries);
            string json = JsonUtility.ToJson(profile, true);
            File.WriteAllText(path, json);

            Debug.Log($"[LinkGuard] [proguard] Profile saved to {path}");
            return true;
        }

        public static bool Load(List<AndroidArtifactEntry> entries, out string path)
        {
            string directory = Path.Combine(Directory.GetCurrentDirectory(), DefaultDirectory);

            if (!Directory.Exists(directory))
            {
                directory = Directory.GetCurrentDirectory();
            }

            path = EditorUtility.OpenFilePanel("Load ProGuard Profile", directory, "json");

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            string json;

            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Load Profile", $"Failed to read profile: {ex.Message}", "OK");
                return false;
            }

            ProGuardProfile profile;

            try
            {
                profile = JsonUtility.FromJson<ProGuardProfile>(json);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Load Profile", $"Failed to parse profile: {ex.Message}", "OK");
                return false;
            }

            if (profile == null || profile.Selections == null)
            {
                EditorUtility.DisplayDialog("Load Profile", "Profile is empty or invalid.", "OK");
                return false;
            }

            ApplyProfile(profile, entries);
            Debug.Log($"[LinkGuard] [proguard] Profile loaded from {path}");
            return true;
        }

        internal static ProGuardProfile ToProfile(IReadOnlyList<AndroidArtifactEntry> entries)
        {
            ProGuardProfile profile = new ProGuardProfile { Version = 1 };

            foreach (AndroidArtifactEntry entry in entries)
            {
                if (!entry.ProducesEntry)
                {
                    continue;
                }

                profile.Selections.Add(new ProGuardSelection
                {
                    Artifact = entry.Name,
                    Source = entry.Source,
                    OriginPath = entry.OriginPath,
                    KeepAll = entry.IsArtifactSelected,
                    Packages = entry.Packages
                        .Where(p => p.IsSelected && !string.IsNullOrEmpty(p.Fullname))
                        .Select(p => p.Fullname)
                        .ToList(),
                    Classes = entry.Classes
                        .Where(c => c.IsSelected)
                        .Select(c => c.Fullname)
                        .ToList()
                });
            }

            return profile;
        }

        internal static void ApplyProfile(ProGuardProfile profile, List<AndroidArtifactEntry> entries)
        {
            Dictionary<string, AndroidArtifactEntry> byStableKey = entries
                .GroupBy(GetStableKey, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            Dictionary<string, List<AndroidArtifactEntry>> byName = entries
                .GroupBy(e => e.Name, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

            foreach (AndroidArtifactEntry entry in entries)
            {
                entry.SelectAll(false);
            }

            foreach (ProGuardSelection selection in profile.Selections)
            {
                if (!TryGetEntry(selection, byStableKey, byName, out AndroidArtifactEntry entry))
                {
                    continue;
                }

                if (selection.KeepAll)
                {
                    entry.SelectAll(true);
                }

                HashSet<string> wantedPackages = selection.Packages == null
                    ? new HashSet<string>(StringComparer.Ordinal)
                    : new HashSet<string>(selection.Packages, StringComparer.Ordinal);

                foreach (JavaPackageEntry package in entry.Packages)
                {
                    if (wantedPackages.Contains(package.Fullname))
                    {
                        package.IsSelected = true;
                    }
                }

                HashSet<string> wantedClasses = selection.Classes == null
                    ? new HashSet<string>(StringComparer.Ordinal)
                    : new HashSet<string>(selection.Classes, StringComparer.Ordinal);

                foreach (JavaClassEntry javaClass in entry.Classes)
                {
                    if (wantedClasses.Contains(javaClass.Fullname))
                    {
                        javaClass.SelectAll(true);
                    }
                }
            }
        }

        private static bool TryGetEntry(ProGuardSelection selection,
            Dictionary<string, AndroidArtifactEntry> byStableKey,
            Dictionary<string, List<AndroidArtifactEntry>> byName,
            out AndroidArtifactEntry entry)
        {
            entry = null;

            if (!string.IsNullOrEmpty(selection.OriginPath)
                && byStableKey.TryGetValue(GetStableKey(selection.Source, selection.OriginPath), out entry))
            {
                return true;
            }

            if (string.IsNullOrEmpty(selection.Artifact)
                || !byName.TryGetValue(selection.Artifact, out List<AndroidArtifactEntry> namedEntries)
                || namedEntries.Count != 1)
            {
                return false;
            }

            entry = namedEntries[0];
            return true;
        }

        private static string GetStableKey(AndroidArtifactEntry entry)
        {
            return GetStableKey(entry.Source, entry.OriginPath);
        }

        private static string GetStableKey(AndroidArtifactSource source, string originPath)
        {
            return ((int)source).ToString() + "|" + (originPath ?? string.Empty);
        }
    }
}
