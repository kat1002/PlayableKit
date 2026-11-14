using System.Collections.Generic;

namespace PlayableKit.Analytics
{
    /// <summary>Shared analytics name constants.</summary>
    public static class AnalyticsNames
    {
        public const string ButtonPrefix = "btn_";
    }

    /// <summary>
    /// SDK-agnostic analytics hub. Game code calls the helpers; SDK adapters register as
    /// <see cref="IAnalyticsSink"/>; observers can subscribe to <see cref="Tracked"/>.
    /// Every event is logged via <see cref="PALog"/> when <see cref="LogToConsole"/> is true.
    /// </summary>
    public static class PlayableAnalytics
    {
        public static bool LogToConsole = true;

        private static readonly List<IAnalyticsSink> Sinks = new List<IAnalyticsSink>();

        /// <summary>Raised for every tracked event (kind, name, value).</summary>
        public static event System.Action<AnalyticsEventKind, string, int> Tracked;

        public static void AddSink(IAnalyticsSink sink)
        {
            if (sink == null || Sinks.Contains(sink))
            {
                return;
            }
            Sinks.Add(sink);
        }

        public static void RemoveSink(IAnalyticsSink sink)
        {
            if (sink == null)
            {
                return;
            }
            Sinks.Remove(sink);
        }

        public static void LevelStart(int levelNumber) { Track(AnalyticsEventKind.LevelStart, DefaultName(AnalyticsEventKind.LevelStart), levelNumber); }
        public static void LevelWon(int levelNumber) { Track(AnalyticsEventKind.LevelWon, DefaultName(AnalyticsEventKind.LevelWon), levelNumber); }
        public static void LevelFailed(int levelNumber) { Track(AnalyticsEventKind.LevelFailed, DefaultName(AnalyticsEventKind.LevelFailed), levelNumber); }
        public static void LevelRetry(int levelNumber) { Track(AnalyticsEventKind.LevelRetry, DefaultName(AnalyticsEventKind.LevelRetry), levelNumber); }
        public static void TutorialStarted() { Track(AnalyticsEventKind.TutorialStarted, DefaultName(AnalyticsEventKind.TutorialStarted), 0); }
        public static void TutorialComplete() { Track(AnalyticsEventKind.TutorialComplete, DefaultName(AnalyticsEventKind.TutorialComplete), 0); }
        public static void EndCardShown() { Track(AnalyticsEventKind.EndCardShown, DefaultName(AnalyticsEventKind.EndCardShown), 0); }
        public static void Score(int value) { Track(AnalyticsEventKind.Score, DefaultName(AnalyticsEventKind.Score), value); }

        /// <summary>Custom named event with one int parameter.</summary>
        public static void Custom(string eventName, int value)
        {
            Track(AnalyticsEventKind.Custom, eventName, value);
        }

        /// <summary>Custom "btn_&lt;name&gt;" click event (value = 1).</summary>
        public static void Button(string buttonName)
        {
            Custom(AnalyticsNames.ButtonPrefix + buttonName, 1);
        }

        public static void Track(AnalyticsEventKind kind, string name, int value)
        {
            if (LogToConsole)
            {
                PALog.Info("Analytics", name + " " + value);
            }

            for (int i = 0; i < Sinks.Count; i++)
            {
                Sinks[i].Send(kind, name, value);
            }

            if (Tracked != null)
            {
                Tracked(kind, name, value);
            }
        }

        public static string DefaultName(AnalyticsEventKind kind)
        {
            switch (kind)
            {
                case AnalyticsEventKind.LevelStart: return "LevelStart";
                case AnalyticsEventKind.LevelWon: return "LevelWon";
                case AnalyticsEventKind.LevelFailed: return "LevelFailed";
                case AnalyticsEventKind.LevelRetry: return "LevelRetry";
                case AnalyticsEventKind.TutorialStarted: return "TutorialStarted";
                case AnalyticsEventKind.TutorialComplete: return "TutorialComplete";
                case AnalyticsEventKind.EndCardShown: return "EndCardShown";
                case AnalyticsEventKind.Score: return "Score";
                default: return "Custom";
            }
        }
    }
}
