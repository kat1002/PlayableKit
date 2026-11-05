using UnityEditor;
using UnityEngine;

namespace LunaTools
{
    /// <summary>Tools/Luna menu entries for the luna.json sync bridge. Editor only.</summary>
    public static class LunaAssetsSyncMenu
    {
        const string AutoRefreshKey = "LunaSync.AutoRefresh";
        const string AutoRefreshMenu = "Tools/Luna/Auto Refresh When Disk Changes";

        public static bool AutoRefresh
        {
            get { return EditorPrefs.GetBool(AutoRefreshKey, false); }
        }

        [MenuItem("Tools/Luna/Refresh From Disk", false, 100)]
        static void RefreshFromDisk()
        {
            LunaConfigBridge.Refresh();
        }

        [MenuItem("Tools/Luna/Apply To luna.json", false, 101)]
        static void ApplyToLunaJson()
        {
            LunaConfigBridge.Apply();
        }

        [MenuItem("Tools/Luna/Diagnose Bridge", false, 102)]
        static void DiagnoseBridge()
        {
            Debug.Log("[LunaSync] " + LunaConfigBridge.Diagnose());
        }

        [MenuItem(AutoRefreshMenu, false, 120)]
        static void ToggleAutoRefresh()
        {
            EditorPrefs.SetBool(AutoRefreshKey, !AutoRefresh);
        }

        [MenuItem(AutoRefreshMenu, true)]
        static bool ToggleAutoRefreshValidate()
        {
            Menu.SetChecked(AutoRefreshMenu, AutoRefresh);
            return true;
        }
    }
}
