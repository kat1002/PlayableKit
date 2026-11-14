#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace PlayableKit.Editor.Media
{
    /// <summary>Options for a baked, blurred backdrop frame.</summary>
    [Serializable]
    public sealed class PABackdropSettings
    {
        public int width = 640;
        public int height = 360;
        [Range(0f, 40f)] public float blurSigma = 18f;
        [Range(0f, 1f)] public float darken;
        [Tooltip("JPEG quality scale: 2 is best, 12 is smallest.")]
        [Range(2, 12)] public int jpegQuality = 3;

        /// <summary>Keeps the longest side and matches the source's aspect ratio.</summary>
        public void MatchAspect(PAMediaInfo source)
        {
            if (source == null || source.Width <= 0 || source.Height <= 0) return;
            int longSide = Mathf.Max(width, height);
            if (source.IsLandscape)
            {
                width = longSide;
                height = Even(longSide * (double)source.Height / source.Width);
            }
            else
            {
                height = longSide;
                width = Even(longSide * (double)source.Width / source.Height);
            }
        }

        public void Swap()
        {
            int previous = width;
            width = height;
            height = previous;
        }

        public string Signature(double time)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2}|{3:0.###}|{4:0.###}|{5}", time, width, height, blurSigma, darken, jpegQuality);
        }

        internal static int Even(double value)
        {
            int rounded = (int)Math.Round(value);
            return Math.Max(2, rounded - (rounded & 1));
        }
    }

    /// <summary>Grabs one frame with ffmpeg, cover-crops it, blurs / darkens it and writes a JPG.</summary>
    public static class PABackdropCapture
    {
        private const int TimeoutMs = 30000;

        public static string BuildArguments(string input, double timeSeconds, PABackdropSettings settings, string destination)
        {
            int width = PABackdropSettings.Even(Mathf.Clamp(settings.width, 2, 8192));
            int height = PABackdropSettings.Even(Mathf.Clamp(settings.height, 2, 8192));
            float blur = Mathf.Clamp(settings.blurSigma, 0f, 40f);
            float darken = Mathf.Clamp01(settings.darken);

            string filter = string.Format(CultureInfo.InvariantCulture,
                "scale={0}:{1}:force_original_aspect_ratio=increase:flags=lanczos,crop={0}:{1},setsar=1", width, height);
            if (blur > 0.01f) filter += string.Format(CultureInfo.InvariantCulture, ",gblur=sigma={0:0.###}:steps=3", blur);
            if (darken > 0.001f) filter += string.Format(CultureInfo.InvariantCulture, ",colorchannelmixer=rr={0:0.###}:gg={0:0.###}:bb={0:0.###}", 1f - darken);

            // -ss before -i: fast keyframe seek, then exact decode to the frame.
            return string.Format(CultureInfo.InvariantCulture,
                "-hide_banner -loglevel error -y -ss {0:0.###} -i {1} -frames:v 1 -an -vf {2} -q:v {3} -pix_fmt yuvj420p -update 1 {4}",
                Math.Max(0d, timeSeconds),
                PAFfmpegProcess.Quote(input),
                PAFfmpegProcess.Quote(filter),
                Mathf.Clamp(settings.jpegQuality, 2, 12),
                PAFfmpegProcess.Quote(destination));
        }

        /// <summary>Writes the JPG. Returns null on success, else the error.</summary>
        public static string Capture(string input, double timeSeconds, PABackdropSettings settings, string destination)
        {
            PAProcessResult result = PAFfmpegProcess.Run(PAFfmpegLocator.FfmpegPath, BuildArguments(input, timeSeconds, settings, destination), TimeoutMs);
            if (!result.Succeeded) return result.ErrorSummary;
            return File.Exists(destination) ? null : "ffmpeg wrote no image.";
        }

        /// <summary>Clamps a time so the seek lands on a real frame (a little before the end).</summary>
        public static double ClampTime(double time, PAMediaInfo source)
        {
            if (source == null || source.DurationSeconds <= 0d) return Math.Max(0d, time);
            double lastFrame = Math.Max(0d, source.DurationSeconds - (source.FrameRate > 0d ? 1d / source.FrameRate : 0.05d));
            return Math.Min(Math.Max(0d, time), lastFrame);
        }

        public static string DefaultOutputPath(string input)
        {
            return Path.Combine(Path.GetDirectoryName(input) ?? string.Empty, Path.GetFileNameWithoutExtension(input) + "_backdrop.jpg");
        }
    }
}
#endif
