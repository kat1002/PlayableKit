#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PlayableKit.Editor
{
    /// <summary>
    /// Unified Editor window for PlayableKit combining:
    /// 1. Runtime Dependencies & Code Scanner
    /// 2. Luna Compliance & Spine .skel Scanner
    /// 3. UI Sanitizer (Clean DrivenRectTransformTracker for Luna)
    /// 4. Kit Utilities & Standalone Package Exporter
    /// </summary>
    public sealed class PAPlayableKitWindow : EditorWindow
    {
        private enum Tab
        {
            CodeAndDependencies = 0,
            LunaCompliance = 1,
            UiSanitizer = 2,
            UtilitiesAndExport = 3
        }

        private static readonly string[] TabLabels = new[]
        {
            "Dependencies & Scanner",
            "Luna Compliance",
            "UI Sanitizer",
            "Utilities & Export"
        };

        private Tab _currentTab = Tab.CodeAndDependencies;
        private Vector2 _utilScroll;
        private string _findAssetId = "";

        [MenuItem("Tools/PlayableKit/PlayableKit Control Center %#k", priority = 0)]
        public static void Open()
        {
            var win = GetWindow<PAPlayableKitWindow>("PlayableKit");
            win.minSize = new Vector2(700f, 500f);
            win.Show();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawTabBar();

            EditorGUILayout.Space(8f);

            switch (_currentTab)
            {
                case Tab.CodeAndDependencies:
                    DrawCodeAndDependenciesTab();
                    break;
                case Tab.LunaCompliance:
                    DrawLunaComplianceTab();
                    break;
                case Tab.UiSanitizer:
                    DrawUiSanitizerTab();
                    break;
                case Tab.UtilitiesAndExport:
                    DrawUtilitiesAndExportTab();
                    break;
            }
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("PlayableKit Ad Toolkit", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Unified control center for verification, Luna sanitization, and package exporting.", EditorStyles.miniLabel);
            }
        }

        private void DrawTabBar()
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < TabLabels.Length; i++)
                {
                    bool isSelected = (int)_currentTab == i;
                    GUI.backgroundColor = isSelected ? new Color(0.2f, 0.75f, 0.3f) : Color.white;
                    if (GUILayout.Button(TabLabels[i], GUILayout.Height(28)))
                    {
                        _currentTab = (Tab)i;
                        GUI.FocusControl(null);
                    }
                }
                GUI.backgroundColor = Color.white;
            }
        }

        private void DrawCodeAndDependenciesTab()
        {
            EditorGUILayout.HelpBox(
                "Deep runtime dependency scanner: traces textures, audio, missing components, " +
                "and forbidden code patterns across the active scene and referenced prefabs.",
                MessageType.Info);

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Open Dependency & Code Scanner Window", GUILayout.Height(36)))
            {
                PADependencyScannerPanel.ShowWindow();
            }
        }

        private void DrawLunaComplianceTab()
        {
            EditorGUILayout.HelpBox(
                "Luna compliance scanner: scans active scene and project for Spine binary (.skel.bytes), " +
                "unsupported URP shaders, DOTween DLL presence, and forbidden runtime APIs." +
                " Build Selector prepares luna.json for the open scene (scene, creative name, script excludes).",
                MessageType.Info);

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Luna Compliance Panel", GUILayout.Height(36)))
                {
                    PALunaCompliancePanel.Open();
                }

                if (GUILayout.Button("Open Spine .skel Binary Inspector", GUILayout.Height(36)))
                {
                    PASpineSkelScannerPanel.Open();
                }

                if (GUILayout.Button("Open Build Selector", GUILayout.Height(36)))
                {
                    PABuildSelectorWindow.ShowWindow();
                }
            }
        }

        private void DrawUiSanitizerTab()
        {
            EditorGUILayout.HelpBox(
                "Sanitizes UI for Luna by stripping components using DrivenRectTransformTracker " +
                "(LayoutGroups, ContentSizeFitter, AspectRatioFitter, ScrollRect) and baking rects / swapping " +
                "to Luna-safe PAAspectFitter & PADragScroll.",
                MessageType.Info);

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Sanitize Active Scene UI For Luna (PALunaUiSanitizer)", GUILayout.Height(36)))
            {
                PALunaUiSanitizer.Sanitize();
            }
        }

        private void DrawUtilitiesAndExportTab()
        {
            _utilScroll = EditorGUILayout.BeginScrollView(_utilScroll);

            // AssetBundles cleaner
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("AssetBundles Tag Cleaner", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Clears all AssetBundle tags across project files to prevent Luna build failure.", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(4f);
                if (GUILayout.Button("Force Remove All AssetBundle Tags", GUILayout.Height(28)))
                {
                    RemoveAllAssetBundles();
                }
            }

            EditorGUILayout.Space(10f);

            // Find Asset By ID
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Find Asset By Instance ID", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Look up and ping an asset in the Project / Hierarchy from an Instance ID reported in Luna / Unity logs.", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(4f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    _findAssetId = EditorGUILayout.TextField("Instance ID:", _findAssetId);
                    if (GUILayout.Button("Find & Ping", GUILayout.Width(100)))
                    {
                        FindAndPingAsset(_findAssetId);
                    }
                }
            }

            EditorGUILayout.Space(10f);

            // Video tools (FFmpeg)
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Video Tools (FFmpeg)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Encode videos into small browser-safe H.264 under a size budget, and bake blurred backdrop stills. Needs FFmpeg + FFprobe.", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(4f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Video Optimizer", GUILayout.Height(28))) Media.PAVideoOptimizerWindow.Open();
                    if (GUILayout.Button("Backdrop Capture", GUILayout.Height(28))) Media.PABackdropCaptureWindow.Open();
                }
            }

            EditorGUILayout.Space(10f);

            // Package Exporter
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Export Standalone PlayableKit Package", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Exports Packages/com.kat.playablekit as PlayableKit.unitypackage (TextMeshPro and Luna are not included).", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(4f);
                if (GUILayout.Button("Export PlayableKit.unitypackage...", GUILayout.Height(32)))
                {
                    PAPackageExporter.ExportPackageInteractive();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        public static void RemoveAllAssetBundles()
        {
            string[] assetBundleNames = AssetDatabase.GetAllAssetBundleNames();
            if (assetBundleNames.Length == 0)
            {
                EditorUtility.DisplayDialog("Notice", "Project is clean! No AssetBundle tags found.", "OK");
                return;
            }

            bool confirm = EditorUtility.DisplayDialog(
                "Remove AssetBundle Tags",
                $"Found {assetBundleNames.Length} asset bundle name(s) in project.\n\nRemove all tags to prevent Luna build issues?",
                "Remove All",
                "Cancel");

            if (!confirm) return;

            int count = 0;
            foreach (var bundleName in assetBundleNames)
            {
                string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);
                foreach (var assetPath in assetPaths)
                {
                    AssetImporter importer = AssetImporter.GetAtPath(assetPath);
                    if (importer != null)
                    {
                        importer.assetBundleName = "";
                        importer.assetBundleVariant = "";
                        count++;
                    }
                }
                AssetDatabase.RemoveAssetBundleName(bundleName, true);
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Complete", $"Cleared AssetBundle tags from {count} file(s).", "OK");
        }

        private static void FindAndPingAsset(string rawId)
        {
            if (int.TryParse(rawId, out int id))
            {
                Object obj = EditorUtility.InstanceIDToObject(id);
                if (obj != null)
                {
                    Selection.activeObject = obj;
                    EditorGUIUtility.PingObject(obj);
                    Debug.Log("[PlayableKit] Found Asset: " + obj.name, obj);
                }
                else
                {
                    Debug.LogError("[PlayableKit] No asset found with instance ID: " + id);
                }
            }
            else
            {
                Debug.LogWarning("[PlayableKit] Please enter a valid numeric instance ID.");
            }
        }
    }
}
#endif
