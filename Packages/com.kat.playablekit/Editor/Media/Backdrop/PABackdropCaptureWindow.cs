#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PlayableKit.Editor.Media
{
    /// <summary>
    /// Bakes a blurred still from a video into a small JPG sprite, used behind a letterboxed video instead of a
    /// runtime blur. Live preview; requires FFmpeg + FFprobe.
    /// </summary>
    public sealed class PABackdropCaptureWindow : EditorWindow
    {
        private const double PreviewDelaySeconds = 0.35d;

        private readonly PAFfmpegSettingsGUI _ffmpeg = new PAFfmpegSettingsGUI();
        private PABackdropSettings _settings = new PABackdropSettings();
        private string _inputPath;
        private PAMediaInfo _source;
        private double _time;
        private string _outputPath;
        private bool _livePreview = true;

        private Texture2D _preview;
        private string _previewFile;
        private string _previewSignature;
        private double _previewDueAt = -1d;
        private string _status;
        private MessageType _statusType = MessageType.None;
        private Vector2 _scroll;

        [MenuItem("Tools/PlayableKit/Media/Backdrop Capture", priority = 42)]
        public static void Open()
        {
            var window = GetWindow<PABackdropCaptureWindow>("Backdrop Capture");
            window.minSize = new Vector2(420f, 520f);
            List<string> selected = PAMediaAssets.SelectedVideoFiles();
            if (selected.Count > 0) window.SetInput(selected[0]);
        }

        private void OnEnable()
        {
            _previewFile = Path.Combine(Path.GetTempPath(), "pa_backdrop_preview_" + GetInstanceID() + ".jpg");
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            if (_preview != null) DestroyImmediate(_preview);
            PAMediaAssets.DeleteIfExists(_previewFile);
        }

        private bool CanCapture
        {
            get { return _source != null && _source.HasVideo && _ffmpeg.IsVerified; }
        }

        private void SetInput(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            _ffmpeg.EnsureChecked();

            string error;
            PAMediaInfo info = PAMediaProbe.Probe(path, out error);
            if (info == null || !info.HasVideo)
            {
                SetStatus(error ?? "No video stream.", MessageType.Error);
                return;
            }

            _inputPath = path;
            _source = info;
            _time = PABackdropCapture.ClampTime(info.DurationSeconds * 0.5d, info);
            _settings.MatchAspect(info);
            _outputPath = PABackdropCapture.DefaultOutputPath(path);
            _previewSignature = null;
            SetStatus(null, MessageType.None);
            RequestPreview();
        }

        // ---------- preview ----------

        private void RequestPreview()
        {
            if (_livePreview) _previewDueAt = EditorApplication.timeSinceStartup + PreviewDelaySeconds;
        }

        private void Tick()
        {
            if (_previewDueAt < 0d || EditorApplication.timeSinceStartup < _previewDueAt) return;
            _previewDueAt = -1d;
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            if (!CanCapture) return;
            double time = PABackdropCapture.ClampTime(_time, _source);
            string signature = _settings.Signature(time);
            if (signature == _previewSignature) return;

            string error = PABackdropCapture.Capture(_inputPath, time, _settings, _previewFile);
            if (error != null)
            {
                SetStatus("Preview failed: " + error, MessageType.Error);
                return;
            }

            if (_preview == null) _preview = new Texture2D(2, 2, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
            _preview.LoadImage(File.ReadAllBytes(_previewFile));
            _previewSignature = signature;
            SetStatus("Preview " + _preview.width + "x" + _preview.height + ", "
                      + (new FileInfo(_previewFile).Length / 1024) + " KB at " + FormatTime(time) + ".", MessageType.None);
            Repaint();
        }

        private void Save()
        {
            if (string.IsNullOrEmpty(_outputPath)) return;
            string folder = Path.GetDirectoryName(_outputPath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            string path = Path.ChangeExtension(_outputPath, ".jpg");
            bool existed = File.Exists(path);
            string error = PABackdropCapture.Capture(_inputPath, PABackdropCapture.ClampTime(_time, _source), _settings, path);
            if (error != null)
            {
                SetStatus("Save failed: " + error, MessageType.Error);
                return;
            }

            PAMediaAssets.ImportBackdrop(path, Mathf.Max(_settings.width, _settings.height), existed);
            string assetPath = PAMediaAssets.ToAssetPath(path);
            if (assetPath != null) EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(assetPath));
            SetStatus("Saved " + (assetPath ?? path) + " (" + (new FileInfo(path).Length / 1024) + " KB)"
                      + (assetPath == null ? ". Outside Assets/, not imported." : "."), MessageType.Info);
        }

        // ---------- GUI ----------

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _ffmpeg.Draw();
            EditorGUILayout.Space();
            DrawInput();

            if (_source != null)
            {
                EditorGUILayout.Space();
                EditorGUI.BeginChangeCheck();
                DrawTime();
                DrawImageSettings();
                if (EditorGUI.EndChangeCheck()) RequestPreview();

                EditorGUILayout.Space();
                DrawPreview();
                DrawOutput();
            }

            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, _statusType);
            EditorGUILayout.EndScrollView();
        }

        private void DrawInput()
        {
            EditorGUILayout.LabelField("Video", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(string.IsNullOrEmpty(_inputPath) ? "(none)" : Path.GetFileName(_inputPath));
                if (GUILayout.Button("Pick...", GUILayout.Width(60)))
                {
                    SetInput(EditorUtility.OpenFilePanelWithFilters("Select video", string.Empty,
                        new[] { "Video", "mp4,mov,m4v,webm,avi,mkv", "All files", "*" }));
                }
                if (GUILayout.Button("Use Selected", GUILayout.Width(96)))
                {
                    List<string> selected = PAMediaAssets.SelectedVideoFiles();
                    if (selected.Count > 0) SetInput(selected[0]);
                }
            }
            if (_source != null) EditorGUILayout.LabelField("Source", _source.Describe());
        }

        private void DrawTime()
        {
            EditorGUILayout.LabelField("Frame", EditorStyles.boldLabel);
            float duration = Mathf.Max(0.01f, (float)_source.DurationSeconds);
            _time = EditorGUILayout.Slider("Time (s)", (float)_time, 0f, duration);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Start")) _time = 0d;
                if (GUILayout.Button("25%")) _time = duration * 0.25d;
                if (GUILayout.Button("50%")) _time = duration * 0.5d;
                if (GUILayout.Button("75%")) _time = duration * 0.75d;
                if (GUILayout.Button("End")) _time = duration;
            }
        }

        private void DrawImageSettings()
        {
            EditorGUILayout.LabelField("Image", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _settings.width = Mathf.Clamp(EditorGUILayout.IntField("Size", _settings.width), 2, 8192);
                _settings.height = Mathf.Clamp(EditorGUILayout.IntField(_settings.height, GUILayout.Width(60)), 2, 8192);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Match Video Aspect")) _settings.MatchAspect(_source);
                if (GUILayout.Button("Swap W/H")) _settings.Swap();
            }
            _settings.blurSigma = EditorGUILayout.Slider("Blur", _settings.blurSigma, 0f, 40f);
            _settings.darken = EditorGUILayout.Slider("Darken", _settings.darken, 0f, 1f);
            _settings.jpegQuality = EditorGUILayout.IntSlider(new GUIContent("JPEG q (2 best, 12 smallest)"), _settings.jpegQuality, 2, 12);
        }

        private void DrawPreview()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
                _livePreview = EditorGUILayout.ToggleLeft("Live", _livePreview, GUILayout.Width(50));
                using (new EditorGUI.DisabledScope(!CanCapture))
                {
                    if (GUILayout.Button("Refresh", GUILayout.Width(70)))
                    {
                        _previewSignature = null;
                        RefreshPreview();
                    }
                }
            }

            if (_preview == null) return;
            float width = position.width - 24f;
            float height = width * _preview.height / Mathf.Max(1f, _preview.width);
            Rect rect = GUILayoutUtility.GetRect(width, Mathf.Min(height, 360f), GUILayout.ExpandWidth(true));
            EditorGUI.DrawPreviewTexture(rect, _preview, null, ScaleMode.ScaleToFit);
        }

        private void DrawOutput()
        {
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _outputPath = EditorGUILayout.TextField(_outputPath);
                if (GUILayout.Button("...", GUILayout.Width(28)))
                {
                    string picked = EditorUtility.SaveFilePanel("Save backdrop", Path.GetDirectoryName(_outputPath),
                        Path.GetFileNameWithoutExtension(_outputPath), "jpg");
                    if (!string.IsNullOrEmpty(picked)) _outputPath = picked;
                }
            }
            using (new EditorGUI.DisabledScope(!CanCapture))
            {
                if (GUILayout.Button("Save Backdrop", GUILayout.Height(28))) Save();
            }
        }

        private void SetStatus(string text, MessageType type)
        {
            _status = text;
            _statusType = type;
        }

        private static string FormatTime(double seconds)
        {
            return seconds.ToString("0.00", CultureInfo.InvariantCulture) + "s";
        }
    }
}
#endif
