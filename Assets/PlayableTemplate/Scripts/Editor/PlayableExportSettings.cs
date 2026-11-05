using UnityEditor;
using UnityEngine;

namespace PlayableTemplate.Core.EditorTools
{
    /// <summary>
    /// Per-project export configuration. sceneOverridePath empty = use the active/open scene.
    /// Auto-created at a well-known path on first use so no manual setup is required.
    /// </summary>
    public class PlayableExportSettings : ScriptableObject
    {
        private const string AssetPath = "Assets/PlayableTemplate/PlayableExportSettings.asset";

        [Tooltip("Leave empty to use the currently active/open scene.")]
        public string sceneOverridePath = "";
        public string alwaysIncludedShaderName = "Unlit/Color";

        public static PlayableExportSettings GetOrCreate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PlayableExportSettings>(AssetPath);
            if (settings != null) return settings;

            settings = CreateInstance<PlayableExportSettings>();
            var dir = System.IO.Path.GetDirectoryName(AssetPath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }
}
