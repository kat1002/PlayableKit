#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PlayableKit.Editor.Media
{
    /// <summary>The "FFmpeg" box shared by every media window: path, browse, auto-detect, test.</summary>
    public sealed class PAFfmpegSettingsGUI
    {
        private string _status;
        private MessageType _statusType = MessageType.None;
        private bool _checked;

        public bool IsVerified { get; private set; }

        /// <summary>Checks the saved path once, the first time a window draws.</summary>
        public void EnsureChecked()
        {
            if (!_checked) Test();
        }

        public void Draw()
        {
            EnsureChecked();
            EditorGUILayout.LabelField("FFmpeg", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                string path = EditorGUILayout.TextField(PAFfmpegLocator.FfmpegPath);
                if (EditorGUI.EndChangeCheck())
                {
                    PAFfmpegLocator.FfmpegPath = path;
                    IsVerified = false;
                    _status = null;
                }
                if (GUILayout.Button("Browse", GUILayout.Width(64))) Browse();
                if (GUILayout.Button("Detect", GUILayout.Width(56))) Detect();
                if (GUILayout.Button("Test", GUILayout.Width(44))) Test();
            }

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.HelpBox(_status, _statusType);
            }
            if (!IsVerified)
            {
                EditorGUILayout.HelpBox(
                    "Install FFmpeg (with ffprobe), e.g. 'winget install Gyan.FFmpeg' or 'brew install ffmpeg', then press Detect.",
                    MessageType.Info);
            }
        }

        private void Browse()
        {
            string picked = EditorUtility.OpenFilePanel("Select ffmpeg executable", string.Empty, string.Empty);
            if (string.IsNullOrEmpty(picked)) return;
            PAFfmpegLocator.FfmpegPath = picked;
            Test();
        }

        private void Detect()
        {
            string found = PAFfmpegLocator.Detect();
            if (found == null)
            {
                SetStatus("ffmpeg not found in PATH or the usual install folders.", MessageType.Warning, false);
                return;
            }
            PAFfmpegLocator.FfmpegPath = found;
            Test();
        }

        private void Test()
        {
            _checked = true;
            string error;
            string version = PAFfmpegLocator.Verify(out error);
            if (version == null) SetStatus(error, MessageType.Error, false);
            else SetStatus(version, MessageType.None, true);
        }

        private void SetStatus(string text, MessageType type, bool verified)
        {
            _status = text;
            _statusType = type;
            IsVerified = verified;
        }
    }
}
#endif
