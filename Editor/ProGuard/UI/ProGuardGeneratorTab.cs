using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.LinkGuard.Editor.ProGuard
{
    internal sealed class ProGuardGeneratorTab : IGeneratorTab
    {
        private const string Title = "ProGuard Generator";
        private const string USSName = "LinkXmlGeneratorWindow";
        private const string ShowPreviewKey = "ProGuardGenerator.ShowPreview";
        private const string BaseRulesExpandedKey = "ProGuardGenerator.BaseRulesExpanded";

        public string TabLabel => "ProGuard";
        public int Order => 1;
        public bool IsAvailable => true;

        private VisualElement _root;
        private List<AndroidArtifactEntry> _entries = new();
        private ProGuardTreeController _treeController;
        private ProGuardPreviewPanel _previewPanel;
        private TwoPaneSplitView _splitter;
        private VisualElement _previewHost;
        private ToolbarToggle _previewToggle;
        private ToolbarButton _generateButton;
        private TextField _baseRulesField;
        private Label _noticeLabel;
        private Label _footerLabel;
        private bool _showPreview;
        private bool _subscribed;

        public VisualElement CreateView()
        {
            _root = new VisualElement();
            _root.style.flexGrow = 1f;
            _root.AddToClassList("lxg-root");

            StyleSheet styles = Resources.Load<StyleSheet>(USSName);
            if (styles != null)
            {
                _root.styleSheets.Add(styles);
            }

            BuildToolbar();
            BuildNotice();
            BuildBaseRules();
            BuildSearch();
            BuildContent();
            BuildFooter();

            _showPreview = EditorPrefs.GetBool(ShowPreviewKey, false);
            _previewToggle.SetValueWithoutNotify(_showPreview);
            ApplyShowPreview();

            _root.RegisterCallback<AttachToPanelEvent>(OnAttach);
            _root.RegisterCallback<DetachFromPanelEvent>(OnDetach);

            UpdatePlatformState();
            UpdateFooter();

            return _root;
        }

        private void BuildToolbar()
        {
            Toolbar toolbar = new Toolbar();

            toolbar.Add(MakeButton("Refresh", Refresh));
            toolbar.Add(MakeButton("Select All", () => _treeController.SelectAll(true)));
            toolbar.Add(MakeButton("None", () => _treeController.SelectAll(false)));

            VisualElement spacer = new VisualElement();
            spacer.AddToClassList("lxg-tb-spacer");
            toolbar.Add(spacer);

            toolbar.Add(MakeButton("Load Profile", LoadProfileClickedHandler));
            toolbar.Add(MakeButton("Save Profile", SaveProfileClickedHandler));

            VisualElement flex = new VisualElement();
            flex.AddToClassList("lxg-tb-flex");
            toolbar.Add(flex);

            _previewToggle = new ToolbarToggle { text = "Preview" };
            _previewToggle.AddToClassList("lxg-tb-btn");
            _previewToggle.RegisterValueChangedCallback(PreviewToggleChangedHandler);
            toolbar.Add(_previewToggle);

            _generateButton = new ToolbarButton(Generate) { text = "Generate ProGuard" };
            _generateButton.AddToClassList("lxg-tb-btn");
            _generateButton.AddToClassList("lxg-generate");
            toolbar.Add(_generateButton);

            _root.Add(toolbar);
        }

        private static ToolbarButton MakeButton(string text, System.Action onClick)
        {
            ToolbarButton button = new ToolbarButton(onClick) { text = text };
            button.AddToClassList("lxg-tb-btn");
            return button;
        }

        private void BuildNotice()
        {
            _noticeLabel = new Label();
            _noticeLabel.style.display = DisplayStyle.None;
            _noticeLabel.style.whiteSpace = WhiteSpace.Normal;
            _noticeLabel.style.paddingLeft = 6f;
            _noticeLabel.style.paddingRight = 6f;
            _noticeLabel.style.paddingTop = 3f;
            _noticeLabel.style.paddingBottom = 3f;
            _noticeLabel.style.color = new Color(0.9f, 0.7f, 0.3f);
            _root.Add(_noticeLabel);
        }

        private void BuildBaseRules()
        {
            Foldout foldout = new Foldout
            {
                text = "Base ProGuard rules (always added)",
                value = EditorPrefs.GetBool(BaseRulesExpandedKey, false),
            };
            foldout.RegisterValueChangedCallback(evt =>
                EditorPrefs.SetBool(BaseRulesExpandedKey, evt.newValue));

            Label hint = new Label("Appended verbatim to proguard-user.txt on every generation.");
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.opacity = 0.7f;
            hint.style.marginBottom = 4f;
            foldout.Add(hint);

            _baseRulesField = new TextField { multiline = true, value = ProGuardBaseRulesStore.Load() };
            _baseRulesField.style.minHeight = 80f;
            _baseRulesField.style.whiteSpace = WhiteSpace.Normal;
            _baseRulesField.RegisterValueChangedCallback(BaseRulesChangedHandler);
            foldout.Add(_baseRulesField);

            _root.Add(foldout);
        }

        private void BaseRulesChangedHandler(ChangeEvent<string> evt)
        {
            ProGuardBaseRulesStore.Save(evt.newValue);

            if (_showPreview)
            {
                RebuildPreview();
            }
        }

        private string GetBaseRules()
        {
            return _baseRulesField != null ? _baseRulesField.value : string.Empty;
        }

        private void BuildSearch()
        {
            Toolbar searchBar = new Toolbar();
            searchBar.AddToClassList("lxg-search-bar");

            ToolbarSearchField searchField = new ToolbarSearchField();
            searchField.AddToClassList("lxg-search-field");
            searchField.RegisterValueChangedCallback(evt => _treeController.SetSearch(evt.newValue));
            searchBar.Add(searchField);

            _root.Add(searchBar);
        }

        private void BuildContent()
        {
            VisualElement content = new VisualElement();
            content.AddToClassList("lxg-content");

            _splitter = new TwoPaneSplitView(1, 220f, TwoPaneSplitViewOrientation.Vertical);
            _splitter.AddToClassList("lxg-splitter");

            VisualElement treeHost = new VisualElement();
            treeHost.AddToClassList("lxg-tree-host");

            TreeView tree = new TreeView { fixedItemHeight = 20 };
            tree.AddToClassList("lxg-tree");

            Label emptyHint = new Label("No Android artifacts. Press Refresh.");
            emptyHint.AddToClassList("lxg-empty-hint");

            treeHost.Add(tree);
            treeHost.Add(emptyHint);

            _previewHost = new VisualElement();
            _previewHost.AddToClassList("lxg-preview-host");

            _splitter.Add(treeHost);
            _splitter.Add(_previewHost);

            content.Add(_splitter);
            _root.Add(content);

            _treeController = new ProGuardTreeController(tree, emptyHint)
            {
                OnChanged = SelectionChangedHandler
            };

            _previewPanel = new ProGuardPreviewPanel(_previewHost);
        }

        private void BuildFooter()
        {
            VisualElement footer = new VisualElement();
            footer.AddToClassList("lxg-footer");

            _footerLabel = new Label();
            _footerLabel.AddToClassList("lxg-footer-label");
            footer.Add(_footerLabel);

            _root.Add(footer);
        }

        private void Refresh()
        {
            try
            {
                _entries = AndroidArtifactScanner.Scan((message, progress) =>
                    EditorUtility.DisplayProgressBar(Title, message, progress));
                _treeController.SetEntries(_entries);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (_showPreview)
            {
                RebuildPreview();
            }

            UpdateFooter();
        }

        private void Generate()
        {
            if (!ProGuardBuildSettings.IsAndroidTarget())
            {
                EditorUtility.DisplayDialog(Title,
                    "Active build target is not Android. Switch platform to Android before generating ProGuard rules.",
                    "OK");
                return;
            }

            if (!HasAnySelection())
            {
                EditorUtility.DisplayDialog(Title, "Nothing is selected.", "OK");
                return;
            }

            string text = ProGuardRulesBuilder.Build(_entries, GetBaseRules());
            _previewPanel.SetText(text);

            ProGuardWriter.WriteWithConfirmation(text);
            UpdatePlatformState();
            UpdateFooter();
        }

        private void RebuildPreview()
        {
            string text = ProGuardRulesBuilder.Build(_entries, GetBaseRules());
            _previewPanel.SetText(text);
        }

        private bool HasAnySelection()
        {
            return _entries.Any(e => e.ProducesEntry);
        }

        private void ApplyShowPreview()
        {
            if (_showPreview)
            {
                _splitter.UnCollapse();
            }
            else
            {
                _splitter.CollapseChild(1);
            }
        }

        private void SelectionChangedHandler()
        {
            if (_showPreview)
            {
                RebuildPreview();
            }

            UpdateFooter();
        }

        private void PreviewToggleChangedHandler(ChangeEvent<bool> evt)
        {
            _showPreview = evt.newValue;
            EditorPrefs.SetBool(ShowPreviewKey, _showPreview);
            ApplyShowPreview();

            if (_showPreview && !_previewPanel.HasContent)
            {
                RebuildPreview();
            }
        }

        private void LoadProfileClickedHandler()
        {
            if (!ProGuardProfileStorage.Load(_entries, out _))
            {
                return;
            }

            _treeController.Rebuild();

            if (_showPreview)
            {
                RebuildPreview();
            }

            UpdateFooter();
        }

        private void SaveProfileClickedHandler()
        {
            ProGuardProfileStorage.Save(_entries, out _);
        }

        private void UpdateFooter()
        {
            int total = _entries.Count;
            int selectedArtifacts = _entries.Count(e => e.IsArtifactSelected);
            int selectedClasses = _entries.Sum(e => e.SelectedClassCount);

            _footerLabel.text = $"Artifacts: {total}    " +
                $"Selected: {selectedArtifacts} artifacts, {selectedClasses} classes    " +
                $"Target: {ProGuardWriter.DefaultPath}";

            _generateButton.SetEnabled(HasAnySelection());
        }

        private void OnAttach(AttachToPanelEvent evt)
        {
            if (_subscribed)
            {
                return;
            }

            EditorUserBuildSettings.activeBuildTargetChanged += OnBuildTargetChanged;
            _subscribed = true;
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            if (!_subscribed)
            {
                return;
            }

            EditorUserBuildSettings.activeBuildTargetChanged -= OnBuildTargetChanged;
            _subscribed = false;
        }

        private void OnBuildTargetChanged()
        {
            UpdatePlatformState();
        }

        private void UpdatePlatformState()
        {
            bool android = ProGuardBuildSettings.IsAndroidTarget();
            _generateButton.style.display = android ? DisplayStyle.Flex : DisplayStyle.None;

            if (!android)
            {
                ShowNotice("Active build target is not Android. Switch platform to Android to generate ProGuard rules.");
                return;
            }

            if (!ProGuardBuildSettings.IsMinifyEnabled())
            {
                ShowNotice("Android minification (R8/ProGuard) is disabled in Player Settings. " +
                    "Rules will be generated but won't be applied until you enable Minify.");
                return;
            }

            _noticeLabel.style.display = DisplayStyle.None;
        }

        private void ShowNotice(string message)
        {
            _noticeLabel.text = message;
            _noticeLabel.style.display = DisplayStyle.Flex;
        }
    }
}
