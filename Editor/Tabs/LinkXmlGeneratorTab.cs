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
    internal sealed class LinkXmlGeneratorTab : IGeneratorTab
    {
        private const string UxmlName = "LinkXmlGeneratorWindow";
        private const string USSName = "LinkXmlGeneratorWindow";
        private const string ShowPreviewKey = "LinkXmlGenerator.ShowPreview";
        private const string SplitPxKey = "LinkXmlGenerator.SplitPx";
        private const string Title = "Link XML Generator";
        private const float DefaultPreviewHeight = 220f;
        
        private readonly IPrecompiledTypeResolver _typeResolver = new PrecompiledTypeResolver();

        public string TabLabel => "link.xml";
        public int Order => 0;
        public bool IsAvailable => true;

        private VisualElement _root;
        private List<AssemblyEntry> _entries = new();
        private AssemblyTreeController _treeController;
        private PreviewPanel _previewPanel;
        private TwoPaneSplitView _splitter;
        private VisualElement _previewHost;
        private VisualElement _mergeButtonsHost;
        private IReadOnlyList<ILinkXmlMergeProvider> _mergeProviders;
        private ToolbarToggle _previewToggle;
        private ToolbarButton _generateButton;
        private Label _footerLabel;
        private bool _showPreview;
        private bool _previewDirty;

        public VisualElement CreateView()
        {
            _root = new VisualElement();
            _root.style.flexGrow = 1f;

            VisualTreeAsset tree = Resources.Load<VisualTreeAsset>(UxmlName);
            StyleSheet styles = Resources.Load<StyleSheet>(USSName);

            if (tree == null)
            {
                _root.Add(new Label($"Failed to load UXML at {UxmlName}"));
                return _root;
            }

            tree.CloneTree(_root);

            if (styles != null)
            {
                _root.styleSheets.Add(styles);
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

            return _root;
        }

        private void CacheElements()
        {
            VisualElement root = _root;

            ToolbarButton refreshBtn = root.Q<ToolbarButton>("btn-refresh");
            ToolbarButton selectAllBtn = root.Q<ToolbarButton>("btn-select-all");
            ToolbarButton noneBtn = root.Q<ToolbarButton>("btn-none");
            ToolbarButton loadBtn = root.Q<ToolbarButton>("btn-load");
            ToolbarButton saveBtn = root.Q<ToolbarButton>("btn-save");
            ToolbarButton validateBtn = root.Q<ToolbarButton>("btn-validate");
            ToolbarButton syncBtn = root.Q<ToolbarButton>("btn-sync");
            _mergeButtonsHost = root.Q<VisualElement>("merge-buttons");
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
            validateBtn.clicked += ValidateClickedHandler;
            syncBtn.clicked += SyncClickedHandler;
            _generateButton.clicked += Generate;

            BuildMergeButtons();

            searchField.RegisterValueChangedCallback(SearchChangedHandler);
            _previewToggle.RegisterValueChangedCallback(PreviewToggleChangedHandler);
        }

        private void BuildMergeButtons()
        {
            if (_mergeButtonsHost == null)
            {
                return;
            }

            _mergeButtonsHost.Clear();
            _mergeProviders = LinkXmlMergeProviderRegistry.Discover();

            if (_mergeProviders.Count == 0)
            {
                return;
            }

            if (_mergeProviders.Count == 1)
            {
                ILinkXmlMergeProvider only = _mergeProviders[0];
                ToolbarButton button = new ToolbarButton(() => MergeProviderClickedHandler(only))
                {
                    text = only.ButtonLabel,
                    tooltip = only.Tooltip ?? string.Empty,
                };
                button.AddToClassList("lxg-tb-btn");
                _mergeButtonsHost.Add(button);
                return;
            }

            ToolbarMenu menu = new ToolbarMenu { text = "Merge", tooltip = "Run a single merge provider." };
            menu.AddToClassList("lxg-tb-btn");

            foreach (ILinkXmlMergeProvider provider in _mergeProviders)
            {
                ILinkXmlMergeProvider captured = provider;
                menu.menu.AppendAction(captured.ButtonLabel, _ => MergeProviderClickedHandler(captured));
            }

            _mergeButtonsHost.Add(menu);

            ToolbarButton mergeAll = new ToolbarButton(MergeAllClickedHandler)
            {
                text = "Merge All",
                tooltip = "Run every merge provider in order.",
            };
            mergeAll.AddToClassList("lxg-tb-btn");
            _mergeButtonsHost.Add(mergeAll);
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

        private void ValidateClickedHandler()
        {
            if (!File.Exists(LinkXmlWriter.DefaultPath))
            {
                EditorUtility.DisplayDialog(Title, $"No link.xml found at {LinkXmlWriter.DefaultPath}.", "OK");
                return;
            }

            LinkXmlValidationReport report;

            try
            {
                EditorUtility.DisplayProgressBar(Title, "Validating link.xml...", 0.5f);
                report = LinkXmlValidator.Validate(apply: false);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (!report.Success)
            {
                EditorUtility.DisplayDialog(
                    Title, $"link.xml could not be validated: {report.FailureReason}", "OK");
                return;
            }

            if (!report.Changed)
            {
                string message = "link.xml is valid. No stale entries found.";

                if (report.KeptUnknown.Count > 0)
                {
                    message += $"\n\n{report.KeptUnknown.Count} entries could not be verified and were kept "
                        + "(see Console).";
                }

                EditorUtility.DisplayDialog(Title, message, "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(Title, BuildValidationSummary(report), "Remove", "Cancel"))
            {
                return;
            }

            LinkXmlValidator.Apply(report);
            Refresh();
        }

        private static string BuildValidationSummary(LinkXmlValidationReport report)
        {
            const int maxLines = 12;

            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Validation found stale entries in {report.OutputPath}:");

            int budget = maxLines;
            int omitted = 0;

            if (report.RemovedAssemblies.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine($"Assemblies to remove ({report.RemovedAssemblies.Count}):");

                foreach (string assembly in report.RemovedAssemblies)
                {
                    if (budget <= 0)
                    {
                        omitted++;
                        continue;
                    }

                    builder.AppendLine($"  - {assembly}");
                    budget--;
                }
            }

            if (report.RemovedTypeCount > 0)
            {
                builder.AppendLine();
                builder.AppendLine($"Types to remove ({report.RemovedTypeCount}):");

                foreach (LinkXmlValidationTypeGroup group in report.RemovedTypes)
                {
                    foreach (string type in group.TypeNames)
                    {
                        if (budget <= 0)
                        {
                            omitted++;
                            continue;
                        }

                        builder.AppendLine($"  - {group.AssemblyName}: {type}");
                        budget--;
                    }
                }
            }

            if (omitted > 0)
            {
                builder.AppendLine($"  ... and {omitted} more (see Console).");
            }

            if (report.KeptIgnoreIfMissing.Count > 0 || report.KeptUnknown.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine(
                    $"Kept: {report.KeptIgnoreIfMissing.Count} ignoreIfMissing, "
                    + $"{report.KeptUnknown.Count} unverifiable (see Console).");
            }

            builder.AppendLine();
            builder.Append("Remove these entries?");

            return builder.ToString();
        }

        private void SyncClickedHandler()
        {
            if (!File.Exists(LinkXmlWriter.DefaultPath))
            {
                EditorUtility.DisplayDialog(Title, $"No link.xml found at {LinkXmlWriter.DefaultPath}.", "OK");
                return;
            }

            if (_entries.Count == 0)
            {
                Refresh();
            }

            LinkXmlSyncReport report;

            try
            {
                EditorUtility.DisplayProgressBar(Title, "Syncing link.xml with project code...", 0.5f);
                report = LinkXmlSync.Sync(
                    new ScannedProjectTypeSource(_entries),
                    LinkXmlWriter.DefaultPath,
                    scopePatterns: null,
                    apply: false,
                    throwOnError: false);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (!report.Success)
            {
                EditorUtility.DisplayDialog(
                    Title, $"link.xml could not be synced: {report.FailureReason}", "OK");
                return;
            }

            if (!report.Changed)
            {
                string message = "link.xml already covers every tracked namespace. Nothing to add.";

                if (report.UntrackedAssemblies.Count > 0)
                {
                    message += $"\n\n{report.UntrackedAssemblies.Count} project assemblies are not listed in "
                        + "link.xml at all and were left untouched (see Console).";
                }

                EditorUtility.DisplayDialog(Title, message, "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(Title, BuildSyncSummary(report), "Add", "Cancel"))
            {
                return;
            }

            LinkXmlSync.Apply(report);
            Refresh();
        }

        private static string BuildSyncSummary(LinkXmlSyncReport report)
        {
            const int maxLines = 12;

            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Project code that {report.OutputPath} does not cover yet:");

            int budget = maxLines;
            int omitted = 0;

            if (report.AddedAssemblies.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine($"Assemblies to preserve ({report.AddedAssemblies.Count}):");

                foreach (string assembly in report.AddedAssemblies)
                {
                    if (budget <= 0)
                    {
                        omitted++;
                        continue;
                    }

                    builder.AppendLine($"  + {assembly}");
                    budget--;
                }
            }

            if (report.AddedNamespaceCount > 0)
            {
                builder.AppendLine();
                builder.AppendLine($"Namespaces to preserve ({report.AddedNamespaceCount}):");

                foreach (LinkXmlSyncEntryGroup group in report.AddedNamespaces)
                {
                    foreach (string namespaceName in group.Names)
                    {
                        if (budget <= 0)
                        {
                            omitted++;
                            continue;
                        }

                        builder.AppendLine($"  + {group.AssemblyName}: {namespaceName}.*");
                        budget--;
                    }
                }
            }

            if (report.AddedTypeCount > 0)
            {
                builder.AppendLine();
                builder.AppendLine($"Types to preserve ({report.AddedTypeCount}):");

                foreach (LinkXmlSyncEntryGroup group in report.AddedTypes)
                {
                    foreach (string typeName in group.Names)
                    {
                        if (budget <= 0)
                        {
                            omitted++;
                            continue;
                        }

                        builder.AppendLine($"  + {group.AssemblyName}: {typeName}");
                        budget--;
                    }
                }
            }

            if (omitted > 0)
            {
                builder.AppendLine($"  ... and {omitted} more (see Console).");
            }

            if (report.UntrackedAssemblies.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine(
                    $"{report.UntrackedAssemblies.Count} project assemblies are not listed in link.xml at all "
                    + "and are left untouched (see Console).");
            }

            builder.AppendLine();
            builder.Append("Add these entries? Nothing is removed.");

            return builder.ToString();
        }

        private enum MergeOutcome { Applied, NoContent, Failed }

        private MergeOutcome RunProvider(ILinkXmlMergeProvider provider, out string report)
        {
            report = string.Empty;

            if (provider == null)
            {
                report = "No provider.";
                return MergeOutcome.Failed;
            }

            LinkXmlProviderResult result;

            try
            {
                result = provider.Provide();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LinkXmlGenerator] Merge provider '{provider.Id}' threw: {ex}");
                report = $"Provider '{provider.Id}' failed: {ex.Message}";
                return MergeOutcome.Failed;
            }

            if (result == null)
            {
                report = $"Provider '{provider.Id}' returned no result.";
                return MergeOutcome.Failed;
            }

            LogProviderWarnings(provider, result);
            report = result.Report;

            if (!result.Success)
            {
                return MergeOutcome.Failed;
            }

            if (!result.HasContent)
            {
                return MergeOutcome.NoContent;
            }

            ShowPreview();
            ApplyMergedXmlToTree(result.Xml);

            Debug.Log($"[LinkXmlGenerator] [{provider.Id}] {result.Report}");
            return MergeOutcome.Applied;
        }

        private void MergeProviderClickedHandler(ILinkXmlMergeProvider provider)
        {
            if (provider == null)
            {
                return;
            }

            RunProvider(provider, out string report);
            EditorUtility.DisplayDialog(Title, report, "OK");
            UpdateFooter();
        }

        private void MergeAllClickedHandler()
        {
            if (_mergeProviders == null || _mergeProviders.Count == 0)
            {
                return;
            }

            int applied = 0;
            int skipped = 0;
            int failed = 0;
            StringBuilder details = new StringBuilder();

            foreach (ILinkXmlMergeProvider provider in _mergeProviders)
            {
                MergeOutcome outcome = RunProvider(provider, out string report);

                switch (outcome)
                {
                    case MergeOutcome.Applied: applied++; break;
                    case MergeOutcome.NoContent: skipped++; break;
                    case MergeOutcome.Failed: failed++; break;
                }

                details.AppendLine($"[{provider.Id}] {report}");
            }

            StringBuilder summary = new StringBuilder();
            summary.AppendLine($"Merge All: {applied} applied, {skipped} skipped, {failed} failed.");
            summary.AppendLine();
            summary.Append(details.ToString().TrimEnd());

            EditorUtility.DisplayDialog(Title, summary.ToString(), "OK");
            UpdateFooter();
        }

        private static void LogProviderWarnings(ILinkXmlMergeProvider provider, LinkXmlProviderResult result)
        {
            if (result.Warnings == null)
            {
                return;
            }

            foreach (string warning in result.Warnings)
            {
                Debug.LogWarning($"[LinkXmlGenerator] [{provider.Id}] {warning}");
            }
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

        private void ShowPreview()
        {
            if (_showPreview)
            {
                return;
            }

            _showPreview = true;
            _previewToggle.SetValueWithoutNotify(true);
            EditorPrefs.SetBool(ShowPreviewKey, _showPreview);
            ApplyShowPreview();
        }

        private void ApplyMergedXmlToTree(string xml)
        {
            if (!LinkXmlSelectionImporter.Apply(xml, _entries, _typeResolver))
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

            if (!LinkXmlSelectionImporter.Apply(xml, _entries, _typeResolver))
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

            _footerLabel.text = $"Assemblies: {total}    " +
                $"Selected: {selectedAssemblies} assemblies, {selectedTypes} types    " +
                $"Target: {LinkXmlWriter.DefaultPath}";

            bool needUpdatePreview = _showPreview && _previewDirty && _entries.Count > 0;
            if (needUpdatePreview)
            {
                RebuildPreview();
            }

            _generateButton.SetEnabled(HasAnySelection());
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
            EditorPrefs.SetBool(ShowPreviewKey, _showPreview);
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
