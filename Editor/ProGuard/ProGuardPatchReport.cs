namespace DTech.LinkGuard.Editor.ProGuard
{
    public readonly struct ProGuardPatchReport
    {
        public string Path { get; }
        public int RuleCount { get; }
        public int ArtifactCount { get; }
        public int ClassCount { get; }
        public bool Skipped { get; }
        public string SkipReason { get; }

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

        public override string ToString()
        {
            return Skipped
                ? $"[proguard] skipped: {SkipReason}"
                : $"[proguard] wrote {Path}: {RuleCount} keep rule(s) from {ArtifactCount} artifact(s), {ClassCount} class(es).";
        }
    }
}
