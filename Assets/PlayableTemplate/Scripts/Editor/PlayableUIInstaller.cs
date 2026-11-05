using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PlayableTemplate.Core.EditorTools
{
    /// <summary>
    /// Editor-time construction helpers for the template's generic UI pieces (mute button,
    /// loading screen). Building these via UI API calls rather than hand-authored scene YAML
    /// guarantees a correctly wired result every time, and lets the same code both build the
    /// fresh template scene and retrofit an existing project's scene that follows the same
    /// Canvas > PortraitUI/LandscapeUI > Header convention (see PlayableTemplateBase.unity).
    /// </summary>
    public static class PlayableUIInstaller
    {
        private static readonly Color CoverColor = new Color(0.05f, 0.05f, 0.05f, 1f);

        [MenuItem("Tools/Playable/Add Mute Button + Loading Screen To Open Scene")]
        public static void AddToOpenScene()
        {
            Transform canvas = PlayableTemplateSceneBuilder.FindTemplateCanvas(SceneManager.GetActiveScene());
            if (canvas == null)
            {
                Debug.LogError("[Playable] No template Canvas found in the open scene.");
                return;
            }

            int muteCount = AddMuteButtons(canvas);
            GameObject loading = AddLoadingScreen(canvas);
            if (muteCount > 0 || loading != null)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Playable] Mute button(s) + loading screen added/verified.");
        }

        public static int AddMuteButtons(Transform canvas)
        {
            int created = 0;
            foreach (Transform ui in canvas)
            {
                if (ui.name != "PortraitUI" && ui.name != "LandscapeUI") continue;
                Transform header = FindChildStartingWith(ui, "Header");
                if (header == null) continue;
                if (header.GetComponentInChildren<MuteToggleButton>(true) != null) continue; // idempotent

                CreateMuteButton(header);
                created++;
            }
            return created;
        }

        public static GameObject AddLoadingScreen(Transform canvas)
        {
            foreach (GameObject root in canvas.gameObject.scene.GetRootGameObjects())
                if (root.GetComponentsInChildren<LoadingScreenController>(true).Length > 0)
                    return null; // idempotent
            return CreateLoadingScreen(canvas);
        }

        private static Transform FindChildStartingWith(Transform parent, string prefix)
        {
            foreach (Transform child in parent)
                if (child.name.StartsWith(prefix)) return child;
            return null;
        }

        public static GameObject CreateMuteButton(Transform parent)
        {
            var go = new GameObject("MuteButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(MuteToggleButton));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(260f, 90f);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -20f);

            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.5f);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            var label = textGo.GetComponent<TextMeshProUGUI>();
            label.text = "SOUND: ON";
            label.fontSize = 32;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var toggle = go.GetComponent<MuteToggleButton>();
            var toggleSo = new SerializedObject(toggle);
            toggleSo.FindProperty("label").objectReferenceValue = label;
            toggleSo.ApplyModifiedProperties();

            UnityEventTools.AddPersistentListener(button.onClick, toggle.OnClick);

            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            return go;
        }

        public static GameObject CreateLoadingScreen(Transform parent)
        {
            var go = new GameObject("LoadingScreen", typeof(RectTransform), typeof(Image), typeof(LoadingScreenController));
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.color = CoverColor;

            var controller = go.GetComponent<LoadingScreenController>();
            var so = new SerializedObject(controller);
            so.FindProperty("coverRoot").objectReferenceValue = go;
            so.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            return go;
        }
    }
}
