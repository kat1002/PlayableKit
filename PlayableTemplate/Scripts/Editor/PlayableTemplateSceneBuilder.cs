using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlayableTemplate.Core.EditorTools
{
    /// <summary>
    /// Builds Assets/_Template/Scenes/PlayableTemplateBase.unity from scratch via the Editor UI
    /// API rather than a hand-authored .unity file — this guarantees a correctly wired result
    /// every time (RectTransform anchors, component references, persistent listeners) and keeps
    /// the scene in sync with GameManagerBase/UIControllerBase/AudioManagerBase whenever this
    /// builder is re-run, instead of a frozen file that can silently drift from the code.
    /// </summary>
    public static class PlayableTemplateSceneBuilder
    {
        private const string ScenePath = "Assets/_Template/Scenes/PlayableTemplateBase.unity";
        private const string CtaPrefabPath = "Assets/_Template/Prefabs/CtaButton.prefab";

        [MenuItem("Tools/Playable/Create Template Base Scene")]
        public static void CreateBaseScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 1f, -10f);

            var lightGo = new GameObject("Directional Light", typeof(Light));
            var light = lightGo.GetComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var separator = new GameObject("___________");
            var lunaGo = new GameObject("LunaManager", typeof(LunaManager));
            lunaGo.transform.SetParent(separator.transform);
            var gmGo = new GameObject("GameManager", typeof(GameManagerBase));
            gmGo.transform.SetParent(separator.transform);
            var audioGo = new GameObject("AudioManager", typeof(AudioManagerBase));
            audioGo.transform.SetParent(separator.transform);

            var world = new GameObject("World");
            var player = new GameObject("Player");

            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);

            var uiGo = new GameObject("UI", typeof(UIControllerBase));
            var uiBase = uiGo.GetComponent<UIControllerBase>();

            BuildOrientationUI(canvasGo.transform, "PortraitUI", uiBase);
            BuildOrientationUI(canvasGo.transform, "LandscapeUI", uiBase);

            var fullscreenCta = new GameObject("FullscreenCTA", typeof(RectTransform), typeof(Image), typeof(CtaClickForwarder));
            fullscreenCta.transform.SetParent(canvasGo.transform, false);
            fullscreenCta.SetActive(false);
            var fsRt = (RectTransform)fullscreenCta.transform;
            fsRt.anchorMin = Vector2.zero;
            fsRt.anchorMax = Vector2.one;
            fsRt.sizeDelta = Vector2.zero;
            var fsImage = fullscreenCta.GetComponent<Image>();
            fsImage.color = new Color(0f, 0f, 0f, 0f);
            fullscreenCta.GetComponent<CtaClickForwarder>().ui = uiBase;

            PlayableUIInstaller.AddMuteButtons(canvasGo.transform);
            PlayableUIInstaller.AddLoadingScreen(canvasGo.transform);

            // Wire UI's Inspector refs to what BuildOrientationUI just created.
            var uiSo = new SerializedObject(uiBase);
            WirePortraitOrLandscape(uiSo, "portraitCore", canvasGo.transform.Find("PortraitUI"));
            WirePortraitOrLandscape(uiSo, "landscapeCore", canvasGo.transform.Find("LandscapeUI"));
            uiSo.FindProperty("loadingScreen").objectReferenceValue = canvasGo.transform.Find("LoadingScreen").GetComponent<LoadingScreenController>();
            uiSo.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Playable] Template base scene created at " + ScenePath);
        }

        private static void WirePortraitOrLandscape(SerializedObject uiSo, string fieldName, Transform root)
        {
            var prop = uiSo.FindProperty(fieldName);
            prop.FindPropertyRelative("root").objectReferenceValue = root.gameObject;
            prop.FindPropertyRelative("hand").objectReferenceValue = root.Find("Start/Start/TutorialHand")?.GetComponent<RectTransform>();
            prop.FindPropertyRelative("hintText").objectReferenceValue = root.Find("Start/Start/Hint")?.GetComponent<TextMeshProUGUI>();
            prop.FindPropertyRelative("startObj").objectReferenceValue = root.Find("Start/Start")?.gameObject;
            prop.FindPropertyRelative("endCard").objectReferenceValue = root.Find("EndCard")?.gameObject;
        }

        private static void BuildOrientationUI(Transform canvas, string name, UIControllerBase ui)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(canvas, false);
            var rootRt = (RectTransform)root.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.sizeDelta = Vector2.zero;

            // Header (mute button attaches here via PlayableUIInstaller)
            var header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(root.transform, false);
            StretchFull((RectTransform)header.transform);

            // Start: TapLeft / TapRight halves + centered hand/hint
            var startRoot = new GameObject("Start", typeof(RectTransform));
            startRoot.transform.SetParent(root.transform, false);
            StretchFull((RectTransform)startRoot.transform);

            var tapLeft = new GameObject("TapLeft", typeof(RectTransform), typeof(Image));
            tapLeft.transform.SetParent(startRoot.transform, false);
            var tapLeftRt = (RectTransform)tapLeft.transform;
            tapLeftRt.anchorMin = new Vector2(0f, 0f);
            tapLeftRt.anchorMax = new Vector2(0.5f, 1f);
            tapLeftRt.sizeDelta = Vector2.zero;
            tapLeft.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            var tapRight = new GameObject("TapRight", typeof(RectTransform), typeof(Image));
            tapRight.transform.SetParent(startRoot.transform, false);
            var tapRightRt = (RectTransform)tapRight.transform;
            tapRightRt.anchorMin = new Vector2(0.5f, 0f);
            tapRightRt.anchorMax = new Vector2(1f, 1f);
            tapRightRt.sizeDelta = Vector2.zero;
            tapRight.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            var startInner = new GameObject("Start", typeof(RectTransform));
            startInner.transform.SetParent(startRoot.transform, false);
            var startInnerRt = (RectTransform)startInner.transform;
            startInnerRt.anchorMin = startInnerRt.anchorMax = new Vector2(0.5f, 0.5f);
            startInnerRt.sizeDelta = new Vector2(600f, 300f);

            var hand = new GameObject("TutorialHand", typeof(RectTransform), typeof(Image));
            hand.transform.SetParent(startInner.transform, false);
            var handRt = (RectTransform)hand.transform;
            handRt.anchorMin = handRt.anchorMax = new Vector2(0.5f, 0.6f);
            handRt.sizeDelta = new Vector2(120f, 120f);
            hand.GetComponent<Image>().color = Color.white;

            var hint = new GameObject("Hint", typeof(RectTransform), typeof(TextMeshProUGUI));
            hint.transform.SetParent(startInner.transform, false);
            var hintRt = (RectTransform)hint.transform;
            hintRt.anchorMin = new Vector2(0f, 0f);
            hintRt.anchorMax = new Vector2(1f, 0.4f);
            hintRt.sizeDelta = Vector2.zero;
            var hintText = hint.GetComponent<TextMeshProUGUI>();
            hintText.text = "TAP TO PLAY";
            hintText.fontSize = 48;
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.color = Color.white;

            // EndCard: Dim + CTA + Title, inactive by default
            var endCard = new GameObject("EndCard", typeof(RectTransform));
            endCard.transform.SetParent(root.transform, false);
            StretchFull((RectTransform)endCard.transform);
            endCard.SetActive(false);

            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(CtaClickForwarder));
            dim.transform.SetParent(endCard.transform, false);
            StretchFull((RectTransform)dim.transform);
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            dim.GetComponent<CtaClickForwarder>().ui = ui;

            GameObject ctaGo = null;
            var ctaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CtaPrefabPath);
            if (ctaPrefab != null)
            {
                ctaGo = (GameObject)PrefabUtility.InstantiatePrefab(ctaPrefab, endCard.transform);
            }
            else
            {
                ctaGo = new GameObject("CTA", typeof(RectTransform), typeof(Image), typeof(Button));
                ctaGo.transform.SetParent(endCard.transform, false);
            }
            var ctaRt = ctaGo.GetComponent<RectTransform>();
            ctaRt.anchorMin = ctaRt.anchorMax = new Vector2(0.5f, 0.3f);
            if (ctaRt.sizeDelta == Vector2.zero) ctaRt.sizeDelta = new Vector2(800f, 300f);

            var title = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            title.transform.SetParent(endCard.transform, false);
            var titleRt = (RectTransform)title.transform;
            titleRt.anchorMin = new Vector2(0f, 0.6f);
            titleRt.anchorMax = new Vector2(1f, 0.9f);
            titleRt.sizeDelta = Vector2.zero;
            var titleText = title.GetComponent<TextMeshProUGUI>();
            titleText.text = "GAME OVER";
            titleText.fontSize = 72;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
        }
    }
}
