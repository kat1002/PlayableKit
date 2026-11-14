using System.Collections;
using UnityEngine;
using PlayableKit.Effects;
using PlayableKit.Tweening;
using PlayableKit.UI;

namespace PlayableKit.Flow
{
    /// <summary>
    /// Owns the playable's state machine, the survival timer and the end / CTA hand-off.
    /// This is a generic orchestrator: intro -> first tap -> survive-timer -> end (with a
    /// crash-flourish slow-mo unless it's a timeout) -> end card -> CTA. Subclass and override
    /// the On* hooks below to add game-specific behavior (traffic, scoring, whatever).
    /// </summary>
    public class PAGameManagerBase : MonoBehaviour
    {
        public static PAGameManagerBase Instance { get; protected set; }
        public static GameState State => Instance != null && Instance._session != null ? Instance._session.State : GameState.Intro;
        public static bool IsPlaying => State == GameState.Playing;
        public static float PlayElapsed => Instance != null && Instance._session != null ? Instance._session.PlayElapsed : 0f;

        [Header("Timing")]
        [Tooltip("Seconds of active play before OnMidpointReached() fires.")]
        public float sessionDuration = 25f;
        [Min(0f), Tooltip("Open the store if the player has not provided their first input after this many real-time seconds.")]
        public float introInactivityStoreDelay = 15f;
        [Tooltip("Delay after a crash sequence before the end card appears.")]
        public float endCardDelay = 0.2f;

        [Header("Crash flourish")]
        public float crashShakeAmplitude = 0.35f;
        public float crashShakeDuration = 0.25f;

        [Header("Scene references")]
        [SerializeField] protected PAGameUIController ui;
        [SerializeField] protected PACameraShake cameraShake;

        protected GameSessionState _session;
        protected bool _ending;

        protected virtual void Awake()
        {
            Instance = this;
            _session = new GameSessionState(sessionDuration);
            Application.targetFrameRate = 60;

            if (PALunaManager.Instance != null)
            {
                PALunaManager.Instance.StoreReturned += ReshowEndCard;
            }

            OnAwake();
        }

        protected virtual void Start()
        {
            if (ui != null) ui.ShowIntro();
        }

        /// <summary>Called by the first player tap.</summary>
        public virtual void BeginPlay()
        {
            if (_session == null || !_session.BeginPlay()) return;
            if (PALunaManager.instance != null) PALunaManager.instance.StartGame();
            if (ui != null) ui.HideIntro();
            OnBeginPlay();
        }

        protected virtual void Update()
        {
            if (_session == null) return;

            float unscaledDt = PALunaManager.ClampTickSeconds(Time.unscaledDeltaTime);
            if (_session.TickIntroInactivity(unscaledDt, introInactivityStoreDelay))
            {
                OpenStoreForIntroInactivity();
                return;
            }

            if (!_session.IsPlaying) return;

            if (_session.Tick(Time.deltaTime))
                OnMidpointReached();

            if (ui != null) ui.SetProgress(_session.Progress);

            OnPlayingTick();
        }

        private void OpenStoreForIntroInactivity()
        {
            // Keep the playable lifecycle ordered even though no gameplay input occurred.
            if (PALunaManager.instance != null) PALunaManager.instance.StartGame();
            EndGame(EndReason.Timeout);
            if (PALunaManager.instance != null) PALunaManager.instance.GoToStore();
        }

        /// <summary>Re-activates the end card if the player returns from the store mid-CTA.</summary>
        public virtual void ReshowEndCard()
        {
            if (_session != null && _session.IsEnded && ui != null)
            {
                ui.ShowEndCard();
            }
        }

        /// <summary>External entry point for safety timeout or forced completion.</summary>
        public void ForceEnd(EndReason reason = EndReason.Timeout)
        {
            EndGame(reason);
        }

        /// <summary>Single entry point for every loss/win/timeout condition.</summary>
        public virtual void EndGame(EndReason reason)
        {
            if (_ending || _session == null || !_session.EndGame(reason)) return;
            _ending = true;

            OnEndGameBegin(reason);
            if (cameraShake != null) cameraShake.SetContinuousIntensity(0f);

            if (reason != EndReason.Timeout)
            {
                OnBeforeCrashSequence(reason);
                if (cameraShake != null) cameraShake.Shake(crashShakeAmplitude, crashShakeDuration);
                StartCoroutine(EndSequence(reason));
            }
            else
            {
                if (PALunaManager.instance != null) PALunaManager.instance.EndGame();
                if (ui != null) ui.ShowEndCard();
                OnEndGameAfter(reason);
            }
        }

        private IEnumerator EndSequence(EndReason reason)
        {
            Time.timeScale = 0.35f;
            float elapsed = 0f;
            while (elapsed < 0.25f)
            {
                elapsed += PALunaManager.ClampTickSeconds(Time.unscaledDeltaTime);
                yield return null;
            }
            Time.timeScale = 1f;

            yield return new WaitForSeconds(endCardDelay);

            if (PALunaManager.instance != null) PALunaManager.instance.EndGame();
            if (ui != null) ui.ShowEndCard();
            OnEndGameAfter(reason);
        }

        protected virtual void OnDestroy()
        {
            Time.timeScale = 1f;
            if (PALunaManager.Instance != null)
            {
                PALunaManager.Instance.StoreReturned -= ReshowEndCard;
            }
            if (Instance == this) Instance = null;
        }

        // ---- Extension points for game-specific subclasses ----------------------------
        /// <summary>Wire scene refs / auto-find components here (runs at the end of Awake()).</summary>
        protected virtual void OnAwake() { }
        /// <summary>Kick off game-specific systems (spawners, engine audio, etc.) on the first tap.</summary>
        protected virtual void OnBeginPlay() { }
        /// <summary>Runs every frame while playing, after the session tick/progress update.</summary>
        protected virtual void OnPlayingTick() { }
        /// <summary>Fires once when PlayElapsed crosses sessionDuration (e.g. spawn a finale).</summary>
        protected virtual void OnMidpointReached() { }
        /// <summary>Runs once, immediately, for every end reason (stop loops/effects here).</summary>
        protected virtual void OnEndGameBegin(EndReason reason) { }
        /// <summary>Runs once for non-timeout ends, right before the crash slow-mo starts.</summary>
        protected virtual void OnBeforeCrashSequence(EndReason reason) { }
        /// <summary>Runs once the end card is showing, for both timeout and crash paths.</summary>
        protected virtual void OnEndGameAfter(EndReason reason) { }
    }
}
