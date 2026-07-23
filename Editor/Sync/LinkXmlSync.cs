using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Build;
using UnityEngine;

namespace DTech.LinkGuard.Editor
{
    /// <summary>
    /// Keeps an existing link.xml in sync with the code that is actually in the project: types
    /// added after the file was generated are covered again, so new features are not stripped.
    /// Entries are only added — nothing is removed, reordered, or narrowed. Intended as a
    /// build-time entry point.
    /// </summary>
    public static class LinkXmlSync
    {
        /// <summary>
        /// Synchronizes the link.xml at the default path with the current project code.
        /// </summary>
        /// <param name="apply">When <c>true</c>, writes the extended link.xml back to disk.</param>
        /// <param name="throwOnError">
        /// When <c>true</c>, throws <see cref="UnityEditor.Build.BuildFailedException"/> when the
        /// existing link.xml cannot be read or parsed; when <c>false</c>, the failure is captured
        /// in the returned report.
        /// </param>
        /// <returns>A report of what was added and what is still untracked.</returns>
        public static LinkXmlSyncReport Sync(bool apply = true, bool throwOnError = false)
        {
            return Sync(null, apply, throwOnError);
        }

        /// <summary>
        /// Synchronizes the link.xml at the default path, additionally forcing coverage of every
        /// assembly and namespace matching one of the supplied scope patterns.
        /// </summary>
        /// <param name="scopePatterns">
        /// Glob patterns (<c>*</c> and <c>?</c> wildcards) matched against assembly names and
        /// namespace names, for example <c>Game.*</c>. Use them to cover assemblies that link.xml
        /// does not mention at all yet.
        /// </param>
        /// <param name="apply">When <c>true</c>, writes the extended link.xml back to disk.</param>
        /// <param name="throwOnError">
        /// When <c>true</c>, throws <see cref="UnityEditor.Build.BuildFailedException"/> on a
        /// read or parse failure.
        /// </param>
        /// <returns>A report of what was added and what is still untracked.</returns>
        public static LinkXmlSyncReport Sync(
            IReadOnlyList<string> scopePatterns,
            bool apply = true,
            bool throwOnError = false)
        {
            return Sync(ScannedProjectTypeSource.Create(), LinkXmlWriter.DefaultPath, scopePatterns, apply, throwOnError);
        }

        internal static LinkXmlSyncReport Sync(
            IProjectTypeSource source,
            string path,
            IReadOnlyList<string> scopePatterns,
            bool apply,
            bool throwOnError)
        {
            string normalized = string.IsNullOrEmpty(path) ? LinkXmlWriter.DefaultPath : path;

            if (!File.Exists(normalized))
            {
                Debug.Log($"[LinkXmlGenerator] No link.xml found at {normalized}; nothing to sync.");

                return new LinkXmlSyncReport(
                    normalized,
                    string.Empty,
                    success: true,
                    failureReason: string.Empty,
                    fileExisted: false,
                    changed: false,
                    written: false,
                    addedAssemblies: null,
                    addedNamespaces: null,
                    addedTypes: null,
                    untrackedAssemblies: null);
            }

            string xml;

            try
            {
                xml = File.ReadAllText(normalized);
            }
            catch (Exception ex)
            {
                return Fail(normalized, $"Failed to read link.xml at {normalized}: {ex.Message}", throwOnError);
            }

            LinkXmlSyncOutcome outcome = LinkXmlSyncEngine.Sync(xml, source, scopePatterns);

            if (!outcome.Success)
            {
                return Fail(normalized, outcome.FailureReason, throwOnError);
            }

            bool written = false;

            if (apply && outcome.Changed)
            {
                LinkXmlWriter.Write(outcome.Xml, normalized);
                written = true;
            }

            LinkXmlSyncReport report = new LinkXmlSyncReport(
                normalized,
                outcome.Xml,
                success: true,
                failureReason: string.Empty,
                fileExisted: true,
                changed: outcome.Changed,
                written: written,
                outcome.AddedAssemblies,
                outcome.AddedNamespaces,
                outcome.AddedTypes,
                outcome.UntrackedAssemblies);

            LogReport(report);
            return report;
        }

        internal static void Apply(LinkXmlSyncReport report)
        {
            if (report == null || !report.Success || !report.Changed)
            {
                return;
            }

            LinkXmlWriter.Write(report.Xml, report.OutputPath);
        }

        private static LinkXmlSyncReport Fail(string path, string reason, bool throwOnError)
        {
            Debug.LogError($"[LinkXmlGenerator] link.xml sync failed: {reason}");

            if (throwOnError)
            {
                throw new BuildFailedException($"[LinkXmlGenerator] link.xml sync failed: {reason}");
            }

            return new LinkXmlSyncReport(
                path,
                string.Empty,
                success: false,
                failureReason: reason,
                fileExisted: true,
                changed: false,
                written: false,
                addedAssemblies: null,
                addedNamespaces: null,
                addedTypes: null,
                untrackedAssemblies: null);
        }

        private static void LogReport(LinkXmlSyncReport report)
        {
            Debug.Log($"[LinkXmlGenerator] {report}");

            foreach (string assembly in report.AddedAssemblies)
            {
                Debug.Log($"[LinkXmlGenerator] Added assembly: {assembly}");
            }

            foreach (LinkXmlSyncEntryGroup group in report.AddedNamespaces)
            {
                foreach (string namespaceName in group.Names)
                {
                    Debug.Log($"[LinkXmlGenerator] Added namespace: {group.AssemblyName} -> {namespaceName}");
                }
            }

            foreach (LinkXmlSyncEntryGroup group in report.AddedTypes)
            {
                foreach (string typeName in group.Names)
                {
                    Debug.Log($"[LinkXmlGenerator] Added type: {group.AssemblyName} -> {typeName}");
                }
            }

            foreach (string assembly in report.UntrackedAssemblies)
            {
                Debug.LogWarning(
                    $"[LinkXmlGenerator] Untracked project assembly (not present in link.xml): {assembly}");
            }
        }
    }
}
