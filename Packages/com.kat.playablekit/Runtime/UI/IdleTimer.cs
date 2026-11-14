using UnityEngine;

namespace PlayableKit.UI
{
    /// <summary>
    /// The mirror shape of <see cref="CountdownTimer"/>: counts UP while idle, resets on activity,
    /// and reports expiry once it reaches a threshold (e.g. "force the player to the store if
    /// they've done nothing for N seconds"). Deliberately a separate, small class rather than a
    /// variant of CountdownTimer — the two have opposite direction/reset semantics and unifying
    /// them would just recreate the duplication this kit is trying to remove.
    /// </summary>
    public sealed class IdleTimer
    {
        private readonly float _threshold;
        private float _elapsed;

        public IdleTimer(float thresholdSeconds)
        {
            _threshold = Mathf.Max(0.01f, thresholdSeconds);
        }

        public void Reset()
        {
            _elapsed = 0f;
        }

        /// <returns>True once <paramref name="deltaTime"/> accumulation reaches the threshold since
        /// the last <see cref="Reset"/>. Keeps returning true every subsequent tick until reset —
        /// callers that only want the edge should reset immediately after acting on it.</returns>
        public bool Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            return _elapsed >= _threshold;
        }
    }
}
