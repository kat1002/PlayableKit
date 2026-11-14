#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PlayableKit.Editor
{
    /// <summary>
    /// Lists AudioClips under the selected folder and applies a compact WebGL import preset
    /// (SFX or Music) to each one.
    /// </summary>
    public sealed class PAAudioImportOptimizer : EditorWindow
    {
        private const string Platform = "WebGL";
        private const float MusicMinSeconds = 10f;

        private enum Preset { Skip, Sfx, Music }

        private sealed class Row
        {
            public string path;
            public float length;
            public int channels;
            public long sourceKb;
            public string current;
            public Preset preset;
        }

        private readonly List<Row> _rows = new List<Row>();
        private Vector2 _scroll;

        [MenuItem("Tools/PlayableKit/Audio Import Optimizer")]
        private static void Open()
        {
            GetWindow<PAAudioImportOptimizer>("Audio Import Optimizer").Scan();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan Selected Folder")) Scan();
            using (new EditorGUI.DisabledScope(_rows.Count == 0))
            {
                if (GUILayout.Button("Apply")) Apply();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "SFX: mono, Vorbis q0.35, 22050 Hz.  Music: stereo, Vorbis q0.55, optimized sample rate.  Both: CompressedInMemory (" + Platform + ").",
                MessageType.None);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(row.path, GUILayout.MinWidth(200));
                EditorGUILayout.LabelField(row.length.ToString("0.0") + "s  " + row.channels + "ch  " + row.sourceKb + " KB", GUILayout.Width(150));
                EditorGUILayout.LabelField(row.current, GUILayout.Width(190));
                row.preset = (Preset)EditorGUILayout.EnumPopup(row.preset, GUILayout.Width(70));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void Scan()
        {
            _rows.Clear();
            string folder = SelectedFolder();
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (clip == null || importer == null) continue;

                var row = new Row();
                row.path = path;
                row.length = clip.length;
                row.channels = clip.channels;
                row.sourceKb = new FileInfo(path).Length / 1024;
                row.current = Describe(importer);
                row.preset = clip.length > MusicMinSeconds ? Preset.Music : Preset.Sfx;
                _rows.Add(row);
            }
            Repaint();
        }

        private static string SelectedFolder()
        {
            Object selected = Selection.activeObject;
            if (selected == null) return "Assets";

            string path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path)) return "Assets";
            return AssetDatabase.IsValidFolder(path) ? path : Path.GetDirectoryName(path).Replace('\\', '/');
        }

        private static string Describe(AudioImporter importer)
        {
            if (!importer.ContainsSampleSettingsOverride(Platform)) return "(default)";

            AudioImporterSampleSettings s = importer.GetOverrideSampleSettings(Platform);
            return s.compressionFormat + " " + s.loadType + " q" + s.quality.ToString("0.00");
        }

        private void Apply()
        {
            if (!EditorUtility.DisplayDialog("Audio Import Optimizer",
                    "Reimport " + _rows.Count + " clip(s) with the chosen presets?", "Apply", "Cancel")) return;

            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                if (row.preset == Preset.Skip) continue;

                var importer = AssetImporter.GetAtPath(row.path) as AudioImporter;
                if (importer == null) continue;

                bool sfx = row.preset == Preset.Sfx;
                AudioImporterSampleSettings s = importer.GetOverrideSampleSettings(Platform);
                s.loadType = AudioClipLoadType.CompressedInMemory;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = sfx ? 0.35f : 0.55f;
                s.sampleRateSetting = sfx ? AudioSampleRateSetting.OverrideSampleRate : AudioSampleRateSetting.OptimizeSampleRate;
                if (sfx) s.sampleRateOverride = 22050;

                importer.forceToMono = sfx;
                importer.SetOverrideSampleSettings(Platform, s);
                importer.SaveAndReimport();
            }
            Scan();
        }
    }
}
#endif
