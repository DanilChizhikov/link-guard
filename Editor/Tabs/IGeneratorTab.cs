using UnityEngine.UIElements;

namespace DTech.LinkGuard.Editor
{
    /// <summary>
    /// A tab hosted by the Link Guard window. Implementations are discovered at runtime
    /// and shown as tabs when <see cref="IsAvailable"/> is <c>true</c>.
    /// </summary>
    public interface IGeneratorTab
    {
        /// <summary>Label shown on the tab.</summary>
        string TabLabel { get; }

        /// <summary>Sort order of the tab; lower values appear first.</summary>
        int Order { get; }

        /// <summary>Whether the tab should be shown for the current editor state.</summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Builds the tab's UI.
        /// </summary>
        /// <returns>The root visual element of the tab's view.</returns>
        VisualElement CreateView();
    }
}
