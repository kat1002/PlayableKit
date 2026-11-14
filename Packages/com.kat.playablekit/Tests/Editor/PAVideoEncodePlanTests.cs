using NUnit.Framework;
using PlayableKit.Editor.Media;
using UnityEngine;

namespace PlayableKit.Tests
{
    public class PAVideoEncodePlanTests
    {
        [Test]
        public void TargetBox_FollowsOrientation()
        {
            Assert.AreEqual(new Vector2Int(1280, 720), PAVideoEncodePlan.TargetBox(true, PAVideoResolution.Hd720));
            Assert.AreEqual(new Vector2Int(720, 1280), PAVideoEncodePlan.TargetBox(false, PAVideoResolution.Hd720));
            Assert.AreEqual(new Vector2Int(540, 960), PAVideoEncodePlan.TargetBox(false, PAVideoResolution.Lite540));
        }

        [Test]
        public void FitInside_KeepsAspectWithEvenSides()
        {
            // Tall phone capture (9:19.5) into the 720x1280 box: height-limited, no bars.
            Vector2Int size = PAVideoEncodePlan.FitInside(1080, 2340, new Vector2Int(720, 1280));
            Assert.AreEqual(1280, size.y);
            Assert.AreEqual(0, size.x % 2);
            Assert.AreEqual(590, size.x);
        }

        [Test]
        public void FitInside_NeverUpscales()
        {
            Assert.AreEqual(new Vector2Int(640, 360), PAVideoEncodePlan.FitInside(640, 360, new Vector2Int(1280, 720)));
        }

        [Test]
        public void OutputFrameRate_KeepsLowerSourceRate()
        {
            Assert.AreEqual(24d, PAVideoEncodePlan.OutputFrameRate(24d, 30), 1e-9);
            Assert.AreEqual(30d, PAVideoEncodePlan.OutputFrameRate(60d, 30), 1e-9);
            Assert.AreEqual(30d, PAVideoEncodePlan.OutputFrameRate(0d, 30), 1e-9);
        }

        [Test]
        public void VideoBitrateForBudget_LeavesRoomForAudioAndContainer()
        {
            // 2.4 MB, 15 s, 96 kbps audio: (19200 - 288) / 15 - 96 = 1164.8 → 1164
            Assert.AreEqual(1164, PAVideoEncodePlan.VideoBitrateForBudget(2.4f, 15d, 96));
            Assert.AreEqual(0, PAVideoEncodePlan.VideoBitrateForBudget(2.4f, 0d, 96));
        }

        [Test]
        public void RefitBitrate_LowersBitrateWhenOverBudget()
        {
            var source = new PAMediaInfo { Width = 1920, Height = 1080, DurationSeconds = 15d, FrameRate = 30d, HasVideo = true };
            var settings = new PAVideoEncodeSettings();
            var plan = new PAVideoEncodePlan(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pa_plan_test.mp4"), source, settings);
            int before = plan.VideoBitrateKbps;

            Assert.IsTrue(plan.RefitBitrate((long)(plan.BudgetBytes * 1.2d), 0));
            Assert.Less(plan.VideoBitrateKbps, before);
            Assert.IsFalse(plan.RefitBitrate(1, 0), "Under-budget result must not raise the bitrate.");
        }
    }
}
