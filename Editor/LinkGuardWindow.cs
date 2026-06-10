using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.LinkGuard.Editor
{
    internal sealed class LinkGuardWindow : EditorWindow
    {
        private const string Title = "Link Guard";

        [MenuItem("Window/DTech/Link Guard")]
        public static void Open()
        {
            LinkGuardWindow window = GetWindow<LinkGuardWindow>();
            window.titleContent = new GUIContent(Title);
            window.minSize = new Vector2(640f, 480f);
            window.Show();
        }

        [MenuItem("Window/DTech/Link XML Generator")]
        public static void OpenLegacy()
        {
            Open();
        }

        public void CreateGUI()
        {
            TabView tabView = new TabView();
            tabView.style.flexGrow = 1f;

            foreach (IGeneratorTab tab in GeneratorTabRegistry.Discover())
            {
                if (!tab.IsAvailable)
                {
                    continue;
                }

                Tab element = new Tab(tab.TabLabel);
                VisualElement view = tab.CreateView();
                element.Add(view);
                tabView.Add(element);
                StretchToFill(view, tabView);
            }

            rootVisualElement.Add(tabView);
        }

        private static void StretchToFill(VisualElement from, VisualElement stopAt)
        {
            for (VisualElement current = from; current != null && current != stopAt; current = current.parent)
            {
                current.style.flexGrow = 1f;
            }
        }
    }
}
