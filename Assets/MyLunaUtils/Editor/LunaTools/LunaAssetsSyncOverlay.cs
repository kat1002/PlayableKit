using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LunaTools
{
    /// <summary>
    /// Adds a small Refresh / Apply bar (+ "disk changed" badge) on top of Luna's export window.
    /// The window is IMGUI (OnGUI) hosted in an IMGUIContainer; a later absolutely positioned sibling
    /// in rootVisualElement draws over it. Editor only.
    /// </summary>
    [InitializeOnLoad]
    public static class LunaAssetsSyncOverlay
    {
        const string BarName = "luna-sync-bar";
        const string BadgeName = "luna-sync-badge";
        const double Interval = 1.0;

        static double _next;

        static LunaAssetsSyncOverlay()
        {
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _next) return;
            _next = now + Interval;

            try
            {
                if (!LunaConfigBridge.IsBound) return;
                var type = LunaConfigBridge.LunaWindowType;
                if (type == null) return;

                var windows = Resources.FindObjectsOfTypeAll(type);
                if (windows == null || windows.Length == 0) return;

                bool outOfSync = LunaConfigBridge.IsDiskOutOfSync();
                if (outOfSync && LunaAssetsSyncMenu.AutoRefresh)
                {
                    LunaConfigBridge.Refresh();
                    outOfSync = LunaConfigBridge.IsDiskOutOfSync();
                }

                foreach (var o in windows)
                {
                    var window = o as EditorWindow;
                    if (window == null) continue;
                    var root = window.rootVisualElement;
                    if (root == null) continue;

                    var bar = root.Q(BarName);
                    if (bar == null) bar = AddBar(window, root);

                    var badge = bar.Q<Label>(BadgeName);
                    if (badge != null)
                    {
                        var display = outOfSync ? DisplayStyle.Flex : DisplayStyle.None;
                        if (badge.style.display != display) badge.style.display = display;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[LunaSync] overlay tick failed: " + e.Message);
            }
        }

        static VisualElement AddBar(EditorWindow window, VisualElement root)
        {
            var bar = new VisualElement { name = BarName };
            var s = bar.style;
            s.position = Position.Absolute;
            s.top = 4;
            s.right = 4;
            s.flexDirection = FlexDirection.Row;
            s.alignItems = Align.Center;
            s.paddingLeft = s.paddingRight = 4;
            s.paddingTop = s.paddingBottom = 2;
            s.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = 3;

            var badge = new Label("disk changed") { name = BadgeName };
            badge.style.color = new Color(1f, 0.25f, 0.25f, 1f);
            badge.style.marginRight = 6;
            badge.style.display = DisplayStyle.None;
            bar.Add(badge);

            bar.Add(new Button(() => { LunaConfigBridge.Refresh(); window.Repaint(); }) { text = "Refresh" });
            bar.Add(new Button(() => { LunaConfigBridge.Apply(); window.Repaint(); }) { text = "Apply" });

            root.Add(bar);
            return bar;
        }
    }
}
