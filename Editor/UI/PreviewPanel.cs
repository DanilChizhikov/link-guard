using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace DTech.LinkGuard.Editor
{
    internal sealed class PreviewPanel
    {
        public bool HasContent => !string.IsNullOrEmpty(_xml);
        
        private readonly VisualElement _root;
        private readonly ScrollView _scroll;
        private readonly TextField _textField;
        private readonly Label _emptyLabel;
        private readonly ToolbarButton _copyButton;

        private string _xml = string.Empty;

        public PreviewPanel(VisualElement host)
        {
            _root = host;
            _root.Clear();

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

            _root.Add(toolbar);

            _scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal)
            {
                verticalScrollerVisibility = ScrollerVisibility.Auto,
                horizontalScrollerVisibility = ScrollerVisibility.Auto,
            };
            
            _scroll.AddToClassList("lxg-preview-scroll");
            _scroll.style.display = DisplayStyle.None;
            _root.Add(_scroll);

            _textField = new TextField
            {
                multiline = true,
                isReadOnly = true,
                value = string.Empty,
            };
            _textField.AddToClassList("lxg-preview-text");
            _scroll.Add(_textField);

            _emptyLabel = new Label("Press Preview to render the XML.");
            _emptyLabel.AddToClassList("lxg-preview-empty");
            _root.Add(_emptyLabel);
        }

        public void SetXml(string xml)
        {
            _xml = xml ?? string.Empty;
            _textField.SetValueWithoutNotify(_xml);
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

            EditorGUIUtility.systemCopyBuffer = _xml;
        }
    }
}
