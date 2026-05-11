using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
            ToolbarButton mergeBtn = root.Q<ToolbarButton>("btn-merge");
            _previewToggle = root.Q<ToolbarToggle>("tgl-preview");
            _generateButton = root.Q<ToolbarButton>("btn-generate");

            ToolbarSearchField searchField = root.Q<ToolbarSearchField>("search-field");

            _splitter = root.Q<TwoPaneSplitView>("splitter");
            _previewHost = root.Q<VisualElement>("preview-host");

            TreeView treeView = root.Q<TreeView>("tree");
            Label emptyHint = root.Q<Label>("empty-hint");

            _footerLabel = root.Q<Label>("footer-label");

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
            mergeBtn.clicked += MergeLinkXmlClickedHandler;
            _generateButton.clicked += Generate;

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
                _entries = AssemblyScanner.Scan((message, progress) =>
                    EditorUtility.DisplayProgressBar(Title, message, progress));
                _treeController.SetEntries(_entries);
                TryLoadCurrentLinkXml();
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

        private void MergeLinkXmlClickedHandler()
        {
            IReadOnlyList<string> paths = LinkXmlMergeScanner.FindLinkXmlFiles();

            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog(Title, "No link.xml files were found in Assets or Packages.", "OK");
                return;
            }

            LinkXmlMergeResult result = LinkXmlMerger.Merge(paths);
            LogSkippedFiles(result);

            if (result.FilesMerged == 0)
            {
                EditorUtility.DisplayDialog(Title, BuildMergeReport(result), "OK");
                return;
            }

            ShowPreview();
            ApplyMergedXmlToTree(result.Xml);

            string report = BuildMergeReport(result);
            Debug.Log($"[LinkXmlGenerator] {report}");
            EditorUtility.DisplayDialog(Title, report, "OK");
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

        private static string BuildMergeReport(LinkXmlMergeResult result)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Files found: {result.FilesFound}");
            builder.AppendLine($"Files merged: {result.FilesMerged}");
            builder.AppendLine($"Skipped invalid files: {result.SkippedFiles.Count}");
            builder.AppendLine($"Duplicate entries collapsed: {result.DuplicatesCollapsed}");
            builder.AppendLine($"Output pending: press Generate link.xml to write {LinkXmlWriter.DefaultPath}");

            if (result.SkippedFiles.Count == 0)
            {
                return builder.ToString();
            }

            builder.AppendLine();
            builder.AppendLine("Skipped files:");

            foreach (LinkXmlMergeSkippedFile skippedFile in result.SkippedFiles)
            {
                builder.AppendLine($"{skippedFile.Path}: {skippedFile.Reason}");
            }

            return builder.ToString();
        }

        private static void LogSkippedFiles(LinkXmlMergeResult result)
        {
            foreach (LinkXmlMergeSkippedFile skippedFile in result.SkippedFiles)
            {
                Debug.LogWarning(
                    $"[LinkXmlGenerator] Skipped link.xml at {skippedFile.Path}: {skippedFile.Reason}");
            }
        }

        private void ShowPreview()
        {
            if (_showPreview)
            {
                return;
            }

            _showPreview = true;
            _previewToggle.SetValueWithoutNotify(true);
            ApplyShowPreview();
        }

        private void ApplyMergedXmlToTree(string xml)
        {
            if (!LinkXmlSelectionImporter.Apply(xml, _entries))
            {
                Debug.LogWarning("[LinkXmlGenerator] Failed to import merged link.xml into the tree.");
                return;
            }

            _treeController.SetEntries(_entries);
            RebuildPreview();
        }

        private bool TryLoadCurrentLinkXml()
        {
            string path = LinkXmlWriter.DefaultPath;

            if (!File.Exists(path))
            {
                return false;
            }

            string xml;

            try
            {
                xml = File.ReadAllText(path);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[LinkXmlGenerator] Failed to read link.xml at {path}: {ex.Message}");
                return false;
            }

            if (!LinkXmlSelectionImporter.Apply(xml, _entries))
            {
                Debug.LogWarning($"[LinkXmlGenerator] Failed to import link.xml at {path} into the tree.");
                return false;
            }

            _treeController.Rebuild();

            return true;
        }

        private void UpdateLoadedProfileState()
        {
            if (_showPreview)
            {
                RebuildPreview();
                return;
            }

            MarkPreviewDirty();
        }

        private void UpdateFooter()
        {
            int total = _entries.Count;
            int selectedAssemblies = _entries.Count(e => e.IsAssemblySelected);
            int selectedTypes = _entries.Sum(e => e.SelectedTypeCount);
            int selectedMethods = _entries.Sum(e => e.SelectedMethodCount);

            _footerLabel.text = $"Assemblies: {total}    " +
                $"Selected: {selectedAssemblies} assemblies, {selectedTypes} types, {selectedMethods} methods    " +
                $"Target: {LinkXmlWriter.DefaultPath}";

            bool needUpdatePreview = _showPreview && _previewDirty && _entries.Count > 0;
            if (needUpdatePreview)
            {
                RebuildPreview();
            }
            
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
            if (!LinkXmlProfileStorage.Load(_entries, out _))
            {
                return;
            }

            LinkXmlPreservation.Clear(_entries);
            _entries.RemoveAll(e => e.Source == AssemblySource.LinkXml);
            _treeController.Rebuild();
            UpdateLoadedProfileState();
        }

        private void SaveProfileClickedHandler()
        {
            LinkXmlProfileStorage.Save(_entries, out _);
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
