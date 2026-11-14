#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace PlayableKit.Editor.Media
{
    public enum PAVideoPreset { Lite, Balanced, High, Custom }

    public enum PAVideoResolution { Hd720, Lite540 }

    public enum PAVideoAudioMode { SeparateMp3, EmbeddedAac, Remove }

    /// <summary>x264 preset: slower means a smaller file at the same quality.</summary>
    public enum PAEncoderSpeed { Best, Balanced, Fast }

    /// <summary>Every option of the Video Optimizer. Plain data, saved per user in EditorPrefs.</summary>
    [Serializable]
    public sealed class PAVideoEncodeSettings
    {
        private const string PreferenceKey = "PlayableKit.VideoOptimizer.Settings";

        public PAVideoPreset preset = PAVideoPreset.Balanced;
        public PAVideoResolution resolution = PAVideoResolution.Hd720;
        [Tooltip("Scale to fit inside the target box keeping the source aspect (no black bars). Off: pad to the exact box.")]
        public bool keepAspect = true;
        public bool useSizeBudget = true;
        public float budgetMb = 2.4f;
        public int videoBitrateKbps = 1800;
        public PAVideoAudioMode audioMode = PAVideoAudioMode.SeparateMp3;
        public int audioBitrateKbps = 96;
        [Tooltip("Upper limit. Sources with a lower frame rate keep theirs (no duplicated frames).")]
        public int maxFrameRate = 30;
        [Tooltip("x264 '-tune animation': better for flat game footage.")]
        public bool gameFootageTune = true;
        [Range(0f, 1f)] public float sharpen = 0.2f;
        public PAEncoderSpeed speed = PAEncoderSpeed.Best;
        [Tooltip("ffprobe + full decode check of the result (Baseline, size, no B-frames, yuv420p, fps).")]
        public bool verify = true;
        [Tooltip("Re-encode at a lower bitrate when the result is over the size budget (up to 2 retries).")]
        public bool autoFitBudget = true;
        public bool outputNextToSource = true;
        public string outputFolder = string.Empty;

        public const int MinVideoBitrateKbps = 300;
        public const int MinAudioBitrateKbps = 48;

        public bool OutputsAudio(bool sourceHasAudio)
        {
            return sourceHasAudio && audioMode != PAVideoAudioMode.Remove;
        }

        /// <summary>Resets the encode options to a preset. Custom changes nothing.</summary>
        public void ApplyPreset(PAVideoPreset value)
        {
            preset = value;
            if (value == PAVideoPreset.Custom) return;

            resolution = PAVideoResolution.Hd720;
            keepAspect = true;
            useSizeBudget = true;
            audioMode = PAVideoAudioMode.SeparateMp3;
            audioBitrateKbps = 96;
            maxFrameRate = 30;
            gameFootageTune = true;
            sharpen = 0.2f;
            speed = PAEncoderSpeed.Best;
            budgetMb = BudgetFor(value);
        }

        public static float BudgetFor(PAVideoPreset value)
        {
            switch (value)
            {
                case PAVideoPreset.Lite: return 2.0f;
                case PAVideoPreset.High: return 3.0f;
                default: return 2.4f;
            }
        }

        public static PAVideoEncodeSettings Load()
        {
            string json = EditorPrefs.GetString(PreferenceKey, string.Empty);
            var settings = new PAVideoEncodeSettings();
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    EditorJsonUtility.FromJsonOverwrite(json, settings);
                }
                catch (ArgumentException)
                {
                    // corrupt pref: keep defaults
                }
            }
            return settings;
        }

        public void Save()
        {
            EditorPrefs.SetString(PreferenceKey, EditorJsonUtility.ToJson(this));
        }
    }
}
#endif
