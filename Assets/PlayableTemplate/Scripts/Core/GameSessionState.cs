namespace PlayableTemplate.Core
{
    /// <summary>
    /// Pure C# state machine and session tracker. Decoupled from MonoBehaviour for EditMode testability.
    /// </summary>
    public class GameSessionState
    {
        public GameState State { get; private set; }
        public EndReason EndReason { get; private set; }
        public float PlayElapsed { get; private set; }
        public float SurviveDuration { get; set; }
        public bool IsMidpointTriggered { get; private set; }
        private bool _autoStoreOpened;
        private float _introInactivityElapsed;
        private bool _introInactivityTriggered;

        public bool IsPlaying => State == GameState.Playing;
        public bool IsEnded => State == GameState.Ended;

        public float Progress => SurviveDuration > 0f ? (PlayElapsed / SurviveDuration > 1f ? 1f : PlayElapsed / SurviveDuration) : 1f;

        public GameSessionState(float surviveDuration = 25f)
        {
            SurviveDuration = surviveDuration;
            Reset();
        }

        public void Reset()
        {
            State = GameState.Intro;
            PlayElapsed = 0f;
            IsMidpointTriggered = false;
            _autoStoreOpened = false;
            _introInactivityElapsed = 0f;
            _introInactivityTriggered = false;
        }

        public bool BeginPlay()
        {
            if (State != GameState.Intro) return false;
            State = GameState.Playing;
            PlayElapsed = 0f;
            IsMidpointTriggered = false;
            _autoStoreOpened = false;
            return true;
        }

        /// <summary>
        /// Advance time during active play. Returns true if the midpoint threshold is crossed this tick.
        /// </summary>
        public bool Tick(float deltaTime)
        {
            if (State != GameState.Playing) return false;

            PlayElapsed += deltaTime;
            if (!IsMidpointTriggered && PlayElapsed >= SurviveDuration)
            {
                IsMidpointTriggered = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Advance the real-time inactivity timer while waiting for the first input.
        /// Returns true once when the timeout is crossed.
        /// </summary>
        public bool TickIntroInactivity(float deltaTime, float timeout)
        {
            if (State != GameState.Intro || _introInactivityTriggered) return false;

            _introInactivityElapsed += deltaTime;
            if (_introInactivityElapsed < timeout) return false;

            _introInactivityTriggered = true;
            return true;
        }

        public bool EndGame(EndReason reason)
        {
            if (State == GameState.Ended) return false;
            State = GameState.Ended;
            EndReason = reason;
            return true;
        }

        /// <summary>Claim the one automatic store hand-off for a non-timeout end. Manual CTA taps remain available.</summary>
        public bool TryAutoOpenStore(bool enabled)
        {
            if (!enabled || !IsEnded || EndReason == PlayableTemplate.Core.EndReason.Timeout || _autoStoreOpened) return false;
            _autoStoreOpened = true;
            return true;
        }
    }
}
