using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.Collections.Generic;
using System.IO;
using Spine.Unity;

public class LunaComplianceWindow : EditorWindow
{
    Vector2 scroll;
    List<LunaIssue> issues = new List<LunaIssue>();

    [MenuItem("Tools/MyLunaHelper/Compliance Tool")]
    static void Open()
    {
        GetWindow<LunaComplianceWindow>("Luna Compliance");
    }

    void OnGUI()
    {
        GUILayout.Label("LUNA COMPLIANCE TOOL", EditorStyles.boldLabel);

        if (GUILayout.Button("Scan Active Scene", GUILayout.Height(30)))
            Scan();

        /*
        if (issues.Count > 0)
        {
            if (GUILayout.Button("Auto Fix (Safe Only)", GUILayout.Height(25)))
                AutoFix();
        }
        */

        GUILayout.Space(10);
        scroll = GUILayout.BeginScrollView(scroll);
        foreach (var i in issues)
        {
            GUI.color = i.severity == LunaSeverity.Error ? Color.red : Color.yellow;
            GUILayout.Label($"{i.severity}: {i.message}");
            GUI.color = Color.white;

            if (i.ping != null && GUILayout.Button("Ping"))
                EditorGUIUtility.PingObject(i.ping);
        }
        GUILayout.EndScrollView();
    }

    void Scan()
    {
        issues.Clear();
        ScanSpine();
        ScanShaders();
        ScanDOTween();
        ScanDangerousAPI();
        ScanAndFixURPInProject(false);

        if (issues.Count == 0)
            issues.Add(new LunaIssue(LunaSeverity.Info, "Scene is Luna-safe 🎉", null));
    }

    // ================= SPINE =================
    void ScanSpine()
    {
        var anims = Object.FindObjectsOfType<SkeletonAnimation>(true);
        foreach (var a in anims)
        {
            var data = a.SkeletonDataAsset;
            if (data == null) continue;

            string path = AssetDatabase.GetAssetPath(data);
            if (path.EndsWith(".skel.bytes"))
            {
                issues.Add(new LunaIssue(
                    LunaSeverity.Error,
                    $"Spine Binary (.skel.bytes): {path}",
                    a.gameObject));
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
                        $"Unsupported Shader: {s}",
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
                    $"Blocked DOTween asset found: {p}",
                    null));
            }
        }
    }

    // ================= API =================
    void ScanDangerousAPI()
    {
        string[] csFiles = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories);
        foreach (var f in csFiles)
        {
            string c = File.ReadAllText(f);
            if (
                c.Contains("GetComponent(") && c.Contains("\"") ||
                c.Contains("OverlapCollider") ||
                c.Contains("System.Reflection")
            )
            {
                issues.Add(new LunaIssue(
                    LunaSeverity.Warning,
                    $"Dangerous API in {f}",
                    AssetDatabase.LoadAssetAtPath<Object>(f)));
            }
        }
    }

    // ================= AUTO FIX =================
    void AutoFix()
    {
        foreach (var i in issues)
        {
            if (i.message.Contains("Unsupported Shader") && i.ping is GameObject go)
            {
                var r = go.GetComponent<Renderer>();
                if (r == null) continue;

                var mats = r.sharedMaterials;
                bool changed = false;

                for (int idx = 0; idx < mats.Length; idx++)
                {
                    var mat = mats[idx];
                    if (mat == null || mat.shader == null) continue;

                    string shaderName = mat.shader.name;
                    if (
                        !shaderName.Contains("URP") &&
                        !shaderName.Contains("Lit") &&
                        !shaderName.Contains("Shader Graph")
                    )
                        continue;

                    Shader safeShader = GetSafeShader(go);
                    if (safeShader == null) continue;

                    // 🚫 Material Variant → CLONE
                    if (mat.isVariant)
                    {
                        var newMat = new Material(safeShader);
                        newMat.name = mat.name + "_LunaSafe";

                        if (mat.HasProperty("_MainTex"))
                            newMat.mainTexture = mat.mainTexture;
                        if (mat.HasProperty("_Color"))
                            newMat.color = mat.color;

                        mats[idx] = newMat;
                        changed = true;

                        Debug.Log($"[LUNA FIX] Clone Variant → {newMat.name} ({go.name})");
                    }
                    else
                    {
                        mat.shader = safeShader;
                        changed = true;

                        Debug.Log($"[LUNA FIX] Shader set → {safeShader.name} ({go.name})");
                    }
                }

                if (changed)
                    r.sharedMaterials = mats;
            }

            // ===== DOTWEEN =====
            if (i.message.Contains("DOTween"))
            {
                string path = i.message.Replace("Blocked DOTween asset found: ", "");
                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[LUNA FIX] Deleted DOTween asset: {path}");
            }
        }

        ScanAndFixURPInProject(true);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Scan();
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
                    $"URP Shader Asset: {shaderName} ({path})",
                    mat
                ));

                if (!autoFix) continue;

                // ⚠ Material Variant → clone
                if (mat.isVariant)
                {
                    var newMat = new Material(GetFallbackShaderForMaterial(mat));
                    newMat.name = mat.name + "_LunaSafe";

                    if (mat.HasProperty("_MainTex"))
                        newMat.mainTexture = mat.mainTexture;
                    if (mat.HasProperty("_Color"))
                        newMat.color = mat.color;

                    AssetDatabase.CreateAsset(newMat, path.Replace(".mat", "_LunaSafe.mat"));
                    Debug.Log($"[LUNA FIX] Clone material variant → {newMat.name}");
                }
                else
                {
                    mat.shader = GetFallbackShaderForMaterial(mat);
                    EditorUtility.SetDirty(mat);
                    Debug.Log($"[LUNA FIX] Shader replaced → {mat.shader.name} ({path})");
                }
            }
        }
    }
    Shader GetFallbackShaderForMaterial(Material mat)
    {
        // ưu tiên sprite
        if (mat.mainTexture != null)
            return Shader.Find("Sprites/Default");

        return Shader.Find("Unlit/Texture");
    }


    Shader GetSafeShader(GameObject go)
    {
        // Sprite
        if (go.GetComponent<SpriteRenderer>() != null)
            return Shader.Find("Sprites/Default");

        // UI
        if (go.GetComponent<UnityEngine.UI.Graphic>() != null)
            return Shader.Find("UI/Default");

        // Fallback
        return Shader.Find("Unlit/Texture");
    }

}

// ================= SUPPORT =================
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
