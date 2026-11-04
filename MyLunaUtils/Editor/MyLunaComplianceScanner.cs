using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Spine.Unity;

public static class LunaComplianceScanner
{
    public static List<LunaIssue> ScanActiveScene()
    {
        var issues = new List<LunaIssue>();

        // ===== SPINE =====
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
                    $"Spine binary (.skel.bytes): {path}",
                    a.gameObject
                ));
            }
        }

        // ===== SHADER =====
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
                        $"Unsupported shader: {s}",
                        r.gameObject
                    ));
                }
            }
        }

        return issues;
    }
}
