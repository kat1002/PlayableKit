#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PlayableKit.Editor.Media
{
    /// <summary>
    /// Batch-encodes videos into small, browser-safe H.264 for playable ads (two-pass, size budget, separate MP3,
    /// verification, automatic refit when over budget). Requires FFmpeg + FFprobe.
    /// </summary>
    public sealed class PAVideoOptimizerWindow : EditorWindow
    {
        private static readonly string[] PresetLabels = { "Lite  ≤ 2.0 MB", "Balanced  ≤ 2.4 MB", "High  ≤ 3.0 MB", "Custom" };
        private static readonly string[] ResolutionLabels = { "HD 720p (1280x720 / 720x1280)", "Lite 540p (960x540 / 540x960)" };
        private static readonly string[] AudioLabels = { "Separate MP3 (recommended)", "Embedded AAC", "Remove audio" };
        private static readonly string[] SpeedLabels = { "Best (veryslow)", "Balanced (slow)", "Fast (medium)" };

        private readonly PAFfmpegSettingsGUI _ffmpeg = new PAFfmpegSettingsGUI();
        private readonly List<PAVideoEncodeTask> _tasks = new List<PAVideoEncodeTask>();
        private PAVideoEncodeSettings _settings;
        private int _current = -1;
        private bool _showAdvanced;
        private bool _showLog;
        private Vector2 _scroll;
        private Vector2 _logScroll;

        [MenuItem("Tools/PlayableKit/Media/Video Optimizer", priority = 41)]
        public static void Open()
        {
            var window = GetWindow<PAVideoOptimizerWindow>("Video Optimizer");
            window.minSize = new Vector2(520f, 480f);
            window.AddVideos(PAMediaAssets.SelectedVideoFiles());
        }

        private bool IsEncoding
        {
            get { return _current >= 0; }
        }

        private void OnEnable()
        {
            _settings = PAVideoEncodeSettings.Load();
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            CancelAll();
            _settings.Save();
        }

        // ---------- queue ----------

        private void AddVideos(List<string> paths)
        {
            for (int i = 0; i < paths.Count; i++) AddVideo(paths[i]);
        }

        private void AddVideo(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            for (int i = 0; i < _tasks.Count; i++)
            {
                if (string.Equals(_tasks[i].InputPath, path, System.StringComparison.OrdinalIgnoreCase)) return;
            }

            _ffmpeg.EnsureChecked();
            string error;
            PAMediaInfo info = PAMediaProbe.Probe(path, out error);
            if (info == null || !info.HasVideo)
            {
                ShowNotification(new GUIContent(Path.GetFileName(path) + ": " + (error ?? "no video stream")));
                return;
            }
            _tasks.Add(new PAVideoEncodeTask(path, info));
        }

        private void StartAll()
        {
            _settings.Save();
            EditorApplication.LockReloadAssemblies();
            _current = -1;
            StartNext();
        }

        private void StartNext()
        {
            for (int i = _current + 1; i < _tasks.Count; i++)
            {
                if (_tasks[i].Status != PAEncodeStatus.Pending) continue;
                _current = i;
                _tasks[i].Start(_settings);
                return;
            }
            FinishAll();
        }

        private void FinishAll()
        {
            _current = -1;
            EditorApplication.UnlockReloadAssemblies();
            AssetDatabase.Refresh();
            Repaint();
        }

        private void CancelAll()
        {
            if (!IsEncoding) return;
            for (int i = 0; i < _tasks.Count; i++) _tasks[i].Cancel();
            for (int i = 0; i < _tasks.Count; i++) _tasks[i].Poll();
            FinishAll();
        }

        private void Tick()
        {
            if (!IsEncoding) return;
            PAVideoEncodeTask task = _tasks[_current];
            task.Poll();
            if (task.IsFinished) StartNext();
            Repaint();
        }

        // ---------- GUI ----------

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _ffmpeg.Draw();
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(IsEncoding))
            {
                DrawVideoList();
                EditorGUILayout.Space();
                DrawSettings();
                EditorGUILayout.Space();
                DrawOutput();
            }

            EditorGUILayout.Space();
            DrawRunControls();
            DrawLog();
            EditorGUILayout.EndScrollView();
        }

        private void DrawVideoList()
        {
            EditorGUILayout.LabelField("Videos", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add File...")) AddVideo(PickVideoFile());
                if (GUILayout.Button("Add Selected")) AddVideos(PAMediaAssets.SelectedVideoFiles());
                if (GUILayout.Button("Clear", GUILayout.Width(60))) _tasks.Clear();
            }

            if (_tasks.Count == 0) EditorGUILayout.HelpBox("Drop video files anywhere on this window.", MessageType.None);
            HandleDrop();

            for (int i = 0; i < _tasks.Count; i++)
            {
                if (DrawTaskRow(_tasks[i]))
                {
                    _tasks.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
            }
        }

        /// <summary>Returns true when the row's remove button was pressed.</summary>
        private bool DrawTaskRow(PAVideoEncodeTask task)
        {
            bool remove = false;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(task.FileName, EditorStyles.boldLabel);
                    remove = GUILayout.Button("X", GUILayout.Width(22));
                }
                EditorGUILayout.LabelField("Source", task.Source.Describe());

                if (task.Status == PAEncodeStatus.Pending)
                {
                    var plan = new PAVideoEncodePlan(task.InputPath, task.Source, _settings);
                    string problem;
                    if (plan.IsValid(out problem))
                    {
                        EditorGUILayout.LabelField("Output", plan.Size.x + "x" + plan.Size.y + "  "
                            + plan.FrameRate.ToString("0.##", CultureInfo.InvariantCulture) + " fps  " + plan.VideoBitrateKbps + " kbps  ≈ "
                            + plan.EstimatedTotalMb.ToString("0.00", CultureInfo.InvariantCulture) + " MB");
                    }
                    else
                    {
                        EditorGUILayout.HelpBox(problem, MessageType.Warning);
                    }
                }
                else if (task.Status == PAEncodeStatus.Encoding)
                {
                    Rect bar = GUILayoutUtility.GetRect(18f, 18f, GUILayout.ExpandWidth(true));
                    EditorGUI.ProgressBar(bar, task.Progress, task.StepLabel + "  " + Mathf.RoundToInt(task.Progress * 100f) + "%");
                }
                else
                {
                    EditorGUILayout.HelpBox(task.Message, StatusMessageType(task.Status));
                }
            }
            return remove;
        }

        private void DrawSettings()
        {
            EditorGUILayout.LabelField("Encode", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var preset = (PAVideoPreset)EditorGUILayout.Popup("Preset", (int)_settings.preset, PresetLabels);
            if (EditorGUI.EndChangeCheck()) _settings.ApplyPreset(preset);

            EditorGUI.BeginChangeCheck();
            _settings.resolution = (PAVideoResolution)EditorGUILayout.Popup("Resolution", (int)_settings.resolution, ResolutionLabels);
            _settings.useSizeBudget = EditorGUILayout.Toggle("Fit Size Budget", _settings.useSizeBudget);
            if (_settings.useSizeBudget)
                _settings.budgetMb = Mathf.Max(0.2f, EditorGUILayout.FloatField("Budget per Video (MB)", _settings.budgetMb));
            else
                _settings.videoBitrateKbps = Mathf.Max(PAVideoEncodeSettings.MinVideoBitrateKbps,
                    EditorGUILayout.IntField("Video Bitrate (kbps)", _settings.videoBitrateKbps));
            _settings.audioMode = (PAVideoAudioMode)EditorGUILayout.Popup("Audio", (int)_settings.audioMode, AudioLabels);
            using (new EditorGUI.DisabledScope(_settings.audioMode == PAVideoAudioMode.Remove))
            {
                _settings.audioBitrateKbps = Mathf.Clamp(EditorGUILayout.IntField("Audio Bitrate (kbps)", _settings.audioBitrateKbps),
                    PAVideoEncodeSettings.MinAudioBitrateKbps, 320);
            }

            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced", true);
            if (_showAdvanced)
            {
                EditorGUI.indentLevel++;
                _settings.keepAspect = EditorGUILayout.Toggle(new GUIContent("Keep Aspect (no bars)", "Off pads to the exact 16:9 / 9:16 box with black bars."), _settings.keepAspect);
                _settings.maxFrameRate = EditorGUILayout.IntSlider("Max Frame Rate", _settings.maxFrameRate, 15, 60);
                _settings.speed = (PAEncoderSpeed)EditorGUILayout.Popup("Encoder Speed", (int)_settings.speed, SpeedLabels);
                _settings.gameFootageTune = EditorGUILayout.Toggle("Game Footage Tune", _settings.gameFootageTune);
                _settings.sharpen = EditorGUILayout.Slider("Sharpen", _settings.sharpen, 0f, 1f);
                _settings.verify = EditorGUILayout.Toggle("Verify Output", _settings.verify);
                using (new EditorGUI.DisabledScope(!_settings.useSizeBudget))
                {
                    _settings.autoFitBudget = EditorGUILayout.Toggle("Re-encode If Over Budget", _settings.autoFitBudget);
                }
                EditorGUI.indentLevel--;
            }
            if (EditorGUI.EndChangeCheck()) _settings.preset = PAVideoPreset.Custom;
        }

        private void DrawOutput()
        {
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            _settings.outputNextToSource = EditorGUILayout.Toggle("Next to Source", _settings.outputNextToSource);
            if (_settings.outputNextToSource) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                _settings.outputFolder = EditorGUILayout.TextField("Folder", _settings.outputFolder);
                if (GUILayout.Button("...", GUILayout.Width(28)))
                {
                    string picked = EditorUtility.OpenFolderPanel("Output folder", _settings.outputFolder, string.Empty);
                    if (!string.IsNullOrEmpty(picked)) _settings.outputFolder = picked;
                }
            }
            if (PAMediaAssets.ToAssetPath(_settings.outputFolder + "/x") == null)
            {
                EditorGUILayout.HelpBox("Outside Assets/: files are written but not imported.", MessageType.Info);
            }
        }

        private void DrawRunControls()
        {
            if (IsEncoding)
            {
                if (GUILayout.Button("Cancel", GUILayout.Height(30))) CancelAll();
                return;
            }

            int pending = 0;
            for (int i = 0; i < _tasks.Count; i++) if (_tasks[i].Status == PAEncodeStatus.Pending) pending++;

            using (new EditorGUI.DisabledScope(pending == 0 || !_ffmpeg.IsVerified))
            {
                if (GUILayout.Button(pending > 1 ? "Optimize " + pending + " Videos" : "Optimize Video", GUILayout.Height(30))) StartAll();
            }
            if (pending == 0 && _tasks.Count > 0 && GUILayout.Button("Reset Finished"))
            {
                for (int i = 0; i < _tasks.Count; i++) _tasks[i] = new PAVideoEncodeTask(_tasks[i].InputPath, _tasks[i].Source);
            }
        }

        private void DrawLog()
        {
            PAVideoEncodeTask task = IsEncoding ? _tasks[_current] : LastStarted();
            if (task == null) return;

            _showLog = EditorGUILayout.Foldout(_showLog, "FFmpeg Log (" + task.FileName + ")", true);
            if (!_showLog) return;

            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.Height(140f));
            EditorGUILayout.TextArea(string.Join("\n", task.LogLines()), EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();
        }

        private PAVideoEncodeTask LastStarted()
        {
            for (int i = _tasks.Count - 1; i >= 0; i--) if (_tasks[i].Status != PAEncodeStatus.Pending) return _tasks[i];
            return null;
        }

        private void HandleDrop()
        {
            Event e = Event.current;
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type != EventType.DragPerform) return;

            DragAndDrop.AcceptDrag();
            string[] paths = DragAndDrop.paths;
            for (int i = 0; i < paths.Length; i++)
            {
                string full = Path.IsPathRooted(paths[i]) ? paths[i] : PAMediaAssets.ToAbsolute(paths[i]);
                if (PAMediaAssets.IsVideoFile(full)) AddVideo(full);
            }
            e.Use();
        }

        private static string PickVideoFile()
        {
            return EditorUtility.OpenFilePanelWithFilters("Select video", string.Empty,
                new[] { "Video", "mp4,mov,m4v,webm,avi,mkv", "All files", "*" });
        }

        private static MessageType StatusMessageType(PAEncodeStatus status)
        {
            switch (status)
            {
                case PAEncodeStatus.Done: return MessageType.Info;
                case PAEncodeStatus.Warning: return MessageType.Warning;
                case PAEncodeStatus.Failed: return MessageType.Error;
                default: return MessageType.None;
            }
        }
    }
}
#endif
