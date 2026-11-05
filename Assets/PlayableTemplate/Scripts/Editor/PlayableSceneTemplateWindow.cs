using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlayableTemplate.Core.EditorTools
{
    /// <summary>Small window to create a new template scene or adapt the open scene to the template.</summary>
    public class PlayableSceneTemplateWindow : EditorWindow
    {
        private string _sceneName = PlayableTemplateSceneBuilder.DefaultSceneName;
        private string _folder = PlayableTemplateSceneBuilder.DefaultSceneFolder;
        private List<string> _lastReport;

        [MenuItem("Tools/Playable/Playable Scene Template...")]
        public static void Open()
        {
            var w = GetWindow<PlayableSceneTemplateWindow>(true, "Playable Scene Template");
            w.minSize = new Vector2(380f, 300f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("New Scene", EditorStyles.boldLabel);
            _sceneName = EditorGUILayout.TextField("Scene Name", _sceneName);
            using (new EditorGUILayout.HorizontalScope())
            {
                _folder = EditorGUILayout.TextField("Folder", _folder);
                if (GUILayout.Button("Browse", GUILayout.Width(60f))) Browse();
            }
            if (GUILayout.Button("Create Scene")) PlayableTemplateSceneBuilder.CreateScene(_folder, _sceneName);

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("Adapt Open Scene", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Active scene", SceneManager.GetActiveScene().name);
            if (GUILayout.Button("Adapt Open Scene To Template"))
                _lastReport = PlayableTemplateSceneBuilder.AdaptOpenScene();

            if (_lastReport != null)
            {
                string text = _lastReport.Count == 0
                    ? "Scene already matches the template - no changes."
                    : string.Join("\n", _lastReport);
                EditorGUILayout.HelpBox(text, MessageType.Info);
            }
        }

        private void Browse()
        {
            string start = _folder.StartsWith("Assets") && AssetDatabase.IsValidFolder(_folder) ? _folder : "Assets";
            string picked = EditorUtility.OpenFolderPanel("Select scene folder", start, "");
            if (string.IsNullOrEmpty(picked)) return;

            picked = picked.Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/');
            if (picked == dataPath) _folder = "Assets";
            else if (picked.StartsWith(dataPath + "/")) _folder = "Assets" + picked.Substring(dataPath.Length);
            else
            {
                EditorUtility.DisplayDialog("Invalid folder", "Pick a folder inside this project's Assets folder.", "OK");
                return;
            }
            GUI.FocusControl(null);
        }
    }
}
