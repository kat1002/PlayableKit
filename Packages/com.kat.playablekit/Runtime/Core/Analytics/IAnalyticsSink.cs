namespace PlayableKit.Analytics
{
    /// <summary>Backend that receives analytics events (Luna, other SDKs, test sinks).</summary>
    public interface IAnalyticsSink
    {
        void Send(AnalyticsEventKind kind, string name, int value);
    }
}
