namespace PlayableKit.Analytics
{
    /// <summary>Kind of analytics event. Custom carries a free-form name; the rest are standard events.</summary>
    public enum AnalyticsEventKind
    {
        Custom,
        LevelStart,
        LevelWon,
        LevelFailed,
        LevelRetry,
        TutorialStarted,
        TutorialComplete,
        EndCardShown,
        Score
    }
}
