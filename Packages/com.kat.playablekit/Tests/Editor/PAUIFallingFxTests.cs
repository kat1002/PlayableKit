using NUnit.Framework;
using PlayableKit.Effects;
using UnityEngine;

namespace PlayableKit.Tests
{
    public sealed class PAUIFallingFxTests
    {
        [Test]
        public void ClampCount_ClampsToZeroAndMax()
        {
            Assert.That(PAUIFallingFx.ClampCount(-3), Is.EqualTo(0));
            Assert.That(PAUIFallingFx.ClampCount(40), Is.EqualTo(40));
            Assert.That(PAUIFallingFx.ClampCount(999), Is.EqualTo(PAUIFallingFx.MaxCount));
        }

        [Test]
        public void StreakAngle_RotatesDownOntoVelocity()
        {
            Assert.That(PAUIFallingFx.StreakAngle(new Vector2(0f, -100f)), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(PAUIFallingFx.StreakAngle(new Vector2(100f, -100f)), Is.EqualTo(45f).Within(1e-4f));
            Assert.That(PAUIFallingFx.StreakAngle(new Vector2(-100f, -100f)), Is.EqualTo(-45f).Within(1e-4f));
            Assert.That(PAUIFallingFx.StreakAngle(Vector2.zero), Is.EqualTo(0f));

            Vector2 v = new Vector2(-200f, -2000f);
            Vector3 dir = Quaternion.Euler(0f, 0f, PAUIFallingFx.StreakAngle(v)) * Vector3.down;
            Vector2 n = v.normalized;
            Assert.That(dir.x, Is.EqualTo(n.x).Within(1e-4f));
            Assert.That(dir.y, Is.EqualTo(n.y).Within(1e-4f));
        }

        [Test]
        public void SwayOffset_PeaksAtQuarterPeriod_ZeroAmpIsFlat()
        {
            Assert.That(PAUIFallingFx.SwayOffset(20f, 0.25f, 0f, 1f), Is.EqualTo(20f).Within(1e-4f));
            float[] ages = { 0f, 0.7f, 1f, 3.3f };
            for (int i = 0; i < ages.Length; i++)
            {
                Assert.That(PAUIFallingFx.SwayOffset(0f, 0.25f, 0f, ages[i]), Is.EqualTo(0f));
            }
        }

        [Test]
        public void WrapX_WrapsBothSides()
        {
            Assert.That(PAUIFallingFx.WrapX(130f, -100f, 100f), Is.EqualTo(-70f).Within(1e-4f));
            Assert.That(PAUIFallingFx.WrapX(-130f, -100f, 100f), Is.EqualTo(70f).Within(1e-4f));
            Assert.That(PAUIFallingFx.WrapX(50f, -100f, 100f), Is.EqualTo(50f).Within(1e-4f));
            Assert.That(PAUIFallingFx.WrapX(42f, 10f, 10f), Is.EqualTo(42f));
        }

        [Test]
        public void InitialY_PrewarmFillsArea_ElseStartsAboveTop()
        {
            Assert.That(PAUIFallingFx.InitialY(-960f, 960f, 40f, true, 0f), Is.EqualTo(-1000f).Within(1e-3f));
            Assert.That(PAUIFallingFx.InitialY(-960f, 960f, 40f, true, 1f), Is.EqualTo(1000f).Within(1e-3f));
            Assert.That(PAUIFallingFx.InitialY(-960f, 960f, 40f, false, 0f), Is.EqualTo(1000f).Within(1e-3f));
            Assert.That(PAUIFallingFx.InitialY(-960f, 960f, 40f, false, 1f), Is.EqualTo(3000f).Within(1e-3f));

            float[] us = { 0f, 0.5f, 1f };
            for (int i = 0; i < us.Length; i++)
            {
                Assert.That(PAUIFallingFx.InitialY(-960f, 960f, 40f, false, us[i]), Is.GreaterThanOrEqualTo(1000f));
            }
        }
    }
}
