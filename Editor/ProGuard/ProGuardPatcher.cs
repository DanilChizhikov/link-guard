using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DTech.LinkGuard.Editor.ProGuard
{
    public static class ProGuardPatcher
    {
        public static ProGuardPatchReport Patch(string path = null)
        {
            string target = string.IsNullOrEmpty(path) ? ProGuardBuildSettings.ProguardUserFilePath : path;

            if (!ProGuardBuildSettings.IsMinifyEnabled())
            {
                ProGuardPatchReport skipped = new ProGuardPatchReport(
                    target, 0, 0, 0, true, "Android minification is disabled.");
                Debug.Log($"[LinkGuard] {skipped}");
                return skipped;
            }

            List<AndroidArtifactEntry> entries = AndroidArtifactScanner.Scan();

            foreach (AndroidArtifactEntry entry in entries)
            {
                entry.SelectAll(true);
            }

            string text = ProGuardRulesBuilder.Build(entries);
            if (!ProGuardWriter.Write(text, target))
            {
                ProGuardPatchReport failed = new ProGuardPatchReport(
                    target, 0, entries.Count(e => e.ProducesEntry), entries.Sum(e => e.ClassCount), true,
                    "Could not write ProGuard rules. Enable 'Custom Proguard File' manually and try again.");
                Debug.LogWarning($"[LinkGuard] {failed}");
                return failed;
            }

            ProGuardPatchReport report = new ProGuardPatchReport(
                target,
                CountRules(text),
                entries.Count(e => e.ProducesEntry),
                entries.Sum(e => e.ClassCount),
                false,
                null);

            Debug.Log($"[LinkGuard] {report}");
            return report;
        }

        private static int CountRules(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int count = 0;

            foreach (string line in text.Split('\n'))
            {
                if (line.StartsWith("-keep", StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
