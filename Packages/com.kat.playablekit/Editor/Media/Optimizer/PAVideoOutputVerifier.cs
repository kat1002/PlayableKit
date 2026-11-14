#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;

namespace PlayableKit.Editor.Media
{
    public struct PAVerifyResult
    {
        public bool Passed;
        public string Message;
        public long VideoBytes;
        public long AudioBytes;
        public bool OverBudget;

        public long TotalBytes
        {
            get { return VideoBytes + AudioBytes; }
        }
    }

    /// <summary>Checks an encoded file against its plan: browser-safe H.264 (Baseline, no B-frames, yuv420p), size, fps and budget.</summary>
    public static class PAVideoOutputVerifier
    {
        public static PAVerifyResult Verify(PAVideoEncodePlan plan)
        {
            var result = new PAVerifyResult
            {
                VideoBytes = SizeOf(plan.VideoPath),
                AudioBytes = SizeOf(plan.AudioPath)
            };
            result.OverBudget = plan.Settings.useSizeBudget && result.TotalBytes > plan.BudgetBytes;

            if (result.VideoBytes == 0)
            {
                result.Message = "No output file was written.";
                return result;
            }
            if (!plan.Settings.verify)
            {
                result.Passed = true;
                return result;
            }

            string error;
            PAMediaInfo output = PAMediaProbe.Probe(plan.VideoPath, out error);
            result.Message = output == null ? error : Problem(plan, output);
            result.Passed = result.Message == null;
            if (result.Passed)
            {
                result.Message = string.Format(CultureInfo.InvariantCulture, "Verified: {0}, {1}x{2}, {3:0.##} fps, no B-frames.",
                    output.VideoProfile, output.Width, output.Height, output.FrameRate);
            }
            return result;
        }

        private static string Problem(PAVideoEncodePlan plan, PAMediaInfo output)
        {
            if (string.IsNullOrEmpty(output.VideoProfile) || output.VideoProfile.IndexOf("Baseline", StringComparison.OrdinalIgnoreCase) < 0)
                return "Profile is " + output.VideoProfile + ", expected Baseline.";
            if (output.Width != plan.Size.x || output.Height != plan.Size.y)
                return "Output is " + output.Width + "x" + output.Height + ", expected " + plan.Size.x + "x" + plan.Size.y + ".";
            if (output.HasBFrames)
                return "Output still has B-frames.";
            if (output.PixelFormat != "yuv420p")
                return "Pixel format is " + output.PixelFormat + ", expected yuv420p.";
            if (Math.Abs(output.FrameRate - plan.FrameRate) > 0.05d)
                return "Frame rate is " + output.FrameRate.ToString("0.##", CultureInfo.InvariantCulture) + ", expected "
                       + plan.FrameRate.ToString("0.##", CultureInfo.InvariantCulture) + ".";
            return null;
        }

        private static long SizeOf(string path)
        {
            return !string.IsNullOrEmpty(path) && File.Exists(path) ? new FileInfo(path).Length : 0L;
        }
    }
}
#endif
