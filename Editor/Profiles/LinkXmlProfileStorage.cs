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

            string json;

            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog("Load Profile", $"Failed to read profile: {ex.Message}", "OK");
                }
                else
                {
                    Debug.LogWarning($"[LinkXmlGenerator] Failed to read profile at {path}: {ex.Message}");
                }

                return false;
            }

            LinkXmlProfile profile;

            try
            {
                profile = JsonUtility.FromJson<LinkXmlProfile>(json);
            }
            catch (Exception ex)
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog("Load Profile", $"Failed to parse profile: {ex.Message}", "OK");
                }
                else
                {
                    Debug.LogWarning($"[LinkXmlGenerator] Failed to parse profile at {path}: {ex.Message}");
                }

                return false;
            }

            if (profile == null || profile.Selections == null)
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog("Load Profile", "Profile is empty or invalid.", "OK");
                }
                else
                {
                    Debug.LogWarning($"[LinkXmlGenerator] Profile at {path} is empty or invalid.");
                }

                return false;
            }

            ApplyProfile(profile, entries);
            Debug.Log($"[LinkXmlGenerator] Profile loaded from {path}");

            return true;
        }

        private static LinkXmlProfile ToProfile(IReadOnlyList<AssemblyEntry> entries)
        {
            LinkXmlProfile profile = new LinkXmlProfile
            {
                Version = 2
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
                        .ToList(),
                    Methods = entry.Types
                        .Where(t => !t.IsSelected)
                        .SelectMany(t => t.Methods
                            .Where(m => m.IsSelected)
                            .Select(m => new LinkXmlMethodSelection
                            {
                                Type = t.LinkerFullname,
                                Signature = m.Signature
                            }))
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

                if (selection.Methods == null)
                {
                    continue;
                }

                foreach (LinkXmlMethodSelection methodSelection in selection.Methods)
                {
                    if (methodSelection == null
                        || string.IsNullOrEmpty(methodSelection.Type)
                        || string.IsNullOrEmpty(methodSelection.Signature))
                    {
                        continue;
                    }

                    TypeEntry type = entry.Types.FirstOrDefault(t =>
                        string.Equals(t.LinkerFullname, methodSelection.Type, StringComparison.Ordinal)
                        || string.Equals(t.Fullname, methodSelection.Type, StringComparison.Ordinal));

                    if (type == null || type.IsSelected)
                    {
                        continue;
                    }

                    MethodEntry method = type.Methods.FirstOrDefault(m =>
                        string.Equals(m.Signature, methodSelection.Signature, StringComparison.Ordinal));

                    if (method != null)
                    {
                        method.IsSelected = true;
                    }
                }
            }
        }
    }
}
