using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.LinkGuard.Editor
{
    internal sealed class LinkXmlGeneratorWindow : EditorWindow
    {
        private const string MenuPath = "Window/DTech/" + Title;
        private const string UxmlName = "LinkXmlGeneratorWindow";
        private const string USSName = "LinkXmlGeneratorWindow";
        private const string ShowPreviewKey = "LinkXmlGenerator.ShowPreview";
        private const string SplitPxKey = "LinkXmlGenerator.SplitPx";
        private const string Title = "Link XML Generator";
        private const float DefaultPreviewHeight = 220f;

        private List<AssemblyEntry> _entries = new();
        private AssemblyTreeController _treeController;
        private PreviewPanel _previewPanel;
        private TwoPaneSplitView _splitter;
        private VisualElement _previewHost;
        private ToolbarToggle _previewToggle;
        private ToolbarButton _generateButton;
        private Button _updatePreviewButton;
        private Label _footerLabel;
        private bool _showPreview;
        private bool _previewDirty;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            LinkXmlGeneratorWindow window = GetWindow<LinkXmlGeneratorWindow>();
            window.titleContent = new GUIContent(Title);
            window.minSize = new Vector2(640f, 480f);
            window.Show();
        }

        public void CreateGUI()
        {
            VisualTreeAsset tree = Resources.Load<VisualTreeAsset>(UxmlName);
            StyleSheet styles = Resources.Load<StyleSheet>(USSName);

            if (tree == null)
            {
                rootVisualElement.Add(new Label($"Failed to load UXML at {UxmlName}"));
                return;
            }

            tree.CloneTree(rootVisualElement);

            if (styles != null)
            {
                rootVisualElement.styleSheets.Add(styles);
            }

            CacheElements();
            WireToolbar();
            InitSplitter();
            ApplyShowPreview();
            UpdateFooter();

            if (_entries.Count == 0)
            {
                EditorApplication.delayCall += Refresh;
            }
        }

        private void CacheElements()
        {
            VisualElement root = rootVisualElement;

            ToolbarButton refreshBtn = root.Q<ToolbarButton>("btn-refresh");
            ToolbarButton selectAllBtn = root.Q<ToolbarButton>("btn-select-all");
            ToolbarButton noneBtn = root.Q<ToolbarButton>("btn-none");
            ToolbarButton loadBtn = root.Q<ToolbarButton>("btn-load");
            ToolbarButton saveBtn = root.Q<ToolbarButton>("btn-save");
            _previewToggle = root.Q<ToolbarToggle>("tgl-preview");
            _generateButton = root.Q<ToolbarButton>("btn-generate");

            ToolbarSearchField searchField = root.Q<ToolbarSearchField>("search-field");

            _splitter = root.Q<TwoPaneSplitView>("splitter");
            _previewHost = root.Q<VisualElement>("preview-host");

            TreeView treeView = root.Q<TreeView>("tree");
            Label emptyHint = root.Q<Label>("empty-hint");

            _footerLabel = root.Q<Label>("footer-label");
            _updatePreviewButton = root.Q<Button>("btn-update-preview");

            _treeController = new AssemblyTreeController(treeView, emptyHint)
            {
                OnChanged = SelectionChangedHandler
            };

            _previewPanel = new PreviewPanel(_previewHost);

            refreshBtn.clicked += Refresh;
            selectAllBtn.clicked += SelectAllClickedHandler;
            noneBtn.clicked += NoneClickedHandler;
            loadBtn.clicked += LoadProfileClickedHandler;
            saveBtn.clicked += SaveProfileClickedHandler;
            _generateButton.clicked += Generate;
            _updatePreviewButton.clicked += RebuildPreview;

            searchField.RegisterValueChangedCallback(SearchChangedHandler);
            _previewToggle.RegisterValueChangedCallback(PreviewToggleChangedHandler);
        }

        private void WireToolbar()
        {
            _showPreview = EditorPrefs.GetBool(ShowPreviewKey, false);
            _previewToggle.SetValueWithoutNotify(_showPreview);
        }

        private void InitSplitter()
        {
            float savedPx = EditorPrefs.GetFloat(SplitPxKey, DefaultPreviewHeight);
            _splitter.fixedPaneInitialDimension = Mathf.Max(80f, savedPx);

            _previewHost.RegisterCallback<GeometryChangedEvent>(PreviewGeometryChangedHandler);
        }

        private void ApplyShowPreview()
        {
            if (_showPreview)
            {
                if (_splitter.fixedPane == null || _splitter.fixedPane.resolvedStyle.display == DisplayStyle.None)
                {
                    _splitter.UnCollapse();
                }
            }
            else
            {
                _splitter.CollapseChild(1);
            }

            UpdateFooter();
        }

        private void Refresh()
        {
            try
            {
                EditorUtility.DisplayProgressBar(Title, "Scanning assemblies...", 0.3f);
                _entries = AssemblyScanner.Scan();
                _treeController.SetEntries(_entries);
                MarkPreviewDirty();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            UpdateFooter();
        }

        private void Generate()
        {
            if (!HasAnySelection())
            {
                EditorUtility.DisplayDialog(Title, "Nothing is selected.", "OK");
                return;
            }

            string xml = LinkXmlBuilder.Build(_entries);
            _previewPanel.SetXml(xml);
            _previewDirty = false;

            LinkXmlWriter.WriteWithConfirmation(xml);
            UpdateFooter();
        }

        private void RebuildPreview()
        {
            string xml = LinkXmlBuilder.Build(_entries);
            _previewPanel.SetXml(xml);
            _previewDirty = false;
            UpdateFooter();
        }

        private void MarkPreviewDirty()
        {
            _previewDirty = true;
            _generateButton.SetEnabled(HasAnySelection());
            UpdateFooter();
        }

        private bool HasAnySelection()
        {
            return _entries.Any(e => e.ProducesEntry);
        }

        private void UpdateFooter()
        {
            int total = _entries.Count;
            int selected = _entries.Count(e => e.ProducesEntry);

            _footerLabel.text = $"Assemblies: {total}    Selected: {selected}    Target: {LinkXmlWriter.DefaultPath}";

            bool showUpdate = _showPreview && _previewDirty && _entries.Count > 0;
            _updatePreviewButton.style.display = showUpdate ? DisplayStyle.Flex : DisplayStyle.None;
            _generateButton.SetEnabled(HasAnySelection());
        }

        private void OnDisable()
        {
            EditorPrefs.SetBool(ShowPreviewKey, _showPreview);
            if (_previewHost != null && _previewHost.resolvedStyle.height > 0f)
            {
                EditorPrefs.SetFloat(SplitPxKey, _previewHost.resolvedStyle.height);
            }
        }

        private void SelectionChangedHandler()
        {
            MarkPreviewDirty();
        }

        private void SelectAllClickedHandler()
        {
            _treeController.SelectAll(true);
        }

        private void NoneClickedHandler()
        {
            _treeController.SelectAll(false);
        }

        private void LoadProfileClickedHandler()
        {
            if (!LinkXmlProfileStorage.Load(_entries))
            {
                return;
            }

            _treeController.Rebuild();
            if (_showPreview)
            {
                RebuildPreview();
                return;
            }

            MarkPreviewDirty();
        }

        private void SaveProfileClickedHandler()
        {
            LinkXmlProfileStorage.Save(_entries);
        }

        private void SearchChangedHandler(ChangeEvent<string> evt)
        {
            _treeController.SetSearch(evt.newValue);
        }

        private void PreviewToggleChangedHandler(ChangeEvent<bool> evt)
        {
            _showPreview = evt.newValue;
            ApplyShowPreview();
            if (_showPreview && !_previewPanel.HasContent)
            {
                RebuildPreview();
            }
        }

        private void PreviewGeometryChangedHandler(GeometryChangedEvent evt)
        {
            if (!_showPreview)
            {
                return;
            }

            float height = _previewHost.resolvedStyle.height;
            if (height > 0f)
            {
                EditorPrefs.SetFloat(SplitPxKey, height);
            }
        }
    }
}
