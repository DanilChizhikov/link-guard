namespace DTech.LinkGuard.Editor.ProGuard
{
    /// <summary>
    /// Outcome of a <see cref="ProGuardPatcher.Patch(string)"/> run: the written rules
    /// file and how many keep rules, artifacts, and classes it covers.
    /// </summary>
    public readonly struct ProGuardPatchReport
    {
        /// <summary>Path the ProGuard rules were (or would be) written to.</summary>
        public string Path { get; }

        /// <summary>Number of <c>-keep</c> rules written.</summary>
        public int RuleCount { get; }

        /// <summary>Number of Android artifacts that produced rules.</summary>
        public int ArtifactCount { get; }

        /// <summary>Number of Java/Kotlin classes covered by the rules.</summary>
        public int ClassCount { get; }

        /// <summary>Whether the run was skipped (for example, minification disabled).</summary>
        public bool Skipped { get; }

        /// <summary>Reason the run was skipped or failed; <c>null</c> on a normal write.</summary>
        public string SkipReason { get; }

        /// <summary>
        /// Creates a ProGuard patch report.
        /// </summary>
        /// <param name="path">Path the rules target.</param>
        /// <param name="ruleCount">Number of keep rules written.</param>
        /// <param name="artifactCount">Number of artifacts that produced rules.</param>
        /// <param name="classCount">Number of classes covered.</param>
        /// <param name="skipped">Whether the run was skipped.</param>
        /// <param name="skipReason">Reason for a skip or failure.</param>
        public ProGuardPatchReport(string path, int ruleCount, int artifactCount, int classCount, bool skipped,
            string skipReason)
        {
            Path = path;
            RuleCount = ruleCount;
            ArtifactCount = artifactCount;
            ClassCount = classCount;
            Skipped = skipped;
            SkipReason = skipReason;
        }

        /// <summary>Returns a one-line summary of the ProGuard outcome.</summary>
        /// <returns>A human-readable summary string.</returns>
        public override string ToString()
        {
            return Skipped
                ? $"[proguard] skipped: {SkipReason}"
                : $"[proguard] wrote {Path}: {RuleCount} keep rule(s) from {ArtifactCount} artifact(s), {ClassCount} class(es).";
        }
    }
}
