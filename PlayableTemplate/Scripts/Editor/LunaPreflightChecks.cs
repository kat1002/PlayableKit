using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PlayableTemplate.Core.EditorTools
{
    /// <summary>
    /// Shared, project-agnostic preflight checks. A project's own validator calls these plus
    /// its own game-specific presence checks (see BikePlayable's LunaPreflightValidator).
    /// </summary>
    public static class LunaPreflightChecks
    {
        public static void CheckActiveSceneIsSavedAndRegistered(StringBuilder sb, ref int errors, ref int warnings)
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(activeScene.path))
            {
                string msg = "[Preflight] Active scene has never been saved.";
                Debug.LogError(msg);
                sb.AppendLine("FAIL: " + msg);
                errors++;
                return;
            }

            bool registered = false;
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path == activeScene.path) { registered = true; break; }

            if (!registered)
            {
                string msg = "[Preflight] Active scene '" + activeScene.name + "' is not registered in Build Settings yet " +
                             "(run Prepare For Luna first).";
                Debug.LogWarning(msg);
                sb.AppendLine("WARN: " + msg);
                warnings++;
            }
            else
            {
                string msg = "[Preflight] Active scene: " + activeScene.name + " (PASS)";
                Debug.Log(msg);
                sb.AppendLine("PASS: " + msg);
            }
        }

        public static void CheckNoMissingScripts(StringBuilder sb, ref int errors)
        {
            int missingScripts = 0;
            foreach (var go in Object.FindObjectsOfType<GameObject>(true))
            {
                var components = go.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] == null)
                    {
                        string msg = "[Preflight] Missing script component on GameObject: '" + go.name + "'";
                        Debug.LogError(msg, go);
                        sb.AppendLine("FAIL: " + msg);
                        missingScripts++;
                        errors++;
                    }
                }
            }
            if (missingScripts == 0)
            {
                string msg = "[Preflight] Zero missing script components across all scene objects (PASS)";
                Debug.Log(msg);
                sb.AppendLine("PASS: " + msg);
            }
        }

        public static void CheckNoMeshColliders(StringBuilder sb, ref int warnings)
        {
            var meshColliders = Object.FindObjectsOfType<MeshCollider>();
            if (meshColliders.Length > 0)
            {
                string msg = "[Preflight] " + meshColliders.Length + " MeshColliders found in scene. Recommend removing as Luna physics is disabled.";
                Debug.LogWarning(msg);
                sb.AppendLine("WARN: " + msg);
                warnings++;
            }
            else
            {
                string msg = "[Preflight] Zero MeshColliders in scene (PASS)";
                Debug.Log(msg);
                sb.AppendLine("PASS: " + msg);
            }
        }

        public static void CheckLunaJsonHasCompilerV2(StringBuilder sb, ref int warnings)
        {
            string lunaJsonPath = Path.Combine(Application.dataPath, "../luna.json");
            if (File.Exists(lunaJsonPath))
            {
                string text = File.ReadAllText(lunaJsonPath);
                if (text.Contains("\"enableLunaCompilerV2\": true"))
                {
                    string msg = "[Preflight] Luna Compiler V2 is enabled in luna.json (PASS)";
                    Debug.Log(msg);
                    sb.AppendLine("PASS: " + msg);
                }
                else
                {
                    string msg = "[Preflight] Luna Compiler V2 is not explicitly enabled in luna.json.";
                    Debug.LogWarning(msg);
                    sb.AppendLine("WARN: " + msg);
                    warnings++;
                }
            }
            else
            {
                string msg = "[Preflight] luna.json not found in project root.";
                Debug.LogWarning(msg);
                sb.AppendLine("WARN: " + msg);
                warnings++;
            }
        }

        public static void CheckComponentPresent<T>(string label, StringBuilder sb, ref int errors) where T : Object
        {
            var found = Object.FindObjectOfType<T>();
            if (found == null)
            {
                string msg = "[Preflight] " + label + " not found in scene!";
                Debug.LogError(msg);
                sb.AppendLine("FAIL: " + msg);
                errors++;
            }
            else
            {
                sb.AppendLine("PASS: [Preflight] " + label + " found");
            }
        }
    }
}
