using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PlayableTemplate.Core.EditorTools
{
    /// <summary>
    /// Shared "prepare for Luna" logic: pins the configured shader as always-included and
    /// registers the resolved scene in Build Settings. The scene path is never hardcoded —
    /// it comes from PlayableExportSettings.sceneOverridePath, or failing that, the currently
    /// active/open scene, so a project copied from the template needs zero code edits here.
    /// </summary>
    public static class PlayableExportTools
    {
        public static void PrepareForLuna()
        {
            var settings = PlayableExportSettings.GetOrCreate();

            string scenePath = !string.IsNullOrEmpty(settings.sceneOverridePath)
                ? settings.sceneOverridePath
                : EditorSceneManager.GetActiveScene().path;

            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogError("[Playable] No scene path to register — save the active scene first, " +
                                "or set PlayableExportSettings.sceneOverridePath.");
                return;
            }

            EnsureAlwaysIncludedShader(settings.alwaysIncludedShaderName);
            RegisterSceneInBuildSettings(scenePath);
            Debug.Log("[Playable] " + settings.alwaysIncludedShaderName + " pinned as an always-included shader; " +
                      scenePath + " registered in Build Settings.\n" +
                      "Next: Window > Unity Playworks > add this scene to the Playworks scene list, then Export.");
        }

        private static void RegisterSceneInBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes) if (s.path == scenePath) return;
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureAlwaysIncludedShader(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogWarning("[Playable] Shader not found: " + shaderName); return; }

            Object graphics = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0];
            var so = new SerializedObject(graphics);
            var list = so.FindProperty("m_AlwaysIncludedShaders");

            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }
    }
}
