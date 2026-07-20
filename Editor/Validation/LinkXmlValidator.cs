using System;
using System.IO;
using UnityEditor.Build;
using UnityEngine;

namespace DTech.LinkGuard.Editor
{
    /// <summary>
    /// Validates the project's link.xml against the current build, removing entries for
    /// assemblies and types no longer included. Intended as a build-time entry point.
    /// </summary>
    public static class LinkXmlValidator
    {
        /// <summary>
        /// Validates the link.xml at the default path against the current build.
        /// </summary>
        /// <param name="apply">When <c>true</c>, writes the corrected link.xml back to disk.</param>
        /// <param name="throwOnError">
        /// When <c>true</c>, throws <see cref="UnityEditor.Build.BuildFailedException"/> on a
        /// validation failure; when <c>false</c>, the failure is captured in the returned report.
        /// </param>
        /// <returns>A report of what was removed, kept, or flagged.</returns>
        public static LinkXmlValidationReport Validate(bool apply = true, bool throwOnError = false)
        {
            return Validate(new PlayerBuildMembershipOracle(), LinkXmlWriter.DefaultPath, apply, throwOnError);
        }

        internal static LinkXmlValidationReport Validate(
            IBuildMembershipOracle oracle,
            string path,
            bool apply,
            bool throwOnError)
        {
            string normalized = string.IsNullOrEmpty(path) ? LinkXmlWriter.DefaultPath : path;

            if (!File.Exists(normalized))
            {
                Debug.Log($"[LinkXmlGenerator] No link.xml found at {normalized}; nothing to validate.");

                return new LinkXmlValidationReport(
                    normalized,
                    string.Empty,
                    success: true,
                    failureReason: string.Empty,
                    fileExisted: false,
                    changed: false,
                    written: false,
                    removedAssemblies: null,
                    removedTypes: null,
                    keptIgnoreIfMissing: null,
                    keptUnknown: null);
            }

            string xml;

            try
            {
                xml = File.ReadAllText(normalized);
            }
            catch (Exception ex)
            {
                string reason = $"Failed to read link.xml at {normalized}: {ex.Message}";
                Debug.LogError($"[LinkXmlGenerator] {reason}");

                if (throwOnError)
                {
                    throw new BuildFailedException($"[LinkXmlGenerator] link.xml validation failed: {reason}");
                }

                return FailedReport(normalized, reason);
            }

            LinkXmlValidationOutcome outcome = LinkXmlValidationEngine.Validate(xml, oracle);

            if (!outcome.Success)
            {
                Debug.LogError($"[LinkXmlGenerator] link.xml validation failed: {outcome.FailureReason}");

                if (throwOnError)
                {
                    throw new BuildFailedException(
                        $"[LinkXmlGenerator] link.xml validation failed: {outcome.FailureReason}");
                }

                return FailedReport(normalized, outcome.FailureReason);
            }

            bool written = false;

            if (apply && outcome.Changed)
            {
                LinkXmlWriter.Write(outcome.Xml, normalized);
                written = true;
            }

            LinkXmlValidationReport report = new LinkXmlValidationReport(
                normalized,
                outcome.Xml,
                success: true,
                failureReason: string.Empty,
                fileExisted: true,
                changed: outcome.Changed,
                written: written,
                outcome.RemovedAssemblies,
                outcome.RemovedTypes,
                outcome.KeptIgnoreIfMissing,
                outcome.KeptUnknown);

            LogReport(report);
            return report;
        }

        internal static void Apply(LinkXmlValidationReport report)
        {
            if (report == null || !report.Success || !report.Changed)
            {
                return;
            }

            LinkXmlWriter.Write(report.Xml, report.OutputPath);
        }

        private static LinkXmlValidationReport FailedReport(string path, string reason)
        {
            return new LinkXmlValidationReport(
                path,
                string.Empty,
                success: false,
                failureReason: reason,
                fileExisted: true,
                changed: false,
                written: false,
                removedAssemblies: null,
                removedTypes: null,
                keptIgnoreIfMissing: null,
                keptUnknown: null);
        }

        private static void LogReport(LinkXmlValidationReport report)
        {
            Debug.Log($"[LinkXmlGenerator] {report}");

            foreach (string assembly in report.RemovedAssemblies)
            {
                Debug.Log($"[LinkXmlGenerator] Removed assembly: {assembly}");
            }

            foreach (LinkXmlValidationTypeGroup group in report.RemovedTypes)
            {
                foreach (string type in group.TypeNames)
                {
                    Debug.Log($"[LinkXmlGenerator] Removed type: {group.AssemblyName} -> {type}");
                }
            }

            foreach (string assembly in report.KeptIgnoreIfMissing)
            {
                Debug.Log($"[LinkXmlGenerator] Kept (ignoreIfMissing): {assembly}");
            }

            foreach (LinkXmlValidationSkippedEntry entry in report.KeptUnknown)
            {
                string target = string.IsNullOrEmpty(entry.TypeName)
                    ? entry.AssemblyName
                    : $"{entry.AssemblyName} -> {entry.TypeName}";
                Debug.LogWarning($"[LinkXmlGenerator] Kept (unverified): {target} ({entry.Reason})");
            }
        }
    }
}
