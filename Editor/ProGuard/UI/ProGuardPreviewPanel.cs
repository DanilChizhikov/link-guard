using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace DTech.LinkGuard.Editor.ProGuard
{
    internal sealed class ProGuardPreviewPanel
    {
        public bool HasContent => !string.IsNullOrEmpty(_text);

        private readonly ScrollView _scroll;
        private readonly TextField _textField;
        private readonly Label _emptyLabel;
        private readonly ToolbarButton _copyButton;

        private string _text = string.Empty;

        public ProGuardPreviewPanel(VisualElement host)
        {
            host.Clear();

            Toolbar toolbar = new Toolbar();
            toolbar.AddToClassList("lxg-preview-toolbar");

            Label title = new Label("Preview");
            title.AddToClassList("lxg-preview-title");
            toolbar.Add(title);

            VisualElement spacer = new VisualElement();
            spacer.AddToClassList("lxg-tb-flex");
            toolbar.Add(spacer);

            _copyButton = new ToolbarButton(CopyClickedHandler) { text = "Copy" };
            _copyButton.SetEnabled(false);
            toolbar.Add(_copyButton);

            host.Add(toolbar);

            _scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal)
            {
                verticalScrollerVisibility = ScrollerVisibility.Auto,
                horizontalScrollerVisibility = ScrollerVisibility.Auto,
            };

            _scroll.AddToClassList("lxg-preview-scroll");
            _scroll.style.display = DisplayStyle.None;
            host.Add(_scroll);

            _textField = new TextField
            {
                multiline = true,
                isReadOnly = true,
                value = string.Empty,
            };
            _textField.AddToClassList("lxg-preview-text");
            _scroll.Add(_textField);

            _emptyLabel = new Label("Press Preview to render the rules.");
            _emptyLabel.AddToClassList("lxg-preview-empty");
            host.Add(_emptyLabel);
        }

        public void SetText(string text)
        {
            _text = text ?? string.Empty;
            _textField.SetValueWithoutNotify(_text);
            _copyButton.SetEnabled(HasContent);

            bool empty = !HasContent;
            _emptyLabel.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;
            _scroll.style.display = empty ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void CopyClickedHandler()
        {
            if (!HasContent)
            {
                return;
            }

            EditorGUIUtility.systemCopyBuffer = _text;
        }
    }
}
