using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DTech.LinkGuard.Editor
{
    internal static class LinkXmlProfileStorage
    {
        private const string DefaultDirectory = "ProjectSettings";
        private const string DefaultFileName = "LinkXmlProfile.json";

        public static bool Save(IReadOnlyList<AssemblyEntry> entries, out string path)
        {
            string directory = Path.Combine(Directory.GetCurrentDirectory(), DefaultDirectory);

            if (!Directory.Exists(directory))
            {
                directory = Directory.GetCurrentDirectory();
            }

            path = EditorUtility.SaveFilePanel(
                "Save Link.xml Profile",
                directory,
                DefaultFileName,
                "json");

            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            LinkXmlProfile profile = ToProfile(entries);
            string json = JsonUtility.ToJson(profile, true);
            File.WriteAllText(path, json);

            Debug.Log($"[LinkXmlGenerator] Profile saved to {path}");

            return true;
        }

        public static bool Load(List<AssemblyEntry> entries, out string path)
        {
            string directory = Path.Combine(Directory.GetCurrentDirectory(), DefaultDirectory);

            if (!Directory.Exists(directory))
            {
                directory = Directory.GetCurrentDirectory();
            }

            path = EditorUtility.OpenFilePanel("Load Link.xml Profile", directory, "json");

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            return Load(entries, path, true);
        }

        public static bool Load(List<AssemblyEntry> entries, string path, bool showDialogs)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            LinkXmlProfile profile;

            try
            {
                string json = File.ReadAllText(path);
                profile = JsonUtility.FromJson<LinkXmlProfile>(json);

                if (profile == null || profile.Selections == null)
                {
                    return ReportLoadFailure(path, "Profile is empty or invalid.", showDialogs);
                }
            }
            catch (Exception ex)
            {
                return ReportLoadFailure(path, $"Failed to load profile: {ex.Message}", showDialogs);
            }

            ApplyProfile(profile, entries);
            Debug.Log($"[LinkXmlGenerator] Profile loaded from {path}");

            return true;
        }

        private static bool ReportLoadFailure(string path, string message, bool showDialogs)
        {
            Debug.LogError($"[LinkXmlGenerator] {message} (path: {path})");

            if (showDialogs)
            {
                EditorUtility.DisplayDialog("Load Profile", message, "OK");
            }

            return false;
        }

        private static LinkXmlProfile ToProfile(IReadOnlyList<AssemblyEntry> entries)
        {
            LinkXmlProfile profile = new LinkXmlProfile
            {
                Version = 3
            };

            foreach (AssemblyEntry entry in entries)
            {
                if (!entry.ProducesEntry)
                {
                    continue;
                }

                profile.Selections.Add(new LinkXmlSelection
                {
                    Assembly = entry.Name,
                    PreserveAll = entry.IsAssemblySelected,
                    IgnoreIfMissing = entry.IgnoreIfMissing,
                    Namespaces = entry.Namespaces
                        .Where(n => !string.IsNullOrEmpty(n.Fullname) && n.IsSelected)
                        .Select(n => n.Fullname)
                        .ToList(),
                    GlobalTypes = entry.Types
                        .Where(t => string.IsNullOrEmpty(t.Namespace) && t.IsSelected)
                        .Select(t => t.LinkerFullname)
                        .ToList(),
                    Types = entry.Types
                        .Where(t => t.IsSelected)
                        .Select(t => t.LinkerFullname)
                        .ToList()
                });
            }

            return profile;
        }

        private static void ApplyProfile(LinkXmlProfile profile, List<AssemblyEntry> entries)
        {
            Dictionary<string, AssemblyEntry> byName = entries.ToDictionary(e => e.Name, e => e);

            foreach (AssemblyEntry entry in entries)
            {
                entry.SelectAll(false);
                entry.IgnoreIfMissing = false;
            }

            int promotedTypes = 0;

            foreach (LinkXmlSelection selection in profile.Selections)
            {
                if (string.IsNullOrEmpty(selection.Assembly))
                {
                    continue;
                }

                if (!byName.TryGetValue(selection.Assembly, out AssemblyEntry entry))
                {
                    continue;
                }

                entry.IgnoreIfMissing = selection.IgnoreIfMissing;
                entry.IsAssemblySelected = selection.PreserveAll;

                HashSet<string> wantedNamespaces = selection.Namespaces == null
                    ? new HashSet<string>(StringComparer.Ordinal)
                    : new HashSet<string>(selection.Namespaces, StringComparer.Ordinal);

                foreach (NamespaceEntry ns in entry.Namespaces)
                {
                    if (wantedNamespaces.Contains(ns.Fullname))
                    {
                        ns.IsSelected = true;
                    }
                }

                HashSet<string> wantedGlobalTypes = selection.GlobalTypes == null
                    ? new HashSet<string>(StringComparer.Ordinal)
                    : new HashSet<string>(selection.GlobalTypes, StringComparer.Ordinal);

                HashSet<string> wantedTypes = selection.Types == null
                    ? new HashSet<string>(StringComparer.Ordinal)
                    : new HashSet<string>(selection.Types, StringComparer.Ordinal);

                foreach (TypeEntry type in entry.Types)
                {
                    if (wantedTypes.Contains(type.LinkerFullname)
                        || wantedTypes.Contains(type.Fullname)
                        || wantedGlobalTypes.Contains(type.LinkerFullname)
                        || wantedGlobalTypes.Contains(type.Fullname))
                    {
                        type.SelectAll(true);
                    }
                }

                promotedTypes += PromoteLegacyMethodSelections(selection, entry);
            }

            if (promotedTypes > 0)
            {
                Debug.LogWarning(
                    $"[LinkXmlGenerator] Loaded legacy v2 profile: promoted {promotedTypes} method-level selection(s) to whole-type preserve=\"all\".");
            }
        }

        private static int PromoteLegacyMethodSelections(LinkXmlSelection selection, AssemblyEntry entry)
        {
            if (selection.Methods == null || selection.Methods.Count == 0)
            {
                return 0;
            }

            HashSet<string> uniqueTypes = new HashSet<string>(StringComparer.Ordinal);

            foreach (LinkXmlMethodSelection method in selection.Methods)
            {
                if (method == null || string.IsNullOrEmpty(method.Type))
                {
                    continue;
                }

                uniqueTypes.Add(method.Type);
            }

            int promoted = 0;

            foreach (string typeName in uniqueTypes)
            {
                TypeEntry type = entry.Types.FirstOrDefault(t =>
                    string.Equals(t.LinkerFullname, typeName, StringComparison.Ordinal)
                    || string.Equals(t.Fullname, typeName, StringComparison.Ordinal));

                if (type == null || type.IsSelected)
                {
                    continue;
                }

                type.SelectAll(true);
                promoted++;
            }

            return promoted;
        }
    }
}
