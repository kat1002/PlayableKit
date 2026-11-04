using NUnit.Framework;

namespace LunaManagerTests.EditorTests
{
    public sealed class LunaManagerSessionTests
    {
        private static LunaManager.Session Make(bool useEnd = false, float endSeconds = 30f,
            bool useNoInteraction = false, float noInteractionSeconds = 10f,
            bool useTapCount = false, int tapsToStore = 3, bool switchAfterStore = true, int tapsAfterStore = 1)
        {
            return new LunaManager.Session(useEnd, endSeconds, useNoInteraction, noInteractionSeconds,
                useTapCount, tapsToStore, switchAfterStore, tapsAfterStore);
        }

        [Test]
        public void Start_ReturnsTrueOnlyOnce()
        {
            LunaManager.Session session = Make();
            Assert.That(session.Start(), Is.True);
            Assert.That(session.Start(), Is.False);
            Assert.That(session.IsStarted, Is.True);
        }

        [Test]
        public void End_ReturnsTrueOnlyOnce()
        {
            LunaManager.Session session = Make();
            Assert.That(session.End(), Is.True);
            Assert.That(session.End(), Is.False);
            Assert.That(session.IsEnded, Is.True);
        }

        [Test]
        public void GoToStore_OpensStoreWithoutEnding()
        {
            LunaManager.Session session = Make();
            session.Start();
            Assert.That(session.TryOpenStore(1f), Is.True);
            Assert.That(session.HasOpenedStore, Is.True);
            Assert.That(session.IsEnded, Is.False);
        }

        [Test]
        public void EndGameAfterEnd_OnlyReopensStore()
        {
            LunaManager.Session session = Make();
            session.Start();
            Assert.That(session.End(), Is.True);
            Assert.That(session.TryOpenStore(1f), Is.True);
            Assert.That(session.End(), Is.False);
            Assert.That(session.TryOpenStore(2f), Is.True);
        }

        [Test]
        public void TryOpenStore_BlocksRepeatInsideGuard()
        {
            LunaManager.Session session = Make();
            Assert.That(session.TryOpenStore(5f), Is.True);
            Assert.That(session.TryOpenStore(5.1f), Is.False);
            Assert.That(session.TryOpenStore(5.3f), Is.True);
        }

        [Test]
        public void TryOpenStore_FirstCallAtTimeZeroIsAllowed()
        {
            Assert.That(Make().TryOpenStore(0f), Is.True);
        }

        [Test]
        public void EndTimer_ExpiresOnceAndOnlyWhenStarted()
        {
            LunaManager.Session session = Make(true, 2f);
            Assert.That(session.Tick(5f), Is.EqualTo(LunaManager.TickResult.None));
            session.Start();
            Assert.That(session.Tick(1f), Is.EqualTo(LunaManager.TickResult.None));
            Assert.That(session.Tick(1f), Is.EqualTo(LunaManager.TickResult.EndTimerExpired));
            Assert.That(session.Tick(5f), Is.EqualTo(LunaManager.TickResult.None));
        }

        [Test]
        public void EndTimer_DisabledNeverExpires()
        {
            LunaManager.Session session = Make();
            session.Start();
            Assert.That(session.Tick(1000f), Is.EqualTo(LunaManager.TickResult.None));
        }

        [Test]
        public void EndTimer_StoppedByEnd()
        {
            LunaManager.Session session = Make(true, 2f);
            session.Start();
            session.End();
            Assert.That(session.Tick(5f), Is.EqualTo(LunaManager.TickResult.None));
        }

        [Test]
        public void StartEndTimer_RestartsCountdown()
        {
            LunaManager.Session session = Make(true, 2f);
            session.Start();
            session.Tick(1.5f);
            session.StartEndTimer(3f);
            Assert.That(session.Tick(2f), Is.EqualTo(LunaManager.TickResult.None));
            Assert.That(session.Tick(1f), Is.EqualTo(LunaManager.TickResult.EndTimerExpired));
        }

        [Test]
        public void StartEndTimer_AfterExpiryRunsAgain()
        {
            LunaManager.Session session = Make(true, 1f);
            session.Start();
            Assert.That(session.Tick(1f), Is.EqualTo(LunaManager.TickResult.EndTimerExpired));
            session.StartEndTimer(1f);
            Assert.That(session.Tick(1f), Is.EqualTo(LunaManager.TickResult.EndTimerExpired));
        }

        [Test]
        public void StartEndTimer_SetBeforeStartIsKeptWhenStarting()
        {
            LunaManager.Session session = Make(true, 30f);
            session.StartEndTimer(1f);
            session.Start();
            Assert.That(session.Tick(1f), Is.EqualTo(LunaManager.TickResult.EndTimerExpired));
        }

        [Test]
        public void NoInteraction_ExpiresOnceWhenNotStarted()
        {
            LunaManager.Session session = Make(false, 30f, true, 3f);
            Assert.That(session.Tick(2f), Is.EqualTo(LunaManager.TickResult.None));
            Assert.That(session.Tick(1f), Is.EqualTo(LunaManager.TickResult.NoInteractionExpired));
            Assert.That(session.Tick(5f), Is.EqualTo(LunaManager.TickResult.None));
        }

        [Test]
        public void NoInteraction_CancelledByStart()
        {
            LunaManager.Session session = Make(false, 30f, true, 3f);
            session.Start();
            Assert.That(session.Tick(10f), Is.EqualTo(LunaManager.TickResult.None));
        }

        [Test]
        public void NoInteraction_DisabledNeverExpires()
        {
            LunaManager.Session session = Make();
            Assert.That(session.Tick(1000f), Is.EqualTo(LunaManager.TickResult.None));
        }

        [Test]
        public void NoInteraction_EndGameResultEndsWithoutStarting()
        {
            LunaManager.Session session = Make(false, 30f, true, 3f);
            Assert.That(session.Tick(3f), Is.EqualTo(LunaManager.TickResult.NoInteractionExpired));
            Assert.That(session.End(), Is.True);
            Assert.That(session.IsEnded, Is.True);
            Assert.That(session.IsStarted, Is.False);
        }

        [Test]
        public void PlayTime_CountsFromStartUntilEnd()
        {
            LunaManager.Session session = Make();
            session.Tick(1f);
            Assert.That(session.PlayTime, Is.EqualTo(0f));
            session.Start();
            session.Tick(2f);
            session.End();
            session.Tick(2f);
            Assert.That(session.PlayTime, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void ClampTickSeconds_CapsAtQuarterSecond()
        {
            Assert.That(LunaManager.ClampTickSeconds(0.1f), Is.EqualTo(0.1f));
            Assert.That(LunaManager.ClampTickSeconds(5f), Is.EqualTo(0.25f));
            Assert.That(LunaManager.ClampTickSeconds(-1f), Is.EqualTo(0f));
        }

        [Test]
        public void RegisterTap_TriggersAtThresholdAndSwitchesRuleAfterFirstComplete()
        {
            LunaManager.Session session = Make(false, 30f, false, 10f, true, 3, true, 1);
            Assert.That(session.RegisterTap(), Is.False);
            Assert.That(session.RegisterTap(), Is.False);
            Assert.That(session.RegisterTap(), Is.True);
            // After reaching 3 taps, rule switches to 1 tap
            Assert.That(session.TapsToStore, Is.EqualTo(1));
            Assert.That(session.RegisterTap(), Is.True);
        }
    }
}
