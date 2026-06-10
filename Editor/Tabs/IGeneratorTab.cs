using UnityEngine.UIElements;

namespace DTech.LinkGuard.Editor
{
    public interface IGeneratorTab
    {
        string TabLabel { get; }
        int Order { get; }
        bool IsAvailable { get; }
        VisualElement CreateView();
    }
}
