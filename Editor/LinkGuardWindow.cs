using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.LinkGuard.Editor
{
    internal sealed class LinkGuardWindow : EditorWindow
    {
        private const string Title = "Link Guard";
        private const string USSName = "LinkXmlGeneratorWindow";
        private const string WindowRootClass = "lxg-window-root";
        private const string TabHeaderClass = "lxg-window-tabs";
        private const string TabButtonClass = "lxg-window-tab";
        private const string SelectedTabButtonClass = "lxg-window-tab-selected";
        private const string ContentHostClass = "lxg-window-content";

        private readonly List<WindowTab> _tabs = new();

        private VisualElement _contentHost;
        private WindowTab _selectedTab;

        [MenuItem("Window/DTech/Link Guard")]
        public static void Open()
        {
            LinkGuardWindow window = GetWindow<LinkGuardWindow>();
            window.titleContent = new GUIContent(Title);
            window.minSize = new Vector2(640f, 480f);
            window.Show();
        }

        public void CreateGUI()
        {
            _tabs.Clear();
            _selectedTab = null;
            rootVisualElement.Clear();
            StretchToFill(rootVisualElement);
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.AddToClassList(WindowRootClass);
            AddStyleSheet();

            VisualElement tabHeader = new VisualElement();
            tabHeader.AddToClassList(TabHeaderClass);
            tabHeader.style.flexDirection = FlexDirection.Row;
            tabHeader.style.flexShrink = 0f;
            rootVisualElement.Add(tabHeader);

            _contentHost = new VisualElement();
            _contentHost.AddToClassList(ContentHostClass);
            StretchToFill(_contentHost);
            _contentHost.style.flexDirection = FlexDirection.Column;
            rootVisualElement.Add(_contentHost);

            foreach (IGeneratorTab tab in GeneratorTabRegistry.Discover())
            {
                if (!tab.IsAvailable)
                {
                    continue;
                }

                VisualElement view = tab.CreateView();
                StretchToFill(view);
                view.style.flexDirection = FlexDirection.Column;

                WindowTab windowTab = new WindowTab(view);
                Button button = new Button(() => SelectTab(windowTab))
                {
                    text = tab.TabLabel
                };
                button.AddToClassList(TabButtonClass);
                windowTab.Button = button;

                _tabs.Add(windowTab);
                tabHeader.Add(button);
            }

            if (_tabs.Count > 0)
            {
                SelectTab(_tabs[0]);
            }
            else
            {
                _contentHost.Add(new Label("No Link Guard tabs available."));
            }
        }

        private void SelectTab(WindowTab tab)
        {
            if (tab == null || tab == _selectedTab)
            {
                return;
            }

            if (_selectedTab != null)
            {
                _selectedTab.Button.RemoveFromClassList(SelectedTabButtonClass);
            }

            _contentHost.Clear();
            StretchToFill(tab.View);
            _contentHost.Add(tab.View);

            tab.Button.AddToClassList(SelectedTabButtonClass);
            _selectedTab = tab;
        }

        private void AddStyleSheet()
        {
            StyleSheet styles = Resources.Load<StyleSheet>(USSName);

            if (styles == null)
            {
                return;
            }

            rootVisualElement.styleSheets.Add(styles);
        }

        private static void StretchToFill(VisualElement element)
        {
            element.style.flexGrow = 1f;
            element.style.flexShrink = 1f;
            element.style.minHeight = 0f;
        }

        private sealed class WindowTab
        {
            public readonly VisualElement View;
            public Button Button;

            public WindowTab(VisualElement view)
            {
                View = view;
            }
        }
    }
}
