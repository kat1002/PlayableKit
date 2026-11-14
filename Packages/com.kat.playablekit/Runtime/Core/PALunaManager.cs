using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using PlayableKit.Analytics;

namespace PlayableKit
{
    // What to do when the player has not touched the playable in time.
    public enum NoInteractionResult
    {
        KeepPlayable,
        EndGame
    }

    /// <summary>
    /// Unified Playable lifecycle bridge combining timer, no-interaction timeout, optional tap-count-to-store,
    /// store-return detection, and Luna Lifecycle/Playable/Analytics integration.
    /// Place one instance in each playable scene.
    /// GoToStore visits the store and keeps playing; EndGame is the single final exit.
    /// </summary>
    [DisallowMultipleComponent]
    public class PALunaManager : MonoBehaviour
    {
        // One player action can reach the store from several listeners.
        private const float InstallRepeatGuardSeconds = 0.25f;

        // Longest step a timer takes in one frame, so a tab or store return does not jump it.
        public const float MaxTickSeconds = 0.25f;

        public enum TickResult
        {
            None,
            EndTimerExpired,
            NoInteractionExpired
        }

        // Pure lifecycle state. No Luna or Time calls, so EditMode tests can drive it.
        public sealed class Session
        {
            private readonly bool m_UseEndTimer;
            private readonly float m_EndTimerSeconds;
            private readonly bool m_UseNoInteractionTimeout;
            private readonly bool m_UseTapCountToStore;
            private readonly bool m_SwitchTapRuleAfterStore;
            private readonly int m_TapsAfterFirstStore;

            private int m_TapsToStore;
            private int m_TapCount;
            private float m_NoInteractionLeft;
            private bool m_NoInteractionFired;
            private bool m_EndTimerRunning;
            private float m_EndTimerLeft;
            private float m_LastInstallTime;
            private bool m_HasInstallTime;

            public bool IsStarted { get; private set; }
            public bool HasStarted { get { return IsStarted; } }
            public bool IsEnded { get; private set; }
            public bool HasEnded { get { return IsEnded; } }
            public bool HasOpenedStore { get; private set; }
            public float PlayTime { get; private set; }
            public float RemainingSeconds { get { return m_EndTimerLeft; } }
            public int TapCount { get { return m_TapCount; } }
            public int TapsToStore { get { return m_TapsToStore; } }

            public Session(
                bool useEndTimer,
                float endTimerSeconds,
                bool useNoInteractionTimeout,
                float noInteractionTimeoutSeconds,
                bool useTapCountToStore = false,
                int tapsToStore = 6,
                bool switchTapRuleAfterStore = true,
                int tapsAfterFirstStore = 1)
            {
                m_UseEndTimer = useEndTimer;
                m_EndTimerSeconds = Mathf.Max(0f, endTimerSeconds);
                m_UseNoInteractionTimeout = useNoInteractionTimeout;
                m_NoInteractionLeft = Mathf.Max(0f, noInteractionTimeoutSeconds);
                m_EndTimerLeft = m_EndTimerSeconds;

                m_UseTapCountToStore = useTapCountToStore;
                m_TapsToStore = Mathf.Max(1, tapsToStore);
                m_SwitchTapRuleAfterStore = switchTapRuleAfterStore;
                m_TapsAfterFirstStore = Mathf.Max(1, tapsAfterFirstStore);
            }

            // Returns true only the first time. Cancels the no-interaction timer.
            public bool Start()
            {
                if (IsStarted) return false;
                IsStarted = true;
                if (m_UseEndTimer && !m_EndTimerRunning) StartEndTimer(m_EndTimerSeconds);
                return true;
            }

            // Returns true only the first time. Stops both timers.
            public bool End()
            {
                if (IsEnded) return false;
                IsEnded = true;
                m_EndTimerRunning = false;
                return true;
            }

            // False when the previous store open is too recent.
            public bool TryOpenStore(float now)
            {
                if (m_HasInstallTime && now - m_LastInstallTime < InstallRepeatGuardSeconds) return false;
                HasOpenedStore = true;
                m_HasInstallTime = true;
                m_LastInstallTime = now;

                if (m_UseTapCountToStore)
                {
                    m_TapCount = 0;
                    if (m_SwitchTapRuleAfterStore)
                    {
                        m_TapsToStore = m_TapsAfterFirstStore;
                    }
                }
                return true;
            }

            /// <summary>
            /// Increments the tap counter when tap-to-store is enabled.
            /// Returns true on the tap that reaches the current threshold.
            /// </summary>
            public bool RegisterTap()
            {
                if (!m_UseTapCountToStore || IsEnded) return false;
                m_TapCount++;
                if (m_TapCount < m_TapsToStore) return false;
                m_TapCount = 0;
                if (m_SwitchTapRuleAfterStore)
                {
                    m_TapsToStore = m_TapsAfterFirstStore;
                }
                return true;
            }

            public void SetTapsToStore(int newTapsToStore)
            {
                m_TapsToStore = Mathf.Max(1, newTapsToStore);
                m_TapCount = 0;
            }

            // (Re)starts the end timer. It only counts down while started and not ended.
            public void StartEndTimer(float seconds)
            {
                m_EndTimerRunning = true;
                m_EndTimerLeft = Mathf.Max(0f, seconds);
            }

            public TickResult Tick(float dt)
            {
                if (IsEnded || dt <= 0f) return TickResult.None;

                if (!IsStarted)
                {
                    if (!m_UseNoInteractionTimeout || m_NoInteractionFired) return TickResult.None;
                    m_NoInteractionLeft = Mathf.Max(0f, m_NoInteractionLeft - dt);
                    if (m_NoInteractionLeft > 0f) return TickResult.None;
                    m_NoInteractionFired = true;
                    return TickResult.NoInteractionExpired;
                }

                PlayTime += dt;
                if (!m_EndTimerRunning) return TickResult.None;
                m_EndTimerLeft = Mathf.Max(0f, m_EndTimerLeft - dt);
                if (m_EndTimerLeft > 0f) return TickResult.None;
                m_EndTimerRunning = false;
                return TickResult.EndTimerExpired;
            }
        }

        // Caps one frame's step for the timers.
        public static float ClampTickSeconds(float dt)
        {
            return Mathf.Clamp(dt, 0f, MaxTickSeconds);
        }

        [Header("End Timer")]
        [SerializeField] private bool m_UseEndTimer = true;
        [SerializeField] private bool m_EndOnTimer = true;
        [SerializeField] private float currentEndTime = 40f;

        [Header("First Interaction & Idle Timeout")]
        [SerializeField] private bool m_StartOnFirstPointerDown = false;
        [SerializeField] private bool m_UseNoInteractionTimeout = false;
        [SerializeField, Min(1f)] private float m_NoInteractionTimeoutSeconds = 10f;
        [SerializeField] private NoInteractionResult m_NoInteractionResult = NoInteractionResult.KeepPlayable;

        [Header("Optional Tap Count To Store")]
        [Tooltip("When enabled, automatically counts pointer downs and triggers GoToStore / fullscreenBtn when m_TapsToStore is reached.")]
        [SerializeField] private bool m_UseTapCountToStore = false;
        [SerializeField] private bool m_AutoCountGlobalPointerDown = true;
        [SerializeField, Min(1)] private int m_TapsToStore = 6;
        [SerializeField] private bool m_SwitchTapRuleAfterStore = true;
        [SerializeField, Min(1)] private int m_TapsAfterFirstStore = 1;
        [SerializeField] private bool m_OpenStoreImmediatelyOnTapLimit = false;
        [SerializeField] private GameObject m_FullscreenCtaButton;

        [Header("Events")]
        public UnityEvent onEndTimeEventReached = new UnityEvent();
        public UnityEvent onNoInteractionTimeout = new UnityEvent();
        public UnityEvent onTapLimitReached = new UnityEvent();

        public event Action GameplayStarted;
        public event Action GameplayEnded;
        public event Action StoreVisited;
        public event Action StoreReturned;
        public event Action EndTimerReached;
        public event Action NoInteractionTimedOut;
        public event Action TapLimitReached;
        public event Action<bool> PauseChanged;
        public event Action<bool> MuteChanged;

        public static PALunaManager instance;
        public static PALunaManager Instance { get { return instance; } }

        private Session m_Session;
        private int m_LastTapFrame = -1;
        private bool m_LeftForStore;
        private float m_VolumeBeforePause = 1f;

        public bool IsStarted { get { return GetSession().IsStarted; } }
        public bool HasStarted { get { return IsStarted; } }
        public bool IsEnded { get { return GetSession().IsEnded; } }
        public bool HasEnded { get { return IsEnded; } }
        public bool HasOpenedStore { get { return GetSession().HasOpenedStore; } }
        public bool IsStoreVisited { get { return HasOpenedStore; } }
        public float PlayTime { get { return GetSession().PlayTime; } }
        public float RemainingSeconds { get { return GetSession().RemainingSeconds; } }
        public int TapCount { get { return GetSession().TapCount; } }
        public bool IsPaused { get; private set; }
        public bool IsMuted { get; private set; }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            GetSession();

            if (m_FullscreenCtaButton != null)
            {
                m_FullscreenCtaButton.SetActive(false);
            }

    #if UNITY_LUNA
            if (EventSystem.current != null && EventSystem.current.GetComponent<StandaloneInputModule>() == null)
            {
                EventSystem.current.gameObject.AddComponent<StandaloneInputModule>();
            }
    #endif

            PlayableKit.Analytics.PlayableAnalytics.AddSink(LunaAnalyticsSink.Instance);
        }

        private void OnEnable()
        {
            Luna.Unity.LifeCycle.OnPause += HandlePause;
            Luna.Unity.LifeCycle.OnResume += HandleResume;
            Luna.Unity.LifeCycle.OnMute += HandleMute;
            Luna.Unity.LifeCycle.OnUnmute += HandleUnmute;
        }

        private void OnDisable()
        {
            Luna.Unity.LifeCycle.OnPause -= HandlePause;
            Luna.Unity.LifeCycle.OnResume -= HandleResume;
            Luna.Unity.LifeCycle.OnMute -= HandleMute;
            Luna.Unity.LifeCycle.OnUnmute -= HandleUnmute;
        }

        private void OnDestroy()
        {
            PlayableKit.Analytics.PlayableAnalytics.RemoveSink(LunaAnalyticsSink.Instance);
            if (instance == this) instance = null;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && HasOpenedStore)
            {
                m_LeftForStore = true;
            }
            else if (hasFocus && m_LeftForStore)
            {
                m_LeftForStore = false;
                if (StoreReturned != null) StoreReturned();
            }
        }

        private Session GetSession()
        {
            if (m_Session == null)
            {
                m_Session = new Session(
                    m_UseEndTimer,
                    currentEndTime,
                    m_UseNoInteractionTimeout,
                    m_NoInteractionTimeoutSeconds,
                    m_UseTapCountToStore,
                    m_TapsToStore,
                    m_SwitchTapRuleAfterStore,
                    m_TapsAfterFirstStore);
            }
            return m_Session;
        }

        /// <summary>
        /// Overrides the auto-store delay. Must be called before <see cref="StartGame"/> (or restarts the end timer if already running).
        /// </summary>
        public static void SetEndTime(float seconds)
        {
            if (instance != null && seconds > 0f)
            {
                instance.currentEndTime = seconds;
                instance.StartEndTimer(seconds);
            }
        }

        /// <summary>
        /// Logs a custom Luna Analytics event.
        /// </summary>
        public static void TrackEvent(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            Luna.Unity.Analytics.LogEvent(eventName, 0);
        }

        private void Update()
        {
            if (IsPaused) return;

            Session session = GetSession();
            bool pointerDown = PointerDownThisFrame();

            if (m_StartOnFirstPointerDown && !session.IsStarted && pointerDown)
            {
                StartGame();
            }

            if (m_UseTapCountToStore && m_AutoCountGlobalPointerDown && pointerDown && m_LastTapFrame != Time.frameCount)
            {
                m_LastTapFrame = Time.frameCount;
                RegisterTap();
            }

            TickResult result = session.Tick(ClampTickSeconds(Time.unscaledDeltaTime));
            if (result == TickResult.EndTimerExpired)
            {
                OnEndTimerExpired();
            }
            else if (result == TickResult.NoInteractionExpired)
            {
                if (m_NoInteractionResult == NoInteractionResult.EndGame) EndGame(false);
                if (onNoInteractionTimeout != null) onNoInteractionTimeout.Invoke();
                if (NoInteractionTimedOut != null) NoInteractionTimedOut();
            }
        }

        /// <summary>
        /// Increments the tap counter when <c>m_UseTapCountToStore</c> is enabled.
        /// Can also be called manually from UI buttons or gameplay interactions.
        /// </summary>
        public void RegisterTap()
        {
            StartGame();
            if (!GetSession().RegisterTap()) return;

            if (m_FullscreenCtaButton != null && !m_FullscreenCtaButton.activeSelf)
            {
                m_FullscreenCtaButton.SetActive(true);
            }

            if (onTapLimitReached != null) onTapLimitReached.Invoke();
            if (TapLimitReached != null) TapLimitReached();

            if (m_OpenStoreImmediatelyOnTapLimit)
            {
                GoToStore();
            }
        }

        public void SetTapsToStore(int newTapsToStore)
        {
            GetSession().SetTapsToStore(newTapsToStore);
            if (m_FullscreenCtaButton != null)
            {
                m_FullscreenCtaButton.SetActive(false);
            }
        }

        private void OnEndTimerExpired()
        {
            if (m_EndOnTimer)
            {
                // A player who already visited the store is not sent there again automatically.
                EndGame(!HasOpenedStore);
            }
            GameEventManager.RaisedEvent(GameEventManager.EventId.TimeEnd);
            if (onEndTimeEventReached != null) onEndTimeEventReached.Invoke();
            if (EndTimerReached != null) EndTimerReached();
        }

        // Call on the first player interaction. Safe to call repeatedly.
        public void StartGame()
        {
            if (!GetSession().Start()) return;
            Luna.Unity.LifeCycle.GameStarted();
            if (GameplayStarted != null) GameplayStarted();
        }

        public void StartPlayable()
        {
            StartGame();
        }

        public void RecordInteraction()
        {
            StartGame();
        }

        // (Re)starts the end timer at a creative-chosen moment.
        public void StartEndTimer(float seconds)
        {
            GetSession().StartEndTimer(seconds);
        }

        // Install/CTA buttons: open the store only, the game keeps running (no GameEnded).
        public void GoToStore()
        {
            StartGame();
            OpenStore();
        }

        // Final end: GameEnded() fires at most once; later calls only reopen the store.
        public void EndGame(bool openStore)
        {
            if (openStore) StartGame();
            if (GetSession().End())
            {
                Luna.Unity.LifeCycle.GameEnded();
                if (GameplayEnded != null) GameplayEnded();
            }
            if (openStore) OpenStore();
        }

        public void EndGame()
        {
            EndGame(false);
        }

        public void EndGameAndOpenStore()
        {
            EndGame(true);
        }

        public void InstallFullGame()
        {
            EndGameAndOpenStore();
        }

        public void ResetSession()
        {
            m_Session = new Session(
                m_UseEndTimer,
                currentEndTime,
                m_UseNoInteractionTimeout,
                m_NoInteractionTimeoutSeconds,
                m_UseTapCountToStore,
                m_TapsToStore,
                m_SwitchTapRuleAfterStore,
                m_TapsAfterFirstStore);
        }

        // Opens synchronously so it stays inside the player's click.
        // Uses Time.realtimeSinceStartup because Time.unscaledTime freezes while the store tab is open.
        private void OpenStore()
        {
            if (!GetSession().TryOpenStore(Time.realtimeSinceStartup)) return;
            if (m_FullscreenCtaButton != null && m_FullscreenCtaButton.activeSelf)
            {
                m_FullscreenCtaButton.SetActive(false);
            }
            Luna.Unity.Playable.InstallFullGame();
            if (StoreVisited != null) StoreVisited();
        }

        // Luna has no AudioListener.pause, so pausing silences via volume and resume restores the prior level
        // (which carries the in-game mute toggle owned by PAAudioManagerBase).
        private void HandlePause()
        {
            if (!IsPaused) m_VolumeBeforePause = AudioListener.volume;
            IsPaused = true;
            if (HasOpenedStore) m_LeftForStore = true;
            AudioListener.volume = 0f;
            if (PauseChanged != null) PauseChanged(true);
        }

        private void HandleResume()
        {
            IsPaused = false;
            AudioListener.volume = m_VolumeBeforePause;
            if (PauseChanged != null) PauseChanged(false);
            if (m_LeftForStore)
            {
                m_LeftForStore = false;
                if (StoreReturned != null) StoreReturned();
            }
        }

        private void HandleMute()
        {
            SetMuted(true);
        }

        private void HandleUnmute()
        {
            SetMuted(false);
        }

        public void SetMuted(bool muted)
        {
            IsMuted = muted;
            m_VolumeBeforePause = muted ? 0f : 1f;
            AudioListener.volume = IsPaused ? 0f : m_VolumeBeforePause;
            if (MuteChanged != null) MuteChanged(muted);
        }

        private static bool PointerDownThisFrame()
        {
            if (Input.GetMouseButtonDown(0)) return true;
            for (int i = 0; i < Input.touchCount; i++)
            {
                if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
            }
            return false;
        }
    }
}
