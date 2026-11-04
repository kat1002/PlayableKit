using System;
using BlackGemsGlobal.SeatAway.GamePlayEvent;
using UnityEngine;
using UnityEngine.Events;

// What to do when the player has not touched the playable in time.
public enum NoInteractionResult
{
    KeepPlayable,
    EndGame
}

/// <summary>
/// Unified Playable lifecycle bridge combining the best features from squishy_pa, mukbang_pa, and Obby-Playable-Ads.
/// Place one instance in each playable scene.
/// GoToStore visits the store and keeps playing; EndGame is the single final exit.
/// </summary>
[DisallowMultipleComponent]
public class LunaManager : MonoBehaviour
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
        private float m_NoInteractionLeft;
        private bool m_NoInteractionFired;
        private bool m_EndTimerRunning;
        private float m_EndTimerLeft;
        private float m_LastInstallTime;

        public bool IsStarted { get; private set; }
        public bool HasStarted { get { return IsStarted; } }
        public bool IsEnded { get; private set; }
        public bool HasEnded { get { return IsEnded; } }
        public bool HasOpenedStore { get; private set; }
        public float PlayTime { get; private set; }
        public float RemainingSeconds { get { return m_EndTimerLeft; } }

        public Session(bool useEndTimer, float endTimerSeconds, bool useNoInteractionTimeout,
            float noInteractionTimeoutSeconds)
        {
            m_UseEndTimer = useEndTimer;
            m_EndTimerSeconds = Mathf.Max(0f, endTimerSeconds);
            m_UseNoInteractionTimeout = useNoInteractionTimeout;
            m_NoInteractionLeft = Mathf.Max(0f, noInteractionTimeoutSeconds);
            m_EndTimerLeft = m_EndTimerSeconds;
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
            if (HasOpenedStore && now - m_LastInstallTime < InstallRepeatGuardSeconds) return false;
            HasOpenedStore = true;
            m_LastInstallTime = now;
            return true;
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

    [Header("First Interaction")]
    [SerializeField] private bool m_StartOnFirstPointerDown = false;
    [SerializeField] private bool m_UseNoInteractionTimeout = false;
    [SerializeField, Min(1f)] private float m_NoInteractionTimeoutSeconds = 10f;
    [SerializeField] private NoInteractionResult m_NoInteractionResult = NoInteractionResult.KeepPlayable;

    [Header("Events")]
    public UnityEvent onEndTimeEventReached = new UnityEvent();
    public UnityEvent onNoInteractionTimeout = new UnityEvent();

    public event Action GameplayStarted;
    public event Action GameplayEnded;
    public event Action StoreVisited;
    public event Action EndTimerReached;
    public event Action NoInteractionTimedOut;
    public event Action<bool> PauseChanged;
    public event Action<bool> MuteChanged;

    public static LunaManager instance;
    public static LunaManager Instance { get { return instance; } }

    private Session m_Session;

    public bool IsStarted { get { return GetSession().IsStarted; } }
    public bool HasStarted { get { return IsStarted; } }
    public bool IsEnded { get { return GetSession().IsEnded; } }
    public bool HasEnded { get { return IsEnded; } }
    public bool HasOpenedStore { get { return GetSession().HasOpenedStore; } }
    public bool IsStoreVisited { get { return HasOpenedStore; } }
    public float PlayTime { get { return GetSession().PlayTime; } }
    public float RemainingSeconds { get { return GetSession().RemainingSeconds; } }
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
        PlayableKit.Analytics.PlayableAnalytics.AddSink(Shared.UI.Adapters.LunaAnalyticsSink.Instance);
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
        PlayableKit.Analytics.PlayableAnalytics.RemoveSink(Shared.UI.Adapters.LunaAnalyticsSink.Instance);
        if (instance == this) instance = null;
    }

    private Session GetSession()
    {
        if (m_Session == null)
        {
            m_Session = new Session(m_UseEndTimer, currentEndTime, m_UseNoInteractionTimeout,
                m_NoInteractionTimeoutSeconds);
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

    private void Update()
    {
        if (IsPaused) return;

        Session session = GetSession();
        if (m_StartOnFirstPointerDown && !session.IsStarted && PointerDownThisFrame())
        {
            StartGame();
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
        m_Session = new Session(m_UseEndTimer, currentEndTime, m_UseNoInteractionTimeout,
            m_NoInteractionTimeoutSeconds);
    }

    // Opens synchronously so it stays inside the player's click.
    private void OpenStore()
    {
        if (!GetSession().TryOpenStore(Time.unscaledTime)) return;
        Luna.Unity.Playable.InstallFullGame();
        if (StoreVisited != null) StoreVisited();
    }

    private void HandlePause()
    {
        IsPaused = true;
        AudioListener.pause = true;
        if (PauseChanged != null) PauseChanged(true);
    }

    private void HandleResume()
    {
        IsPaused = false;
        AudioListener.pause = IsMuted;
        if (PauseChanged != null) PauseChanged(false);
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
        AudioListener.volume = muted ? 0f : 1f;
        AudioListener.pause = muted || IsPaused;
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
