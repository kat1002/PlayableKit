#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace PlayableKit.Editor.Media
{
    /// <summary>
    /// The concrete encode for one source file: output size, frame rate, bitrate, output paths and the ffmpeg command lines.
    /// Pure computation (no UI, no process), so the sizing rules can be unit-tested.
    /// </summary>
    public sealed class PAVideoEncodePlan
    {
        /// <summary>Container and muxing overhead kept free inside the size budget.</summary>
        public const double ContainerOverhead = 0.015d;

        public readonly string InputPath;
        public readonly PAMediaInfo Source;
        public readonly PAVideoEncodeSettings Settings;
        public readonly Vector2Int Size;
        /// <summary>Exact box including black bars when keepAspect is off; equals Size otherwise.</summary>
        public readonly Vector2Int PadBox;
        public readonly double FrameRate;
        public readonly int AudioBitrateKbps;
        public readonly string VideoPath;
        public readonly string AudioPath;

        public int VideoBitrateKbps { get; private set; }

        public PAVideoEncodePlan(string inputPath, PAMediaInfo source, PAVideoEncodeSettings settings)
        {
            InputPath = inputPath;
            Source = source;
            Settings = settings;

            Vector2Int box = TargetBox(source.IsLandscape, settings.resolution);
            Size = settings.keepAspect ? FitInside(source.Width, source.Height, box) : box;
            PadBox = box;
            FrameRate = OutputFrameRate(source.FrameRate, settings.maxFrameRate);
            AudioBitrateKbps = settings.OutputsAudio(source.HasAudio)
                ? Mathf.Max(PAVideoEncodeSettings.MinAudioBitrateKbps, settings.audioBitrateKbps)
                : 0;
            VideoBitrateKbps = settings.useSizeBudget
                ? VideoBitrateForBudget(settings.budgetMb, source.DurationSeconds, AudioBitrateKbps)
                : settings.videoBitrateKbps;

            string folder = settings.outputNextToSource || string.IsNullOrEmpty(settings.outputFolder)
                ? Path.GetDirectoryName(inputPath)
                : settings.outputFolder;
            string baseName = Path.GetFileNameWithoutExtension(inputPath) + "_playable_" + Size.x + "x" + Size.y + "_"
                              + settings.preset.ToString().ToLowerInvariant();
            bool separateAudio = source.HasAudio && settings.audioMode == PAVideoAudioMode.SeparateMp3;
            string[] paths = PAMediaAssets.UniquePaths(
                Path.Combine(folder ?? string.Empty, baseName + ".mp4"),
                separateAudio ? Path.Combine(folder ?? string.Empty, baseName + "_audio.mp3") : null);
            VideoPath = paths[0];
            AudioPath = paths[1];
        }

        public bool IsValid(out string problem)
        {
            if (Source.DurationSeconds <= 0d) problem = "Unknown duration.";
            else if (VideoBitrateKbps < PAVideoEncodeSettings.MinVideoBitrateKbps)
                problem = "Budget too small for a " + Source.DurationSeconds.ToString("0.0", CultureInfo.InvariantCulture)
                          + "s video (needs at least " + PAVideoEncodeSettings.MinVideoBitrateKbps + " kbps). Raise the budget, lower the audio bitrate or trim the clip.";
            else problem = null;
            return problem == null;
        }

        public double EstimatedTotalMb
        {
            get { return Source.DurationSeconds * (VideoBitrateKbps + AudioBitrateKbps) / 8000d * (1d + ContainerOverhead); }
        }

        public long BudgetBytes
        {
            get { return (long)(Settings.budgetMb * 1000000d); }
        }

        /// <summary>Scales the bitrate so the next encode lands just under budget. False when it would fall below the minimum.</summary>
        public bool RefitBitrate(long actualVideoBytes, long audioBytes)
        {
            double allowedVideoBytes = BudgetBytes * (1d - ContainerOverhead) - audioBytes;
            if (actualVideoBytes <= 0 || allowedVideoBytes <= 0) return false;

            int next = (int)Math.Floor(VideoBitrateKbps * (allowedVideoBytes / actualVideoBytes) * 0.97d);
            if (next < PAVideoEncodeSettings.MinVideoBitrateKbps || next >= VideoBitrateKbps) return false;
            VideoBitrateKbps = next;
            return true;
        }

        // ---------- sizing rules ----------

        public static Vector2Int TargetBox(bool landscape, PAVideoResolution resolution)
        {
            int longSide = resolution == PAVideoResolution.Lite540 ? 960 : 1280;
            int shortSide = resolution == PAVideoResolution.Lite540 ? 540 : 720;
            return landscape ? new Vector2Int(longSide, shortSide) : new Vector2Int(shortSide, longSide);
        }

        /// <summary>Largest even size inside <paramref name="box"/> with the source aspect. Never upscales.</summary>
        public static Vector2Int FitInside(int width, int height, Vector2Int box)
        {
            if (width <= 0 || height <= 0) return box;
            double scale = Math.Min(1d, Math.Min((double)box.x / width, (double)box.y / height));
            return new Vector2Int(Even(width * scale), Even(height * scale));
        }

        public static double OutputFrameRate(double sourceFps, int maxFps)
        {
            int limit = Mathf.Clamp(maxFps, 10, 60);
            return sourceFps > 1d && sourceFps < limit ? sourceFps : limit;
        }

        public static int VideoBitrateForBudget(float budgetMb, double durationSeconds, int audioKbps)
        {
            if (durationSeconds <= 0d) return 0;
            double totalKbits = Math.Max(0d, budgetMb) * 8000d;
            double reserveKbits = Math.Max(64d, totalKbits * ContainerOverhead);
            return (int)Math.Floor((totalKbits - reserveKbits) / durationSeconds - audioKbps);
        }

        private static int Even(double value)
        {
            int rounded = (int)Math.Round(value);
            return Math.Max(2, rounded - (rounded & 1));
        }

        // ---------- command lines ----------

        /// <summary>Two-pass encode, then (optional) decode checks. Every step reports progress.</summary>
        public List<PAFfmpegStep> BuildSteps(string passLogBase)
        {
            string ffmpeg = PAFfmpegLocator.FfmpegPath;
            var steps = new List<PAFfmpegStep>
            {
                new PAFfmpegStep("Pass 1/2", ffmpeg, VideoArguments(1, passLogBase) + " -an -f mp4 " + PAFfmpegProcess.Quote(PAFfmpegProcess.NullDevice), 0.45f),
                new PAFfmpegStep("Pass 2/2", ffmpeg, VideoArguments(2, passLogBase) + SecondPassOutputs(), 0.45f)
            };

            if (Settings.verify)
            {
                steps.Add(new PAFfmpegStep("Decode check", ffmpeg, DecodeArguments(VideoPath, "-map 0:v:0 -an"), 0.07f));
                if (AudioPath != null)
                {
                    steps.Add(new PAFfmpegStep("Audio decode check", ffmpeg, DecodeArguments(AudioPath, "-map 0:a:0"), 0.03f));
                }
            }
            return steps;
        }

        private string VideoArguments(int pass, string passLogBase)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "-hide_banner -y -progress pipe:1 -nostats -i {0} -map 0:v:0 -vf {1} " +
                "-c:v libx264 -preset {2}{3} -profile:v baseline -level:v {4} -pix_fmt yuv420p " +
                "-refs 3 -bf 0 -b:v {5}k -maxrate {5}k -bufsize {6}k " +
                "-g {7} -keyint_min {7} -sc_threshold 0 -fps_mode cfr -pass {8} -passlogfile {9}",
                PAFfmpegProcess.Quote(InputPath),
                PAFfmpegProcess.Quote(VideoFilter()),
                X264Preset(Settings.speed),
                Settings.gameFootageTune ? " -tune animation" : string.Empty,
                FrameRate > 30.5d ? "3.2" : "3.1",
                VideoBitrateKbps,
                VideoBitrateKbps * 2,
                Math.Max(1, (int)Math.Round(FrameRate * 2d)),
                pass,
                PAFfmpegProcess.Quote(passLogBase));
        }

        private string VideoFilter()
        {
            string filter = string.Format(CultureInfo.InvariantCulture,
                "scale={0}:{1}:flags=lanczos+accurate_rnd+full_chroma_int", Size.x, Size.y);
            if (!Settings.keepAspect)
            {
                filter = string.Format(CultureInfo.InvariantCulture,
                    "scale={0}:{1}:force_original_aspect_ratio=decrease:flags=lanczos+accurate_rnd+full_chroma_int,pad={0}:{1}:(ow-iw)/2:(oh-ih)/2:color=black",
                    PadBox.x, PadBox.y);
            }
            filter += ",setsar=1";
            if (Settings.sharpen > 0.0001f)
            {
                filter += string.Format(CultureInfo.InvariantCulture, ",unsharp=5:5:{0:0.00}:3:3:0", Settings.sharpen);
            }
            return filter + string.Format(CultureInfo.InvariantCulture, ",fps={0:0.###}", FrameRate);
        }

        private string SecondPassOutputs()
        {
            bool embedAac = Source.HasAudio && Settings.audioMode == PAVideoAudioMode.EmbeddedAac;
            string audio = embedAac
                ? string.Format(CultureInfo.InvariantCulture, " -map 0:a:0 -c:a aac -b:a {0}k -ar 44100 -ac 2", AudioBitrateKbps)
                : " -an";

            string result = audio + " -map_metadata -1 -metadata:s:v:0 rotate=0 -movflags +faststart " + PAFfmpegProcess.Quote(VideoPath);
            if (AudioPath != null)
            {
                result += string.Format(CultureInfo.InvariantCulture,
                    " -map 0:a:0 -vn -c:a libmp3lame -b:a {0}k -ar 44100 -ac 2 -map_metadata -1 {1}",
                    AudioBitrateKbps, PAFfmpegProcess.Quote(AudioPath));
            }
            return result;
        }

        private static string DecodeArguments(string path, string map)
        {
            return "-hide_banner -v error -progress pipe:1 -nostats -i " + PAFfmpegProcess.Quote(path) + " " + map
                   + " -f null " + PAFfmpegProcess.Quote(PAFfmpegProcess.NullDevice);
        }

        private static string X264Preset(PAEncoderSpeed speed)
        {
            switch (speed)
            {
                case PAEncoderSpeed.Fast: return "medium";
                case PAEncoderSpeed.Balanced: return "slow";
                default: return "veryslow";
            }
        }
    }
}
#endif
