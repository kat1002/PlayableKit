using NUnit.Framework;
using PlayableTemplate.Core;
using System.Reflection;

namespace PlayableTemplate.Core.Tests
{
    public class GameSessionStateTests
    {
        [Test]
        public void InitialState_IsIntro()
        {
            var session = new GameSessionState(20f);
            Assert.AreEqual(GameState.Intro, session.State);
            Assert.IsFalse(session.IsPlaying);
            Assert.IsFalse(session.IsEnded);
            Assert.AreEqual(0f, session.PlayElapsed);
            Assert.AreEqual(0f, session.Progress);
        }

        [Test]
        public void BeginPlay_TransitionsToIntroToPlaying()
        {
            var session = new GameSessionState(20f);
            bool started = session.BeginPlay();
            Assert.IsTrue(started);
            Assert.AreEqual(GameState.Playing, session.State);
            Assert.IsTrue(session.IsPlaying);

            // Double call returns false
            Assert.IsFalse(session.BeginPlay());
        }

        [Test]
        public void Tick_AdvancesPlayElapsedAndProgress()
        {
            var session = new GameSessionState(10f);
            session.BeginPlay();

            session.Tick(5f);
            Assert.AreEqual(5f, session.PlayElapsed, 0.001f);
            Assert.AreEqual(0.5f, session.Progress, 0.001f);

            session.Tick(5f);
            Assert.AreEqual(10f, session.PlayElapsed, 0.001f);
            Assert.AreEqual(1f, session.Progress, 0.001f);
        }

        [Test]
        public void Tick_ThresholdCrossed_TriggersMidpointOnce()
        {
            var session = new GameSessionState(10f);
            session.BeginPlay();

            bool trigger1 = session.Tick(9.9f);
            Assert.IsFalse(trigger1);
            Assert.IsFalse(session.IsMidpointTriggered);

            bool trigger2 = session.Tick(0.2f);
            Assert.IsTrue(trigger2);
            Assert.IsTrue(session.IsMidpointTriggered);

            // Subsequent ticks should not re-trigger
            bool trigger3 = session.Tick(1f);
            Assert.IsFalse(trigger3);
        }

        [Test]
        public void EndGame_TransitionsToEndedWithReason()
        {
            var session = new GameSessionState(25f);
            session.BeginPlay();

            bool ended = session.EndGame(EndReason.Loss);
            Assert.IsTrue(ended);
            Assert.AreEqual(GameState.Ended, session.State);
            Assert.AreEqual(EndReason.Loss, session.EndReason);
            Assert.IsTrue(session.IsEnded);

            // Double end returns false
            Assert.IsFalse(session.EndGame(EndReason.Win));
            Assert.AreEqual(EndReason.Loss, session.EndReason);
        }

        [Test]
        public void EndGame_WithTimeout_SetsCorrectReason()
        {
            var session = new GameSessionState(25f);
            session.BeginPlay();
            session.EndGame(EndReason.Timeout);
            Assert.AreEqual(EndReason.Timeout, session.EndReason);
        }

        [Test]
        public void IntroInactivity_TriggersAtThresholdOnlyOnce()
        {
            var session = new GameSessionState(25f);

            Assert.IsFalse(session.TickIntroInactivity(14.9f, 15f));
            Assert.IsTrue(session.TickIntroInactivity(0.1f, 15f));
            Assert.IsFalse(session.TickIntroInactivity(1f, 15f));
        }

        [Test]
        public void BeginPlay_CancelsIntroInactivity()
        {
            var session = new GameSessionState(25f);
            session.TickIntroInactivity(14.9f, 15f);

            session.BeginPlay();

            Assert.IsFalse(session.TickIntroInactivity(1f, 15f));
        }

        [Test]
        public void Crash_RequestsAutomaticStoreOnlyOnce()
        {
            var session = new GameSessionState(25f);
            session.BeginPlay();
            session.EndGame(EndReason.Loss);

            Assert.IsTrue(TryAutoStore(session, true));
            Assert.IsFalse(TryAutoStore(session, true));
        }

        [Test]
        public void Timeout_AndDisabledNetwork_DoNotRequestAutomaticStore()
        {
            var timeout = new GameSessionState(25f);
            timeout.BeginPlay();
            timeout.EndGame(EndReason.Timeout);
            Assert.IsFalse(TryAutoStore(timeout, true));

            var crash = new GameSessionState(25f);
            crash.BeginPlay();
            crash.EndGame(EndReason.Win);
            Assert.IsFalse(TryAutoStore(crash, false));
            Assert.IsTrue(TryAutoStore(crash, true));
        }

        private static bool TryAutoStore(GameSessionState session, bool enabled)
        {
            var method = typeof(GameSessionState).GetMethod("TryAutoOpenStore", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method, "The ended session needs an automatic store decision.");
            return (bool)method.Invoke(session, new object[] { enabled });
        }

    }
}
