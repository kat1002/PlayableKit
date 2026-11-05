using TMPro;
using UnityEngine;

namespace PlayableKit.UI
{
    /// <summary>
    /// A one-shot countdown: starts at N seconds, ticks down, optionally writes the remaining
    /// whole-second count to a Hor+Ver label pair, and reports expiry exactly once. Plain C#
    /// class — construct one per timer you need, call <see cref="Tick"/> from your own Update.
    /// </summary>
    public sealed class CountdownTimer
    {
        private readonly Oriented<TMP_Text> _label;
        private float _remaining;

        public CountdownTimer(Oriented<TMP_Text> label = default)
        {
            _label = label;
        }

        public bool IsRunning { get; private set; }
        public float Remaining => _remaining;

        public void Start(float seconds)
        {
            _remaining = Mathf.Max(1f, seconds);
            IsRunning = true;
            UpdateLabel();
        }

        public void Stop()
        {
            IsRunning = false;
        }

        /// <returns>True on the exact frame the timer reaches zero (fires once).</returns>
        public bool Tick(float deltaTime)
        {
            if (!IsRunning)
            {
                return false;
            }

            _remaining -= deltaTime;
            UpdateLabel();

            if (_remaining > 0f)
            {
                return false;
            }

            IsRunning = false;
            return true;
        }

        private void UpdateLabel()
        {
            if (_label.Hor == null && _label.Ver == null)
            {
                return;
            }

            _label.SetText(Mathf.CeilToInt(Mathf.Max(0f, _remaining)).ToString());
        }
    }
}
