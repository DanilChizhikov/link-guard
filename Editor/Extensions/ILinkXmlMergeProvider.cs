namespace DTech.LinkGuard.Editor
{
    /// <summary>
    /// Contributes link.xml content to the merge pipeline. Implementations are
    /// discovered via <see cref="UnityEditor.TypeCache"/>, surfaced as toolbar buttons
    /// in the Link Guard window, and run by <see cref="LinkXmlPatcher"/> at build time.
    /// </summary>
    public interface ILinkXmlMergeProvider
    {
        /// <summary>Stable identifier for this provider, used in reports and de-duplication.</summary>
        string Id { get; }

        /// <summary>Label shown on the provider's toolbar button.</summary>
        string ButtonLabel { get; }

        /// <summary>Tooltip shown for the provider's toolbar button.</summary>
        string Tooltip { get; }

        /// <summary>
        /// Produces this provider's link.xml contribution.
        /// </summary>
        /// <returns>The provider's XML, report text, warnings, and success state.</returns>
        LinkXmlProviderResult Provide();
    }
}
