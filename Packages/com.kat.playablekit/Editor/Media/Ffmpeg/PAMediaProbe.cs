#if UNITY_EDITOR
using System;
using System.Globalization;
using UnityEngine;

namespace PlayableKit.Editor.Media
{
    /// <summary>What ffprobe reports about a media file. Width/height are display size (rotation applied).</summary>
    public sealed class PAMediaInfo
    {
        public int Width;
        public int Height;
        public double DurationSeconds;
        public double FrameRate;
        public bool HasVideo;
        public bool HasAudio;
        public string VideoProfile;
        public string PixelFormat;
        public bool HasBFrames;

        public bool IsLandscape
        {
            get { return Width >= Height; }
        }

        public string Describe()
        {
            return Width + "x" + Height + "  " + DurationSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s  "
                   + FrameRate.ToString("0.##", CultureInfo.InvariantCulture) + " fps  " + (HasAudio ? "audio" : "no audio");
        }
    }

    /// <summary>Reads <see cref="PAMediaInfo"/> with one ffprobe call (JSON output).</summary>
    public static class PAMediaProbe
    {
        private const int TimeoutMs = 20000;

        private const string Entries =
            "stream=codec_type,width,height,avg_frame_rate,profile,pix_fmt,has_b_frames:stream_tags=rotate:stream_side_data=rotation:format=duration";

        public static PAMediaInfo Probe(string path, out string error)
        {
            PAProcessResult result = PAFfmpegProcess.Run(
                PAFfmpegLocator.FfprobePath,
                "-v error -show_entries " + Entries + " -of json " + PAFfmpegProcess.Quote(path),
                TimeoutMs);

            if (!result.Succeeded)
            {
                error = "ffprobe could not read the file: " + result.ErrorSummary;
                return null;
            }

            ProbeJson json;
            try
            {
                json = JsonUtility.FromJson<ProbeJson>(result.Output);
            }
            catch (ArgumentException e)
            {
                error = "Unexpected ffprobe output: " + e.Message;
                return null;
            }

            PAMediaInfo info = Convert(json);
            error = info.HasVideo || info.HasAudio ? null : "No video or audio stream found.";
            return error == null ? info : null;
        }

        /// <summary>Parses "30000/1001" or "30". Returns 0 for "0/0" or junk.</summary>
        public static double ParseRate(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0d;

            int slash = value.IndexOf('/');
            double numerator;
            if (slash < 0)
            {
                return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out numerator) ? numerator : 0d;
            }

            double denominator;
            if (double.TryParse(value.Substring(0, slash), NumberStyles.Float, CultureInfo.InvariantCulture, out numerator) &&
                double.TryParse(value.Substring(slash + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out denominator) &&
                denominator > 0d)
            {
                return numerator / denominator;
            }
            return 0d;
        }

        private static PAMediaInfo Convert(ProbeJson json)
        {
            var info = new PAMediaInfo();
            if (json == null) return info;

            if (json.format != null)
            {
                double.TryParse(json.format.duration, NumberStyles.Float, CultureInfo.InvariantCulture, out info.DurationSeconds);
            }
            if (json.streams == null) return info;

            for (int i = 0; i < json.streams.Length; i++)
            {
                StreamJson stream = json.streams[i];
                if (stream.codec_type == "audio")
                {
                    info.HasAudio = true;
                }
                else if (stream.codec_type == "video" && !info.HasVideo && stream.width > 0)
                {
                    info.HasVideo = true;
                    bool quarterTurn = Math.Abs(Rotation(stream)) % 180 == 90;
                    info.Width = quarterTurn ? stream.height : stream.width;
                    info.Height = quarterTurn ? stream.width : stream.height;
                    info.FrameRate = ParseRate(stream.avg_frame_rate);
                    info.VideoProfile = stream.profile;
                    info.PixelFormat = stream.pix_fmt;
                    info.HasBFrames = stream.has_b_frames > 0;
                }
            }
            return info;
        }

        private static int Rotation(StreamJson stream)
        {
            if (stream.side_data_list != null)
            {
                for (int i = 0; i < stream.side_data_list.Length; i++)
                {
                    if (stream.side_data_list[i].rotation != 0) return stream.side_data_list[i].rotation;
                }
            }

            int rotate;
            if (stream.tags != null && int.TryParse(stream.tags.rotate, NumberStyles.Integer, CultureInfo.InvariantCulture, out rotate))
            {
                return rotate;
            }
            return 0;
        }

        // JsonUtility mirrors of the ffprobe JSON. Field names must match ffprobe's keys.
#pragma warning disable 0649
        [Serializable] private sealed class ProbeJson { public StreamJson[] streams; public FormatJson format; }
        [Serializable] private sealed class FormatJson { public string duration; }
        [Serializable] private sealed class TagsJson { public string rotate; }
        [Serializable] private sealed class SideDataJson { public int rotation; }

        [Serializable]
        private sealed class StreamJson
        {
            public string codec_type;
            public int width;
            public int height;
            public string avg_frame_rate;
            public string profile;
            public string pix_fmt;
            public int has_b_frames;
            public TagsJson tags;
            public SideDataJson[] side_data_list;
        }
#pragma warning restore 0649
    }
}
#endif
