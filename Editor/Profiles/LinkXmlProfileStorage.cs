using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DTech.LinkGuard.Editor
{
    public static class LinkXmlProfileStorage
    {
        private const string DefaultDirectory = "ProjectSettings";
        private const string DefaultFileName = "LinkXmlProfile.json";

        public static bool Save(IReadOnlyList<AssemblyEntry> entries)
        {
            string directory = Path.Combine(Directory.GetCurrentDirectory(), DefaultDirectory);

            if (!Directory.Exists(directory))
            {
                directory = Directory.GetCurrentDirectory();
            }

            string path = EditorUtility.SaveFilePanel(
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

        public static bool Load(List<AssemblyEntry> entries)
        {
            string directory = Path.Combine(Directory.GetCurrentDirectory(), DefaultDirectory);

            if (!Directory.Exists(directory))
            {
                directory = Directory.GetCurrentDirectory();
            }

            string path = EditorUtility.OpenFilePanel("Load Link.xml Profile", directory, "json");

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            string json = File.ReadAllText(path);

            LinkXmlProfile profile;

            try
            {
                profile = JsonUtility.FromJson<LinkXmlProfile>(json);
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
            Debug.Log($"[LinkXmlGenerator] Profile loaded from {path}");

            return true;
        }

        private static LinkXmlProfile ToProfile(IReadOnlyList<AssemblyEntry> entries)
        {
            LinkXmlProfile profile = new LinkXmlProfile();

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
                        .Where(n => n.IsSelected)
                        .Select(n => n.Fullname)
                        .ToList(),
                    GlobalTypes = entry.GlobalTypes
                        .Where(t => t.IsSelected)
                        .Select(t => t.Fullname)
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

                HashSet<string> wanted = selection.Namespaces == null
                    ? new HashSet<string>(StringComparer.Ordinal)
                    : new HashSet<string>(selection.Namespaces, StringComparer.Ordinal);

                foreach (NamespaceEntry ns in entry.Namespaces)
                {
                    ns.IsSelected = wanted.Contains(ns.Fullname);
                }

                HashSet<string> wantedTypes = selection.GlobalTypes == null
                    ? new HashSet<string>(StringComparer.Ordinal)
                    : new HashSet<string>(selection.GlobalTypes, StringComparer.Ordinal);

                foreach (GlobalTypeEntry t in entry.GlobalTypes)
                {
                    t.IsSelected = wantedTypes.Contains(t.Fullname);
                }
            }
        }
    }
}
