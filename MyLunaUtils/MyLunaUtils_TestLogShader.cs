using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class TestLogShader : MonoBehaviour
{
    void Awake()
    {
        Debug.Log($"[LUNA] {GraphicsSettings.renderPipelineAsset}");
        Debug.Log("[LUNA] Shader Guard start");

        // List shader bạn NGHI NGỜ
        string[] suspectShaders =
        {
            "Shader Graphs/",
            "Universal Render Pipeline/",
            "Spine/",
            "Sprites/",
            "Outline",
            "Lit"
        };

        foreach (var name in suspectShaders)
        {
            var s = Shader.Find(name);
            if (s == null)
            {
                Debug.LogError("[LUNA SHADER MISSING] " + name);
            }
            else
            {
                Debug.Log("[LUNA SHADER OK] " + name);
            }
        }
    }
}
