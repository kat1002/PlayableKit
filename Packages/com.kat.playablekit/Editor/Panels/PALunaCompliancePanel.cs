using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PlayableKit.Editor
{
    public class PALunaCompliancePanel : EditorWindow
    {
        Vector2 scroll;
        readonly List<LunaIssue> issues = new List<LunaIssue>();

        [MenuItem("Tools/PlayableKit/Luna Compliance")]
        public static void Open()
        {
            GetWindow<PALunaCompliancePanel>("Luna Compliance");
        }

        void OnGUI()
        {
            GUILayout.Label("LUNA COMPLIANCE TOOL", EditorStyles.boldLabel);

            if (GUILayout.Button("Scan Active Scene", GUILayout.Height(30)))
                Scan();

            GUILayout.Space(10);
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var i in issues)
            {
                GUI.color = i.severity == LunaSeverity.Error ? Color.red : (i.severity == LunaSeverity.Warning ? Color.yellow : Color.green);
                GUILayout.Label(i.severity + ": " + i.message);
                GUI.color = Color.white;

                if (i.ping != null && GUILayout.Button("Ping"))
                    EditorGUIUtility.PingObject(i.ping);
            }
            GUILayout.EndScrollView();
        }

        void Scan()
        {
            issues.Clear();
            ScanSceneSaved();
            ScanSpine();
            ScanShaders();
            ScanDOTween();
            ScanDangerousAPI();
            ScanAndFixURPInProject(false);

            if (issues.Count == 0)
                issues.Add(new LunaIssue(LunaSeverity.Info, "Scene is Luna-safe", null));
        }

        // ================= SCENE =================
        /// <summary>Errors on an unsaved active scene; warns when it is missing from Build Settings.</summary>
        void ScanSceneSaved()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                issues.Add(new LunaIssue(LunaSeverity.Error, "Active scene has never been saved.", null));
                return;
            }

            var buildScenes = EditorBuildSettings.scenes;
            for (int i = 0; i < buildScenes.Length; i++)
            {
                if (buildScenes[i].path == scene.path) return;
            }

            issues.Add(new LunaIssue(LunaSeverity.Warning, "Active scene '" + scene.name + "' is not in Build Settings.", null));
        }

        // ================= SPINE (Decoupled from Spine DLL via SerializedObject) =================
        void ScanSpine()
        {
            var behaviours = Object.FindObjectsOfType<MonoBehaviour>(true);
            foreach (var mb in behaviours)
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (typeName != "SkeletonAnimation" && typeName != "SkeletonGraphic") continue;

                Object data = PASpineSkelScannerPanel.GetSkeletonDataAsset(mb);
                if (data == null) continue;

                SerializedObject so = new SerializedObject(data);
                SerializedProperty jsonProp = so.FindProperty("skeletonJSON");
                if (jsonProp == null || jsonProp.objectReferenceValue == null) continue;

                string path = AssetDatabase.GetAssetPath(jsonProp.objectReferenceValue);
                if (!string.IsNullOrEmpty(path) && (path.EndsWith(".skel") || path.EndsWith(".skel.bytes")))
                {
                    issues.Add(new LunaIssue(
                        LunaSeverity.Error,
                        "Spine Binary (.skel.bytes): " + path,
                        mb.gameObject));
                }
            }
        }

        // ================= SHADER =================
        void ScanShaders()
        {
            var renderers = Object.FindObjectsOfType<Renderer>(true);
            foreach (var r in renderers)
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || m.shader == null) continue;
                    string s = m.shader.name;

                    if (
                        s.Contains("URP") ||
                        s.Contains("Lit") ||
                        s.Contains("Shader Graph")
                    )
                    {
                        issues.Add(new LunaIssue(
                            LunaSeverity.Error,
                            "Unsupported Shader: " + s,
                            r.gameObject));
                    }
                }
            }
        }

        // ================= DOTWEEN =================
        void ScanDOTween()
        {
            string[] blocked =
            {
                "Assets/Plugins/Demigiant/DOTweenPro",
                "Assets/Plugins/Demigiant/DOTween.XML"
            };

            foreach (var p in blocked)
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(p) != null)
                {
                    issues.Add(new LunaIssue(
                        LunaSeverity.Error,
                        "Blocked DOTween asset found: " + p,
                        null));
                }
            }
        }

        // ================= API =================
        void ScanDangerousAPI()
        {
            if (!Directory.Exists("Assets")) return;
            string[] csFiles = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories);
            foreach (var f in csFiles)
            {
                if (f.Replace('\\', '/').Contains("/Editor/")) continue;
                string c = File.ReadAllText(f);
                if (
                    (c.Contains("GetComponent(") && c.Contains("\"")) ||
                    c.Contains("OverlapCollider") ||
                    c.Contains("System.Reflection")
                )
                {
                    issues.Add(new LunaIssue(
                        LunaSeverity.Warning,
                        "Dangerous API in " + f,
                        AssetDatabase.LoadAssetAtPath<Object>(f)));
                }
            }
        }

        // ================= URP GLOBAL STRIP =================
        void ScanAndFixURPInProject(bool autoFix)
        {
            string[] matGuids = AssetDatabase.FindAssets("t:Material");
            foreach (var guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null) continue;

                string shaderName = mat.shader.name;

                if (
                    shaderName.Contains("URP") ||
                    shaderName.Contains("Universal Render Pipeline") ||
                    shaderName.Contains("Shader Graph")
                )
                {
                    issues.Add(new LunaIssue(
                        LunaSeverity.Error,
                        "URP Shader Asset: " + shaderName + " (" + path + ")",
                        mat
                    ));

                    if (!autoFix) continue;

                    if (mat.isVariant)
                    {
                        var newMat = new Material(GetFallbackShaderForMaterial(mat));
                        newMat.name = mat.name + "_LunaSafe";

                        if (mat.HasProperty("_MainTex"))
                            newMat.mainTexture = mat.mainTexture;
                        if (mat.HasProperty("_Color"))
                            newMat.color = mat.color;

                        AssetDatabase.CreateAsset(newMat, path.Replace(".mat", "_LunaSafe.mat"));
                    }
                    else
                    {
                        mat.shader = GetFallbackShaderForMaterial(mat);
                        EditorUtility.SetDirty(mat);
                    }
                }
            }
        }

        Shader GetFallbackShaderForMaterial(Material mat)
        {
            if (mat.mainTexture != null)
                return Shader.Find("Sprites/Default");

            return Shader.Find("Unlit/Texture");
        }
    }

    public enum LunaSeverity { Info, Warning, Error }

    public class LunaIssue
    {
        public LunaSeverity severity;
        public string message;
        public Object ping;

        public LunaIssue(LunaSeverity s, string m, Object p)
        {
            severity = s;
            message = m;
            ping = p;
        }
    }
}
