using PlayableKit.Analytics;

namespace Shared.UI.Adapters
{
    /// <summary>
    /// Forwards <see cref="PlayableAnalytics"/> events to <c>Luna.Unity.Analytics</c>.
    /// Registered by <see cref="LunaManager"/>. No-op outside a Luna build.
    /// </summary>
    public sealed class LunaAnalyticsSink : IAnalyticsSink
    {
        public static readonly LunaAnalyticsSink Instance = new LunaAnalyticsSink();

        public void Send(AnalyticsEventKind kind, string name, int value)
        {
#if UNITY_LUNA
            switch (kind)
            {
                case AnalyticsEventKind.TutorialStarted: Luna.Unity.Analytics.LogEvent(Luna.Unity.Analytics.EventType.TutorialStarted, value); break;
                case AnalyticsEventKind.TutorialComplete: Luna.Unity.Analytics.LogEvent(Luna.Unity.Analytics.EventType.TutorialComplete, value); break;
                case AnalyticsEventKind.EndCardShown: Luna.Unity.Analytics.LogEvent(Luna.Unity.Analytics.EventType.EndCardShown, value); break;
                case AnalyticsEventKind.LevelStart: Luna.Unity.Analytics.LogEvent(Luna.Unity.Analytics.EventType.LevelStart, value); break;
                case AnalyticsEventKind.LevelWon: Luna.Unity.Analytics.LogEvent(Luna.Unity.Analytics.EventType.LevelWon, value); break;
                case AnalyticsEventKind.LevelFailed: Luna.Unity.Analytics.LogEvent(Luna.Unity.Analytics.EventType.LevelFailed, value); break;
                case AnalyticsEventKind.LevelRetry: Luna.Unity.Analytics.LogEvent(Luna.Unity.Analytics.EventType.LevelRetry, value); break;
                case AnalyticsEventKind.Score: Luna.Unity.Analytics.LogEvent(Luna.Unity.Analytics.EventType.Score, value); break;
                default: Luna.Unity.Analytics.LogEvent(name, value); break;
            }
#endif
        }
    }
}
