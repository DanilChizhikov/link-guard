using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace DTech.LinkGuard.Editor
{
    public class AssemblyTreeController
    {
        public Action OnChanged;
        
        private const string RowClass = "lxg-row";
        private const string GroupLabelClass = "lxg-group-label";
        private const string GlobalLabelClass = "lxg-global-label";
        private const string RowToggleClass = "lxg-row-toggle";
        private const string RowLabelClass = "lxg-row-label";
        private const string RowMetaClass = "lxg-row-meta";
        private const string RowPillClass = "lxg-row-pill";
        private const string RowIgnoreClass = "lxg-row-ignore-toggle";
        private const string HighlightOpen = "<color=#E0A030>";
        private const string HighlightClose = "</color>";
        private const string GlobalNamespaceLabel = "<i><color=#9090C0>&lt;global namespace&gt;</color></i>";

        private static readonly EventCallback<ChangeEvent<bool>> _assemblyToggleCallback = AssemblyToggleHandler;
        private static readonly EventCallback<ChangeEvent<bool>> _namespaceToggleCallback = NamespaceToggleHandler;
        private static readonly EventCallback<ChangeEvent<bool>> _globalNamespaceToggleCallback = GlobalNamespaceToggleHandler;
        private static readonly EventCallback<ChangeEvent<bool>> _globalTypeToggleCallback = GlobalTypeToggleHandler;
        private static readonly EventCallback<ChangeEvent<bool>> _ignoreToggleCallback = IgnoreToggleHandler;

        private static readonly AssemblySource[] _groupOrder =
        {
            AssemblySource.Project,
            AssemblySource.Plugin,
            AssemblySource.UpmPackage,
            AssemblySource.Sdk,
            AssemblySource.Unity,
        };

        private readonly TreeView _tree;
        private readonly VisualElement _emptyHint;
        
        private List<AssemblyEntry> _entries = new();
        private string _search = string.Empty;
        private int _idCounter;

        public AssemblyTreeController(TreeView tree, VisualElement emptyHint)
        {
            _tree = tree;
            _emptyHint = emptyHint;

            _tree.userData = this;
            _tree.makeItem = MakeItem;
            _tree.bindItem = BindItem;
            _tree.unbindItem = UnbindItem;
            _tree.selectionType = SelectionType.None;
        }

        public void SetEntries(List<AssemblyEntry> entries)
        {
            _entries = entries ?? new List<AssemblyEntry>();
            Rebuild();
        }

        public void SetSearch(string search)
        {
            string normalized = search ?? string.Empty;

            if (string.Equals(_search, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _search = normalized;
            Rebuild();
        }

        public void SelectAll(bool value)
        {
            foreach (AssemblyEntry entry in _entries)
            {
                if (value)
                {
                    entry.IsAssemblySelected = true;
                }
                else
                {
                    entry.SelectAll(false);
                }
            }

            _tree.RefreshItems();
            OnChanged?.Invoke();
        }

        public void Rebuild()
        {
            IList<TreeViewItemData<AssemblyTreeNode>> roots = BuildRoots();
            _idCounter = 0;
            _tree.SetRootItems(roots);
            _tree.Rebuild();

            bool empty = roots.Count == 0;
            _emptyHint.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;
            _tree.style.display = empty ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void RefreshItems()
        {
            _tree.RefreshItems();
        }

        private IList<TreeViewItemData<AssemblyTreeNode>> BuildRoots()
        {
            List<TreeViewItemData<AssemblyTreeNode>> roots = new();

            if (_entries.Count == 0)
            {
                return roots;
            }

            Dictionary<AssemblySource, List<AssemblyEntry>> groups = _entries
                .GroupBy(e => e.Source)
                .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Name).ToList());

            foreach (AssemblySource source in _groupOrder)
            {
                if (!groups.TryGetValue(source, out List<AssemblyEntry> bucket))
                {
                    continue;
                }

                List<TreeViewItemData<AssemblyTreeNode>> assemblyItems = new();

                foreach (AssemblyEntry entry in bucket)
                {
                    if (!MatchesAssembly(entry))
                    {
                        continue;
                    }

                    List<TreeViewItemData<AssemblyTreeNode>> children = new();

                    if (entry.HasGlobalTypes)
                    {
                        List<TreeViewItemData<AssemblyTreeNode>> typeItems = new();

                        foreach (GlobalTypeEntry type in entry.GlobalTypes)
                        {
                            if (!MatchesGlobalType(entry, type))
                            {
                                continue;
                            }

                            typeItems.Add(new TreeViewItemData<AssemblyTreeNode>(
                                NextId(),
                                AssemblyTreeNode.ForGlobalType(entry, type)));
                        }

                        if (typeItems.Count > 0)
                        {
                            children.Add(new TreeViewItemData<AssemblyTreeNode>(
                                NextId(),
                                AssemblyTreeNode.ForGlobalNamespace(entry),
                                typeItems));
                        }
                    }

                    foreach (NamespaceEntry ns in entry.Namespaces)
                    {
                        if (!MatchesNamespace(entry, ns))
                        {
                            continue;
                        }

                        children.Add(new TreeViewItemData<AssemblyTreeNode>(
                            NextId(),
                            AssemblyTreeNode.ForNamespace(entry, ns)));
                    }

                    assemblyItems.Add(new TreeViewItemData<AssemblyTreeNode>(
                        NextId(),
                        AssemblyTreeNode.ForAssembly(entry),
                        children));
                }

                if (assemblyItems.Count == 0)
                {
                    continue;
                }

                roots.Add(new TreeViewItemData<AssemblyTreeNode>(
                    NextId(),
                    AssemblyTreeNode.ForGroup(source),
                    assemblyItems));
            }

            return roots;
        }

        private bool MatchesAssembly(AssemblyEntry entry)
        {
            if (string.IsNullOrEmpty(_search))
            {
                return true;
            }

            if (entry.Name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (entry.Namespaces.Any(ns =>
                    ns.Fullname.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            return entry.GlobalTypes.Any(t =>
                t.Fullname.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private bool MatchesNamespace(AssemblyEntry entry, NamespaceEntry ns)
        {
            if (string.IsNullOrEmpty(_search))
            {
                return true;
            }

            if (entry.Name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return ns.Fullname.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool MatchesGlobalType(AssemblyEntry entry, GlobalTypeEntry type)
        {
            if (string.IsNullOrEmpty(_search))
            {
                return true;
            }

            if (entry.Name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return type.Fullname.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private int NextId()
        {
            return _idCounter++;
        }

        private static VisualElement MakeItem()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList(RowClass);

            Toggle toggle = new Toggle { name = "row-toggle" };
            toggle.AddToClassList(RowToggleClass);
            row.Add(toggle);

            Label label = new Label { name = "row-label", enableRichText = true };
            label.AddToClassList(RowLabelClass);
            row.Add(label);

            Label meta = new Label { name = "row-meta", enableRichText = false };
            meta.AddToClassList(RowMetaClass);
            row.Add(meta);

            Label pill = new Label { name = "row-pill", text = "preserve all" };
            pill.AddToClassList(RowPillClass);
            row.Add(pill);

            Toggle ignoreToggle = new Toggle { name = "row-ignore", text = "ignoreIfMissing" };
            ignoreToggle.AddToClassList(RowIgnoreClass);
            row.Add(ignoreToggle);

            return row;
        }

        private void BindItem(VisualElement element, int index)
        {
            AssemblyTreeNode node = _tree.GetItemDataForIndex<AssemblyTreeNode>(index);

            if (node == null)
            {
                return;
            }

            Toggle toggle = element.Q<Toggle>("row-toggle");
            Label label = element.Q<Label>("row-label");
            Label meta = element.Q<Label>("row-meta");
            Label pill = element.Q<Label>("row-pill");
            Toggle ignoreToggle = element.Q<Toggle>("row-ignore");

            UnregisterCallbacks(toggle, ignoreToggle);
            label.RemoveFromClassList(GroupLabelClass);
            label.RemoveFromClassList(GlobalLabelClass);

            switch (node.Kind)
            {
                case AssemblyTreeNodeKind.Group:
                    BindGroup(node, toggle, label, meta, pill, ignoreToggle);

                    break;

                case AssemblyTreeNodeKind.Assembly:
                    BindAssembly(node, toggle, label, meta, pill, ignoreToggle);

                    break;

                case AssemblyTreeNodeKind.Namespace:
                    BindNamespace(node, toggle, label, meta, pill, ignoreToggle);

                    break;

                case AssemblyTreeNodeKind.GlobalNamespace:
                    BindGlobalNamespace(node, toggle, label, meta, pill, ignoreToggle);

                    break;

                case AssemblyTreeNodeKind.GlobalType:
                    BindGlobalType(node, toggle, label, meta, pill, ignoreToggle);

                    break;
            }
        }

        private void UnbindItem(VisualElement element, int index)
        {
            Toggle toggle = element.Q<Toggle>("row-toggle");
            Toggle ignoreToggle = element.Q<Toggle>("row-ignore");
            UnregisterCallbacks(toggle, ignoreToggle);
        }

        private void BindGroup(AssemblyTreeNode node, Toggle toggle, Label label, Label meta, Label pill,
            Toggle ignoreToggle)
        {
            toggle.style.display = DisplayStyle.None;
            pill.style.display = DisplayStyle.None;
            ignoreToggle.style.display = DisplayStyle.None;
            meta.style.display = DisplayStyle.Flex;

            int total = 0;
            int selected = 0;

            foreach (AssemblyEntry entry in _entries)
            {
                if (entry.Source != node.Group)
                {
                    continue;
                }

                total++;

                if (entry.ProducesEntry)
                {
                    selected++;
                }
            }

            label.text = GetGroupLabel(node.Group);
            label.AddToClassList(GroupLabelClass);
            meta.text = $"({selected}/{total})";
        }

        private void BindAssembly(AssemblyTreeNode node, Toggle toggle, Label label, Label meta, Label pill,
            Toggle ignoreToggle)
        {
            AssemblyEntry entry = node.Assembly;

            toggle.style.display = DisplayStyle.Flex;
            toggle.SetValueWithoutNotify(entry.IsAssemblySelected);
            toggle.RegisterValueChangedCallback(_assemblyToggleCallback);
            toggle.userData = entry;

            label.text = HighlightMatch(entry.Name);

            if (entry.HasNamespaces)
            {
                int selectedNs = entry.Namespaces.Count(n => n.IsSelected);
                meta.style.display = DisplayStyle.Flex;
                meta.text = $"[{selectedNs}/{entry.Namespaces.Count} ns]";
            }
            else
            {
                meta.style.display = DisplayStyle.None;
            }

            pill.style.display = entry.IsAssemblySelected ? DisplayStyle.Flex : DisplayStyle.None;

            ignoreToggle.style.display = DisplayStyle.Flex;
            ignoreToggle.SetValueWithoutNotify(entry.IgnoreIfMissing);
            ignoreToggle.RegisterValueChangedCallback(_ignoreToggleCallback);
            ignoreToggle.userData = entry;
        }

        private void BindNamespace(AssemblyTreeNode node, Toggle toggle, Label label, Label meta, Label pill,
            Toggle ignoreToggle)
        {
            toggle.style.display = DisplayStyle.Flex;
            toggle.SetValueWithoutNotify(node.Namespace.IsSelected);
            toggle.RegisterValueChangedCallback(_namespaceToggleCallback);
            toggle.userData = node.Namespace;

            label.text = HighlightMatch(node.Namespace.Fullname);

            meta.style.display = DisplayStyle.None;
            pill.style.display = DisplayStyle.None;
            ignoreToggle.style.display = DisplayStyle.None;
        }

        private void BindGlobalNamespace(AssemblyTreeNode node, Toggle toggle, Label label, Label meta, Label pill,
            Toggle ignoreToggle)
        {
            AssemblyEntry entry = node.Assembly;

            toggle.style.display = DisplayStyle.Flex;
            toggle.SetValueWithoutNotify(entry.IsAllGlobalTypesSelected);
            toggle.RegisterValueChangedCallback(_globalNamespaceToggleCallback);
            toggle.userData = entry;

            label.AddToClassList(GlobalLabelClass);
            label.text = GlobalNamespaceLabel;

            int selected = entry.GlobalTypes.Count(t => t.IsSelected);
            meta.style.display = DisplayStyle.Flex;
            meta.text = $"[{selected}/{entry.GlobalTypes.Count} types]";

            pill.style.display = DisplayStyle.None;
            ignoreToggle.style.display = DisplayStyle.None;
        }

        private void BindGlobalType(AssemblyTreeNode node, Toggle toggle, Label label, Label meta, Label pill,
            Toggle ignoreToggle)
        {
            toggle.style.display = DisplayStyle.Flex;
            toggle.SetValueWithoutNotify(node.GlobalType.IsSelected);
            toggle.RegisterValueChangedCallback(_globalTypeToggleCallback);
            toggle.userData = node.GlobalType;

            label.text = HighlightMatch(node.GlobalType.Fullname);

            meta.style.display = DisplayStyle.None;
            pill.style.display = DisplayStyle.None;
            ignoreToggle.style.display = DisplayStyle.None;
        }

        private static void UnregisterCallbacks(Toggle toggle, Toggle ignoreToggle)
        {
            toggle.UnregisterValueChangedCallback(_assemblyToggleCallback);
            toggle.UnregisterValueChangedCallback(_namespaceToggleCallback);
            toggle.UnregisterValueChangedCallback(_globalNamespaceToggleCallback);
            toggle.UnregisterValueChangedCallback(_globalTypeToggleCallback);
            ignoreToggle.UnregisterValueChangedCallback(_ignoreToggleCallback);
        }

        private string HighlightMatch(string source)
        {
            if (string.IsNullOrEmpty(_search))
            {
                return source;
            }

            int matchIndex = source.IndexOf(_search, StringComparison.OrdinalIgnoreCase);

            if (matchIndex < 0)
            {
                return source;
            }

            string before = source.Substring(0, matchIndex);
            string match = source.Substring(matchIndex, _search.Length);
            string after = source.Substring(matchIndex + _search.Length);

            return string.Concat(before, HighlightOpen, match, HighlightClose, after);
        }

        private static string GetGroupLabel(AssemblySource source)
        {
            return source switch
            {
                AssemblySource.Project => "Project assemblies",
                AssemblySource.Plugin => "Plugins",
                AssemblySource.UpmPackage => "UPM packages",
                AssemblySource.Sdk => "SDKs",
                AssemblySource.Unity => "Unity modules",
                _ => source.ToString()
            };
        }

        private static void AssemblyToggleHandler(ChangeEvent<bool> evt)
        {
            if (evt.target is not Toggle toggle || toggle.userData is not AssemblyEntry entry)
            {
                return;
            }

            entry.IsAssemblySelected = evt.newValue;
            FindController(toggle)?.HandleSelectionChanged();
        }

        private static void NamespaceToggleHandler(ChangeEvent<bool> evt)
        {
            if (evt.target is not Toggle toggle || toggle.userData is not NamespaceEntry ns)
            {
                return;
            }

            ns.IsSelected = evt.newValue;
            FindController(toggle)?.HandleSelectionChanged();
        }

        private static void GlobalNamespaceToggleHandler(ChangeEvent<bool> evt)
        {
            if (evt.target is not Toggle toggle || toggle.userData is not AssemblyEntry entry)
            {
                return;
            }

            foreach (GlobalTypeEntry type in entry.GlobalTypes)
            {
                type.IsSelected = evt.newValue;
            }

            FindController(toggle)?.HandleSelectionChanged();
        }

        private static void GlobalTypeToggleHandler(ChangeEvent<bool> evt)
        {
            if (evt.target is not Toggle toggle || toggle.userData is not GlobalTypeEntry type)
            {
                return;
            }

            type.IsSelected = evt.newValue;
            FindController(toggle)?.HandleSelectionChanged();
        }

        private static void IgnoreToggleHandler(ChangeEvent<bool> evt)
        {
            if (evt.target is not Toggle toggle || toggle.userData is not AssemblyEntry entry)
            {
                return;
            }

            entry.IgnoreIfMissing = evt.newValue;
            FindController(toggle)?.HandleSelectionChanged();
        }

        private static AssemblyTreeController FindController(VisualElement element)
        {
            VisualElement current = element;

            while (current != null)
            {
                if (current.userData is AssemblyTreeController controller)
                {
                    return controller;
                }

                current = current.parent;
            }

            return null;
        }

        private void HandleSelectionChanged()
        {
            _tree.RefreshItems();
            OnChanged?.Invoke();
        }
    }
}
