using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlayableKit.Editor
{
    public class PASpineSkelScannerPanel : EditorWindow
    {
        class SpineUsage
        {
            public GameObject gameObject;
            public Object skeletonDataAsset;
            public string spineFilePath;
        }

        enum ViewMode
        {
            All,
            SpineFile,
            GameObject
        }

        static readonly List<SpineUsage> usages = new List<SpineUsage>();

        Vector2 scroll;
        ViewMode viewMode = ViewMode.All;

        [MenuItem("Tools/MyLunaHelper/Spine Binary Inspector")]
        static void Open()
        {
            GetWindow<PASpineSkelScannerPanel>("Spine Binary Inspector");
        }

        void OnGUI()
        {
            GUILayout.Space(6);
            GUILayout.Label("Luna Spine Inspector (Scene + Project)", EditorStyles.boldLabel);
            GUILayout.Label("Active Scene: " + SceneManager.GetActiveScene().name);

            GUILayout.Space(6);

            if (GUILayout.Button("Scan Current Scene", GUILayout.Height(30)))
                ScanScene();

            if (GUILayout.Button("Scan Entire Project", GUILayout.Height(30)))
                ScanProject();

            if (usages.Count == 0)
            {
                EditorGUILayout.HelpBox("No Spine .skel detected.", MessageType.Info);
                return;
            }

            GUILayout.Space(8);

            viewMode = (ViewMode)EditorGUILayout.EnumPopup("Mode", viewMode);

            GUILayout.Space(6);

            EditorGUILayout.HelpBox(
                "Primary: " + GetPrimaryCount() + " | Total refs: " + usages.Count,
                MessageType.Warning
            );

            GUILayout.Space(6);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (viewMode == ViewMode.SpineFile)
                RenderBySpineFile();
            else if (viewMode == ViewMode.GameObject)
                RenderByGameObject();
            else
                RenderAll();

            EditorGUILayout.EndScrollView();
        }

        // ================= SCAN (Decoupled from Spine DLL via SerializedObject) =================

        static void ScanScene()
        {
            usages.Clear();

            foreach (var mb in Object.FindObjectsOfType<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (typeName == "SkeletonAnimation" || typeName == "SkeletonGraphic")
                {
                    CollectUsage(mb.gameObject, GetSkeletonDataAsset(mb));
                }
            }
        }

        static void ScanProject()
        {
            usages.Clear();

            string[] guids = AssetDatabase.FindAssets("t:Prefab");

            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                    EditorUtility.DisplayProgressBar(
                        "Scanning Prefabs",
                        path,
                        (float)i / guids.Length
                    );

                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;

                    foreach (var mb in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (mb == null) continue;
                        string typeName = mb.GetType().Name;
                        if (typeName == "SkeletonAnimation" || typeName == "SkeletonGraphic")
                        {
                            CollectUsage(mb.gameObject, GetSkeletonDataAsset(mb));
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log("[Luna] ScanProject DONE");
        }

        public static Object GetSkeletonDataAsset(MonoBehaviour spineComponent)
        {
            if (spineComponent == null) return null;
            SerializedObject so = new SerializedObject(spineComponent);
            SerializedProperty prop = so.FindProperty("skeletonDataAsset");
            if (prop == null) prop = so.FindProperty("m_SkeletonDataAsset");
            return prop != null ? prop.objectReferenceValue : null;
        }

        static void CollectUsage(GameObject go, Object asset)
        {
            if (asset == null) return;

            SerializedObject so = new SerializedObject(asset);
            SerializedProperty jsonProp = so.FindProperty("skeletonJSON");

            if (jsonProp == null || jsonProp.objectReferenceValue == null)
                return;

            string path = AssetDatabase.GetAssetPath(jsonProp.objectReferenceValue);

            if (string.IsNullOrEmpty(path))
                return;

            if (!path.EndsWith(".skel") && !path.EndsWith(".skel.bytes"))
                return;

            usages.Add(new SpineUsage
            {
                gameObject = go,
                skeletonDataAsset = asset,
                spineFilePath = path
            });
        }

        // ================= RENDER =================

        void RenderAll()
        {
            foreach (var u in usages)
            {
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.LabelField(u.spineFilePath, EditorStyles.boldLabel);

                RenderGameObject(u.gameObject);
                RenderPingSpineFile(u.spineFilePath);

                EditorGUILayout.EndVertical();
            }
        }

        void RenderBySpineFile()
        {
            foreach (var group in usages.GroupBy(x => x.spineFilePath))
            {
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.LabelField(group.Key, EditorStyles.boldLabel);

                RenderPingSpineFile(group.Key);

                foreach (var u in group)
                    RenderGameObject(u.gameObject);

                EditorGUILayout.EndVertical();
            }
        }

        void RenderByGameObject()
        {
            foreach (var group in usages.GroupBy(x => x.gameObject))
            {
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.LabelField(group.Key.name, EditorStyles.boldLabel);

                RenderGameObject(group.Key);

                foreach (var u in group)
                    RenderPingSpineFile(u.spineFilePath);

                EditorGUILayout.EndVertical();
            }
        }

        void RenderGameObject(GameObject go)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("• " + go.name);

            if (GUILayout.Button("Ping GO", GUILayout.Width(100)))
            {
                var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(go);

                if (prefabAsset != null)
                {
                    Selection.activeObject = prefabAsset;
                    EditorGUIUtility.PingObject(prefabAsset);
                }
                else
                {
                    Selection.activeGameObject = go;
                    EditorGUIUtility.PingObject(go);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        void RenderPingSpineFile(string path)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("• Spine File");

            Object spineAsset = AssetDatabase.LoadAssetAtPath<Object>(path);

            if (GUILayout.Button("Ping File", GUILayout.Width(100)))
            {
                Selection.activeObject = spineAsset;
                EditorGUIUtility.PingObject(spineAsset);
            }

            EditorGUILayout.EndHorizontal();
        }

        int GetPrimaryCount()
        {
            if (viewMode == ViewMode.GameObject)
                return usages.Select(x => x.gameObject).Distinct().Count();

            return usages.Select(x => x.spineFilePath).Distinct().Count();
        }
    }
}
