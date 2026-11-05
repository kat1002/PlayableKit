using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PlayableTemplate.Core.EditorTools
{
    /// <summary>
    /// Find-or-create builder for the Playable template layout. <see cref="Adapt"/> is the single
    /// idempotent pass used both to build a new scene and to bring an existing scene up to the
    /// template: it only adds what is missing, only fills null references, and never moves,
    /// renames, restyles or deletes anything that already exists. A second run returns an empty report.
    /// </summary>
    public static class PlayableTemplateSceneBuilder
    {
        public const string DefaultSceneFolder = "Assets/PlayableTemplate/Scenes";
        public const string DefaultSceneName = "PlayableTemplateBase";
        public const string CtaPrefabPath = "Assets/PlayableTemplate/Prefabs/CtaButton.prefab";
        public const string SeparatorName = "___________";

        private const string UndoName = "Adapt Playable Scene";

        private sealed class Ctx
        {
            public Scene Scene;
            public readonly List<string> Report = new List<string>();
            public readonly HashSet<GameObject> WarnedPrefabs = new HashSet<GameObject>();
        }

        // ---- Menu / public entry points ------------------------------------------------

        [MenuItem("Tools/Playable/Create Template Base Scene")]
        public static void CreateBaseSceneMenu()
        {
            CreateScene(DefaultSceneFolder, DefaultSceneName);
        }

        [MenuItem("Tools/Playable/Adapt Open Scene To Template")]
        public static void AdaptOpenSceneMenu()
        {
            AdaptOpenScene();
        }

        /// <summary>Creates, adapts and saves a new scene. Returns false if cancelled or failed.</summary>
        public static bool CreateScene(string folder, string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[Playable] Scene name is empty.");
                return false;
            }
            sceneName = sceneName.Trim();
            if (sceneName.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                sceneName = sceneName.Substring(0, sceneName.Length - ".unity".Length);
            if (sceneName.Length == 0 || sceneName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                Debug.LogError("[Playable] Invalid scene name: '" + sceneName + "'.");
                return false;
            }

            folder = (folder ?? string.Empty).Trim().Replace('\\', '/').TrimEnd('/');
            if (folder != "Assets" && !folder.StartsWith("Assets/", StringComparison.Ordinal))
            {
                Debug.LogError("[Playable] Folder must be inside the project's Assets folder: '" + folder + "'.");
                return false;
            }

            string scenePath = folder + "/" + sceneName + ".unity";
            if (File.Exists(scenePath) &&
                !EditorUtility.DisplayDialog("Overwrite scene?", scenePath + " already exists. Overwrite it?", "Overwrite", "Cancel"))
                return false;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var report = Adapt(scene);

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            Directory.CreateDirectory(Path.Combine(projectRoot, folder));
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                Debug.LogError("[Playable] Failed to save scene at " + scenePath);
                return false;
            }
            AssetDatabase.Refresh();

            var asset = AssetDatabase.LoadAssetAtPath<Object>(scenePath);
            if (asset != null) EditorGUIUtility.PingObject(asset);
            Debug.Log("[Playable] Template scene created at " + scenePath + "\n" + string.Join("\n", report));
            return true;
        }

        /// <summary>Adapts the active scene inside one Undo group. Returns the report (empty if nothing changed).</summary>
        public static List<string> AdaptOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Playable] No valid active scene to adapt.");
                return new List<string>();
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();

            var report = Adapt(scene);

            Undo.CollapseUndoOperations(group);
            if (report.Count > 0) EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log(report.Count == 0
                ? "[Playable] Scene '" + scene.name + "' already matches template - no changes."
                : "[Playable] Adapted scene '" + scene.name + "':\n" + string.Join("\n", report));
            return report;
        }

        /// <summary>Finds the template canvas without creating anything. Null if the scene has no root canvas.</summary>
        public static Transform FindTemplateCanvas(Scene scene)
        {
            var canvases = new List<Canvas>();
            foreach (var c in FindAllInScene<Canvas>(scene))
                if (IsRootCanvas(c)) canvases.Add(c);
            if (canvases.Count == 0) return null;

            // 1. Already holds PortraitUI/LandscapeUI, or is the parent of a wired core.root.
            var wiredRoots = new List<Transform>();
            foreach (var ui in FindAllInScene<UIControllerBase>(scene))
            {
                var so = new SerializedObject(ui);
                AddRoot(wiredRoots, so, "portraitCore.root");
                AddRoot(wiredRoots, so, "landscapeCore.root");
            }
            foreach (var c in canvases)
            {
                if (c.transform.Find("PortraitUI") != null || c.transform.Find("LandscapeUI") != null)
                    return c.transform;
                foreach (var r in wiredRoots)
                    if (r.parent == c.transform) return c.transform;
            }

            // 2. Scene-root canvas that is not part of a prefab instance.
            foreach (var c in canvases)
                if (c.transform.parent == null && !PrefabUtility.IsPartOfPrefabInstance(c.gameObject))
                    return c.transform;

            // 3. First root canvas.
            return canvases[0].transform;
        }

        /// <summary>The idempotent find-or-create pass. Returns one line per addition/wiring plus warnings.</summary>
        public static List<string> Adapt(Scene scene)
        {
            var c = new Ctx { Scene = scene };
            if (!scene.IsValid() || !scene.isLoaded) return c.Report;

            EnsureCamera(c);
            EnsureLight(c);
            var gm = EnsureManagers(c);
            EnsureNamedRoot(c, "World");
            EnsureNamedRoot(c, "Player");
            EnsureEventSystem(c);

            var canvas = EnsureCanvas(c);
            var ui = EnsureUiController(c, gm);
            var uiSo = new SerializedObject(ui);

            EnsureOrientationUI(c, canvas, "PortraitUI", "portraitCore", uiSo);
            EnsureOrientationUI(c, canvas, "LandscapeUI", "landscapeCore", uiSo);

            int mutes = PlayableUIInstaller.AddMuteButtons(canvas);
            if (mutes > 0) c.Report.Add("Added " + mutes + " mute button(s)");

            EnsureFullscreenCta(c, canvas);

            var loading = PlayableUIInstaller.AddLoadingScreen(canvas);
            if (loading != null) c.Report.Add("Added " + GetPath(loading.transform));

            WireAll(c, canvas, ui, uiSo);
            return c.Report;
        }

        // ---- Scene-level pieces ----------------------------------------------------------

        private static void EnsureCamera(Ctx c)
        {
            if (FindAllInScene<Camera>(c.Scene).Count > 0) return;
            var go = NewRoot(c, "Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 1f, -10f);
        }

        private static void EnsureLight(Ctx c)
        {
            foreach (var l in FindAllInScene<Light>(c.Scene))
                if (l.type == LightType.Directional) return;
            var go = NewRoot(c, "Directional Light", typeof(Light));
            go.GetComponent<Light>().type = LightType.Directional;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static GameManagerBase EnsureManagers(Ctx c)
        {
            Transform separator = null;
            foreach (var root in c.Scene.GetRootGameObjects())
                if (IsSeparatorName(root.name)) { separator = root.transform; break; }
            if (separator == null) separator = NewRoot(c, SeparatorName).transform;

            if (FindAllInScene<LunaManager>(c.Scene).Count == 0)
                NewChild(c, separator, "LunaManager", typeof(LunaManager));

            var gms = FindAllInScene<GameManagerBase>(c.Scene);
            GameManagerBase gm = gms.Count > 0
                ? gms[0]
                : NewChild(c, separator, "GameManager", typeof(GameManagerBase)).GetComponent<GameManagerBase>();

            if (FindAllInScene<AudioManagerBase>(c.Scene).Count == 0)
                NewChild(c, separator, "AudioManager", typeof(AudioManagerBase));
            return gm;
        }

        private static void EnsureNamedRoot(Ctx c, string name)
        {
            foreach (var root in c.Scene.GetRootGameObjects())
                if (root.name == name) return;
            NewRoot(c, name);
        }

        private static void EnsureEventSystem(Ctx c)
        {
            if (FindAllInScene<EventSystem>(c.Scene).Count > 0) return;
            NewRoot(c, "EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static Transform EnsureCanvas(Ctx c)
        {
            var existing = FindTemplateCanvas(c.Scene);
            if (existing != null) return existing;

            var go = NewRoot(c, "Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            return go.transform;
        }

        private static UIControllerBase EnsureUiController(Ctx c, GameManagerBase gm)
        {
            var all = FindAllInScene<UIControllerBase>(c.Scene);
            UIControllerBase chosen = null;

            if (all.Count > 0)
            {
                // 1. Referenced by GameManagerBase.ui.
                foreach (var g in FindAllInScene<GameManagerBase>(c.Scene))
                {
                    var r = new SerializedObject(g).FindProperty("ui")?.objectReferenceValue as UIControllerBase;
                    if (r != null && all.Contains(r)) { chosen = r; break; }
                }
                // 2. One that already has wired refs.
                if (chosen == null)
                {
                    foreach (var u in all)
                    {
                        var so = new SerializedObject(u);
                        if (so.FindProperty("portraitCore.root").objectReferenceValue != null ||
                            so.FindProperty("landscapeCore.root").objectReferenceValue != null ||
                            so.FindProperty("loadingScreen").objectReferenceValue != null)
                        { chosen = u; break; }
                    }
                }
                // 3. First in hierarchy order.
                if (chosen == null) chosen = all[0];

                if (all.Count > 1)
                    c.Report.Add("Warning: " + (all.Count - 1) + " extra UIControllerBase component(s) found; using '" +
                                 GetPath(chosen.transform) + "', extras left untouched.");
                return chosen;
            }

            return NewRoot(c, "UI", typeof(UIControllerBase)).GetComponent<UIControllerBase>();
        }

        // ---- Orientation UI (split from the old BuildOrientationUI) ---------------------------

        private static void EnsureOrientationUI(Ctx c, Transform canvas, string name, string coreField, SerializedObject uiSo)
        {
            var wired = uiSo.FindProperty(coreField + ".root").objectReferenceValue as GameObject;
            Transform root = wired != null ? wired.transform : canvas.Find(name);
            if (root == null)
            {
                root = NewUi(c, canvas, name, null);
                StretchFull((RectTransform)root);
            }

            EnsureHeader(c, root);
            var startInner = EnsureStart(c, root);
            EnsureStartChildren(c, startInner);
            EnsureEndCard(c, root);
        }

        private static void EnsureHeader(Ctx c, Transform root)
        {
            foreach (Transform child in root)
                if (child.name.StartsWith("Header", StringComparison.Ordinal)) return;
            StretchFull((RectTransform)NewUi(c, root, "Header", null));
        }

        /// <summary>Ensures Start/{TapLeft, TapRight, Start}. Returns the inner Start transform.</summary>
        private static Transform EnsureStart(Ctx c, Transform root)
        {
            var startRoot = root.Find("Start");
            if (startRoot == null)
            {
                startRoot = NewUi(c, root, "Start", null);
                StretchFull((RectTransform)startRoot);
            }

            if (startRoot.Find("TapLeft") == null)
            {
                var t = NewUi(c, startRoot, "TapLeft", typeof(Image));
                var rt = (RectTransform)t;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.sizeDelta = Vector2.zero;
                t.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            }

            if (startRoot.Find("TapRight") == null)
            {
                var t = NewUi(c, startRoot, "TapRight", typeof(Image));
                var rt = (RectTransform)t;
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.sizeDelta = Vector2.zero;
                t.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            }

            var inner = startRoot.Find("Start");
            if (inner == null)
            {
                inner = NewUi(c, startRoot, "Start", null);
                var rt = (RectTransform)inner;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(600f, 300f);
            }
            return inner;
        }

        private static void EnsureStartChildren(Ctx c, Transform startInner)
        {
            if (startInner.Find("TutorialHand") == null)
            {
                var t = NewUi(c, startInner, "TutorialHand", typeof(Image));
                var rt = (RectTransform)t;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.6f);
                rt.sizeDelta = new Vector2(120f, 120f);
                t.GetComponent<Image>().color = Color.white;
            }

            if (startInner.Find("Hint") == null)
            {
                var t = NewUi(c, startInner, "Hint", typeof(TextMeshProUGUI));
                var rt = (RectTransform)t;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0.4f);
                rt.sizeDelta = Vector2.zero;
                var text = t.GetComponent<TextMeshProUGUI>();
                text.text = "TAP TO PLAY";
                text.fontSize = 48;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
            }
        }

        private static void EnsureEndCard(Ctx c, Transform root)
        {
            var endCard = root.Find("EndCard");
            if (endCard == null)
            {
                endCard = NewUi(c, root, "EndCard", null);
                StretchFull((RectTransform)endCard);
                endCard.gameObject.SetActive(false);
            }

            var dim = endCard.Find("Dim");
            if (dim == null)
            {
                dim = NewUi(c, endCard, "Dim", typeof(Image), typeof(CtaClickForwarder));
                StretchFull((RectTransform)dim);
                dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            }
            else if (dim.GetComponent<CtaClickForwarder>() == null)
            {
                Undo.AddComponent<CtaClickForwarder>(dim.gameObject);
                c.Report.Add("Added CtaClickForwarder on " + GetPath(dim));
            }

            // CTA: any existing Button under the end card counts, whatever it is named.
            if (endCard.GetComponentInChildren<Button>(true) == null)
                CreateCta(c, endCard);

            if (endCard.Find("Title") == null)
            {
                var t = NewUi(c, endCard, "Title", typeof(TextMeshProUGUI));
                var rt = (RectTransform)t;
                rt.anchorMin = new Vector2(0f, 0.6f);
                rt.anchorMax = new Vector2(1f, 0.9f);
                rt.sizeDelta = Vector2.zero;
                var text = t.GetComponent<TextMeshProUGUI>();
                text.text = "GAME OVER";
                text.fontSize = 72;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
            }
        }

        private static void CreateCta(Ctx c, Transform endCard)
        {
            GameObject ctaGo;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CtaPrefabPath);
            if (prefab != null)
            {
                WarnIfInPrefabInstance(c, endCard, "CTA");
                ctaGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, endCard);
                ctaGo.name = "CTA";
                Undo.RegisterCreatedObjectUndo(ctaGo, UndoName);
                c.Report.Add("Added " + GetPath(ctaGo.transform) + " (CtaButton prefab instance)");
            }
            else
            {
                ctaGo = NewUi(c, endCard, "CTA", typeof(Image), typeof(Button)).gameObject;
                c.Report.Add("Warning: " + CtaPrefabPath + " not found; created a plain button for CTA.");
            }

            var rt = ctaGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.3f);
            if (rt.sizeDelta == Vector2.zero) rt.sizeDelta = new Vector2(800f, 300f);
        }

        private static void EnsureFullscreenCta(Ctx c, Transform canvas)
        {
            if (canvas.Find("FullscreenCTA") != null) return;
            var t = NewUi(c, canvas, "FullscreenCTA", typeof(Image), typeof(CtaClickForwarder));
            t.gameObject.SetActive(false);
            StretchFull((RectTransform)t);
            t.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        }

        // ---- Wiring (null refs only) ----------------------------------------------------

        private static void WireAll(Ctx c, Transform canvas, UIControllerBase ui, SerializedObject uiSo)
        {
            uiSo.Update();
            bool changed = false;
            changed |= WireCore(c, uiSo, "portraitCore", canvas.Find("PortraitUI"));
            changed |= WireCore(c, uiSo, "landscapeCore", canvas.Find("LandscapeUI"));

            var loading = FindAllInScene<LoadingScreenController>(c.Scene);
            if (loading.Count > 0)
                changed |= SetIfNull(c, uiSo.FindProperty("loadingScreen"), loading[0], "UI.loadingScreen");
            if (changed) uiSo.ApplyModifiedProperties();

            foreach (var gm in FindAllInScene<GameManagerBase>(c.Scene))
            {
                var so = new SerializedObject(gm);
                if (SetIfNull(c, so.FindProperty("ui"), ui, gm.name + ".ui"))
                    so.ApplyModifiedProperties();
            }

            foreach (var fwd in FindAllInScene<CtaClickForwarder>(c.Scene))
            {
                var so = new SerializedObject(fwd);
                if (SetIfNull(c, so.FindProperty("ui"), ui, GetPath(fwd.transform) + ".ui"))
                    so.ApplyModifiedProperties();
            }
        }

        private static bool WireCore(Ctx c, SerializedObject uiSo, string field, Transform declaredRoot)
        {
            var rootProp = uiSo.FindProperty(field + ".root");
            bool changed = SetIfNull(c, rootProp, declaredRoot != null ? declaredRoot.gameObject : null, field + ".root");

            var rootGo = rootProp.objectReferenceValue as GameObject;
            if (rootGo == null) return changed;
            var root = rootGo.transform;

            var hand = root.Find("Start/Start/TutorialHand");
            var hint = root.Find("Start/Start/Hint");
            var start = root.Find("Start/Start");
            var end = root.Find("EndCard");

            changed |= SetIfNull(c, uiSo.FindProperty(field + ".hand"), hand != null ? hand.GetComponent<RectTransform>() : null, field + ".hand");
            changed |= SetIfNull(c, uiSo.FindProperty(field + ".hintText"), hint != null ? hint.GetComponent<TextMeshProUGUI>() : null, field + ".hintText");
            changed |= SetIfNull(c, uiSo.FindProperty(field + ".startObj"), start != null ? start.gameObject : null, field + ".startObj");
            changed |= SetIfNull(c, uiSo.FindProperty(field + ".endCard"), end != null ? end.gameObject : null, field + ".endCard");
            return changed;
        }

        private static bool SetIfNull(Ctx c, SerializedProperty prop, Object value, string label)
        {
            if (prop == null || value == null || prop.objectReferenceValue != null) return false;
            prop.objectReferenceValue = value;
            c.Report.Add("Wired " + label);
            return true;
        }

        // ---- Creation helpers -----------------------------------------------------------

        private static GameObject NewRoot(Ctx c, string name, params Type[] comps)
        {
            var go = new GameObject(name, comps);
            if (go.scene != c.Scene) SceneManager.MoveGameObjectToScene(go, c.Scene);
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            c.Report.Add("Added " + name);
            return go;
        }

        private static GameObject NewChild(Ctx c, Transform parent, string name, params Type[] comps)
        {
            var go = new GameObject(name, comps);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            c.Report.Add("Added " + GetPath(go.transform));
            WarnIfInPrefabInstance(c, parent, name);
            return go;
        }

        /// <summary>Creates a UI node (RectTransform + extra components) under parent and reports it.</summary>
        private static Transform NewUi(Ctx c, Transform parent, string name, Type first, params Type[] more)
        {
            var types = new List<Type> { typeof(RectTransform) };
            if (first != null) types.Add(first);
            types.AddRange(more);
            return NewChild(c, parent, name, types.ToArray()).transform;
        }

        private static void WarnIfInPrefabInstance(Ctx c, Transform parent, string childName)
        {
            if (parent == null || !PrefabUtility.IsPartOfPrefabInstance(parent.gameObject)) return;
            var outer = PrefabUtility.GetOutermostPrefabInstanceRoot(parent.gameObject);
            if (outer == null || !c.WarnedPrefabs.Add(outer)) return;
            c.Report.Add("Warning: '" + childName + "' was added inside prefab instance '" + outer.name +
                         "' as an override; it is not applied to the prefab asset.");
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
        }

        // ---- Query helpers --------------------------------------------------------------

        private static List<T> FindAllInScene<T>(Scene scene) where T : Component
        {
            var result = new List<T>();
            foreach (var root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<T>(true));
            return result;
        }

        private static bool IsRootCanvas(Canvas canvas)
        {
            var p = canvas.transform.parent;
            return p == null || p.GetComponentInParent<Canvas>(true) == null;
        }

        private static bool IsSeparatorName(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            foreach (char ch in n) if (ch != '_') return false;
            return true;
        }

        private static void AddRoot(List<Transform> list, SerializedObject so, string path)
        {
            var go = so.FindProperty(path)?.objectReferenceValue as GameObject;
            if (go != null) list.Add(go.transform);
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
