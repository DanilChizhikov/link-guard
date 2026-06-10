using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace DTech.LinkGuard.Editor.ProGuard
{
    internal sealed class ProGuardTreeController
    {
        public Action OnChanged;

        private const string RowClass = "lxg-row";
        private const string GroupLabelClass = "lxg-group-label";
        private const string GlobalLabelClass = "lxg-global-label";
        private const string RowToggleClass = "lxg-row-toggle";
        private const string RowLabelClass = "lxg-row-label";
        private const string RowMetaClass = "lxg-row-meta";
        private const string RowPillClass = "lxg-row-pill";
        private const string HighlightOpen = "<color=#E0A030>";
        private const string HighlightClose = "</color>";
        private const string DefaultPackageLabel = "<i><color=#9090C0>&lt;default package&gt;</color></i>";
        private const string DefaultPackageSearchText = "default package";

        private static readonly EventCallback<ChangeEvent<bool>> _artifactToggleCallback = ArtifactToggleHandler;
        private static readonly EventCallback<ChangeEvent<bool>> _packageToggleCallback = PackageToggleHandler;
        private static readonly EventCallback<ChangeEvent<bool>> _classToggleCallback = ClassToggleHandler;

        private static readonly AndroidArtifactSource[] _groupOrder =
        {
            AndroidArtifactSource.Aar,
            AndroidArtifactSource.AndroidLib,
            AndroidArtifactSource.Jar,
            AndroidArtifactSource.JavaSource,
        };

        private readonly TreeView _tree;
        private readonly VisualElement _emptyHint;

        private List<AndroidArtifactEntry> _entries = new();
        private string _search = string.Empty;
        private int _idCounter;

        public ProGuardTreeController(TreeView tree, VisualElement emptyHint)
        {
            _tree = tree;
            _emptyHint = emptyHint;

            _tree.userData = this;
            _tree.makeItem = MakeItem;
            _tree.bindItem = BindItem;
            _tree.unbindItem = UnbindItem;
            _tree.selectionType = SelectionType.None;
        }

        public void SetEntries(List<AndroidArtifactEntry> entries)
        {
            _entries = entries ?? new List<AndroidArtifactEntry>();
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
            foreach (AndroidArtifactEntry entry in _entries)
            {
                if (value)
                {
                    entry.IsArtifactSelected = true;
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
            _idCounter = 0;
            IList<TreeViewItemData<ProGuardTreeNode>> roots = BuildRoots();
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

        private IList<TreeViewItemData<ProGuardTreeNode>> BuildRoots()
        {
            List<TreeViewItemData<ProGuardTreeNode>> roots = new();

            if (_entries.Count == 0)
            {
                return roots;
            }

            Dictionary<AndroidArtifactSource, List<AndroidArtifactEntry>> groups = _entries
                .GroupBy(e => e.Source)
                .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Name).ToList());

            foreach (AndroidArtifactSource source in _groupOrder)
            {
                if (!groups.TryGetValue(source, out List<AndroidArtifactEntry> bucket))
                {
                    continue;
                }

                List<TreeViewItemData<ProGuardTreeNode>> artifactItems = new();

                foreach (AndroidArtifactEntry entry in bucket)
                {
                    if (!MatchesArtifact(entry))
                    {
                        continue;
                    }

                    List<TreeViewItemData<ProGuardTreeNode>> packageItems = new();

                    foreach (JavaPackageEntry package in entry.Packages)
                    {
                        if (!MatchesPackage(entry, package))
                        {
                            continue;
                        }

                        List<TreeViewItemData<ProGuardTreeNode>> classItems = new();

                        foreach (JavaClassEntry javaClass in package.Classes)
                        {
                            if (!MatchesClass(entry, package, javaClass))
                            {
                                continue;
                            }

                            classItems.Add(new TreeViewItemData<ProGuardTreeNode>(
                                NextId(),
                                ProGuardTreeNode.ForClass(entry, package, javaClass)));
                        }

                        if (classItems.Count == 0)
                        {
                            continue;
                        }

                        packageItems.Add(new TreeViewItemData<ProGuardTreeNode>(
                            NextId(),
                            ProGuardTreeNode.ForPackage(entry, package),
                            classItems));
                    }

                    artifactItems.Add(new TreeViewItemData<ProGuardTreeNode>(
                        NextId(),
                        ProGuardTreeNode.ForArtifact(entry),
                        packageItems));
                }

                if (artifactItems.Count == 0)
                {
                    continue;
                }

                roots.Add(new TreeViewItemData<ProGuardTreeNode>(
                    NextId(),
                    ProGuardTreeNode.ForGroup(source),
                    artifactItems));
            }

            return roots;
        }

        private bool MatchesArtifact(AndroidArtifactEntry entry)
        {
            if (string.IsNullOrEmpty(_search))
            {
                return true;
            }

            return Contains(entry.Name)
                || entry.Packages.Any(p => MatchesPackage(entry, p));
        }

        private bool MatchesPackage(AndroidArtifactEntry entry, JavaPackageEntry package)
        {
            if (string.IsNullOrEmpty(_search) || Contains(entry.Name))
            {
                return true;
            }

            return Contains(GetPackageSearchText(package))
                || package.Classes.Any(c => MatchesClass(entry, package, c));
        }

        private bool MatchesClass(AndroidArtifactEntry entry, JavaPackageEntry package, JavaClassEntry javaClass)
        {
            if (string.IsNullOrEmpty(_search)
                || Contains(entry.Name)
                || Contains(GetPackageSearchText(package)))
            {
                return true;
            }

            return Contains(javaClass.Fullname)
                || Contains(javaClass.DisplayName);
        }

        private bool Contains(string value)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
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

            Label pill = new Label { name = "row-pill", text = "keep" };
            pill.AddToClassList(RowPillClass);
            row.Add(pill);

            return row;
        }

        private void BindItem(VisualElement element, int index)
        {
            ProGuardTreeNode node = _tree.GetItemDataForIndex<ProGuardTreeNode>(index);

            if (node == null)
            {
                return;
            }

            Toggle toggle = element.Q<Toggle>("row-toggle");
            Label label = element.Q<Label>("row-label");
            Label meta = element.Q<Label>("row-meta");
            Label pill = element.Q<Label>("row-pill");

            UnregisterCallbacks(toggle);
            toggle.SetEnabled(true);
            label.RemoveFromClassList(GroupLabelClass);
            label.RemoveFromClassList(GlobalLabelClass);

            switch (node.Kind)
            {
                case ProGuardTreeNodeKind.Group:
                    BindGroup(node, toggle, label, meta, pill);

                    break;

                case ProGuardTreeNodeKind.Artifact:
                    BindArtifact(node, toggle, label, meta, pill);

                    break;

                case ProGuardTreeNodeKind.Package:
                    BindPackage(node, toggle, label, meta, pill);

                    break;

                case ProGuardTreeNodeKind.Class:
                    BindClass(node, toggle, label, meta, pill);

                    break;
            }
        }

        private void UnbindItem(VisualElement element, int index)
        {
            Toggle toggle = element.Q<Toggle>("row-toggle");
            UnregisterCallbacks(toggle);
        }

        private void BindGroup(ProGuardTreeNode node, Toggle toggle, Label label, Label meta, Label pill)
        {
            toggle.style.display = DisplayStyle.None;
            pill.style.display = DisplayStyle.None;
            meta.style.display = DisplayStyle.Flex;

            int total = 0;
            int selected = 0;

            foreach (AndroidArtifactEntry entry in _entries)
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

        private void BindArtifact(ProGuardTreeNode node, Toggle toggle, Label label, Label meta, Label pill)
        {
            AndroidArtifactEntry entry = node.Artifact;

            toggle.style.display = DisplayStyle.Flex;
            toggle.SetValueWithoutNotify(entry.IsArtifactSelected);
            toggle.RegisterValueChangedCallback(_artifactToggleCallback);
            toggle.userData = entry;

            label.text = HighlightMatch(entry.Name);

            meta.style.display = DisplayStyle.Flex;
            meta.text = $"[{entry.SelectedClassCount}/{entry.ClassCount} classes]";

            pill.style.display = entry.IsArtifactSelected ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void BindPackage(ProGuardTreeNode node, Toggle toggle, Label label, Label meta, Label pill)
        {
            JavaPackageEntry package = node.Package;

            toggle.style.display = DisplayStyle.Flex;
            toggle.SetValueWithoutNotify(package.IsSelected);
            toggle.RegisterValueChangedCallback(_packageToggleCallback);
            toggle.userData = package;

            if (string.IsNullOrEmpty(package.Fullname))
            {
                label.AddToClassList(GlobalLabelClass);
                label.text = DefaultPackageLabel;
            }
            else
            {
                label.text = HighlightMatch(package.Fullname);
            }

            meta.style.display = DisplayStyle.Flex;
            meta.text = $"[{package.SelectedClassCount}/{package.Classes.Count} classes]";

            pill.style.display = DisplayStyle.None;
        }

        private void BindClass(ProGuardTreeNode node, Toggle toggle, Label label, Label meta, Label pill)
        {
            JavaClassEntry javaClass = node.Class;

            toggle.style.display = DisplayStyle.Flex;
            toggle.SetValueWithoutNotify(javaClass.IsSelected);
            toggle.RegisterValueChangedCallback(_classToggleCallback);
            toggle.userData = javaClass;

            label.text = HighlightMatch(javaClass.DisplayName);

            meta.style.display = DisplayStyle.None;

            pill.style.display = javaClass.IsSelected ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static void UnregisterCallbacks(Toggle toggle)
        {
            toggle.UnregisterValueChangedCallback(_artifactToggleCallback);
            toggle.UnregisterValueChangedCallback(_packageToggleCallback);
            toggle.UnregisterValueChangedCallback(_classToggleCallback);
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

        private static string GetGroupLabel(AndroidArtifactSource source)
        {
            return source switch
            {
                AndroidArtifactSource.Aar => "AAR plugins",
                AndroidArtifactSource.AndroidLib => "Android libraries",
                AndroidArtifactSource.Jar => "JAR plugins",
                AndroidArtifactSource.JavaSource => "Java/Kotlin sources",
                _ => source.ToString()
            };
        }

        private static string GetPackageSearchText(JavaPackageEntry package)
        {
            return string.IsNullOrEmpty(package.Fullname) ? DefaultPackageSearchText : package.Fullname;
        }

        private static void ArtifactToggleHandler(ChangeEvent<bool> evt)
        {
            if (evt.target is not Toggle toggle || toggle.userData is not AndroidArtifactEntry entry)
            {
                return;
            }

            entry.IsArtifactSelected = evt.newValue;
            FindController(toggle)?.HandleSelectionChanged();
        }

        private static void PackageToggleHandler(ChangeEvent<bool> evt)
        {
            if (evt.target is not Toggle toggle || toggle.userData is not JavaPackageEntry package)
            {
                return;
            }

            package.IsSelected = evt.newValue;
            FindController(toggle)?.HandleSelectionChanged();
        }

        private static void ClassToggleHandler(ChangeEvent<bool> evt)
        {
            if (evt.target is not Toggle toggle || toggle.userData is not JavaClassEntry javaClass)
            {
                return;
            }

            javaClass.SelectAll(evt.newValue);
            FindController(toggle)?.HandleSelectionChanged();
        }

        private static ProGuardTreeController FindController(VisualElement element)
        {
            VisualElement current = element;

            while (current != null)
            {
                if (current.userData is ProGuardTreeController controller)
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
