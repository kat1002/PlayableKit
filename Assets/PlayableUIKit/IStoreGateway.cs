namespace PlayableUIKit
{
    /// <summary>
    /// The "send the player to the store" call, however this project's ad SDK actually exposes it
    /// (Luna's <c>Luna.Unity.Playable.InstallFullGame()</c>, a different SDK's equivalent, or a
    /// stub for editor testing). Implement a thin adapter over your project's actual SDK — see the
    /// template README — rather than depending on that SDK directly.
    /// </summary>
    public interface IStoreGateway
    {
        void GoToStore();
    }
}
