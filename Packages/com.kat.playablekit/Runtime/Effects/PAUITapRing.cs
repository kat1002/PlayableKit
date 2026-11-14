using UnityEngine;
using UnityEngine.UI;

namespace PlayableKit.Effects
{
    /// <summary>
    /// Smooth "tap ripple" ring. One pulse scales an Image from startScale to endScale (ease-out)
    /// while its alpha fades in over fadeInFraction of the pulse, then fades back to zero. The Image is
    /// disabled while idle. Call Play() per tap, or enable loop for a self-repeating pulse. Runs on
    /// unscaled time with no tweens or lambdas (Luna-safe). Sprite: Sprites/Ring.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class PAUITapRing : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Image target;

        [Header("Pulse")]
        [Tooltip("Seconds for one pulse.")]
        [SerializeField] private float duration = 0.6f;
        [SerializeField] private float startScale = 0.3f;
        [SerializeField] private float endScale = 1.3f;
        [Tooltip("Fraction of the pulse (0-1) spent fading in; the rest fades out.")]
        [Range(0.05f, 0.95f)]
        [SerializeField] private float fadeInFraction = 0.3f;
        [Range(0f, 1f)]
        [SerializeField] private float maxAlpha = 1f;

        [Header("Loop")]
        [SerializeField] private bool loop = false;
        [Tooltip("Seconds between the start of one pulse and the next when looping.")]
        [SerializeField] private float loopInterval = 1f;

        [Header("Time")]
        [SerializeField] private bool useUnscaledTime = true;

        private bool _playing;
        private float _time;
        private float _sinceStart;
        private Vector3 _baseScale = Vector3.one;

        private void Reset()
        {
            target = GetComponent<Image>();
        }

        private void Awake()
        {
            if (target == null)
            {
                target = GetComponent<Image>();
            }

            _baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            _sinceStart = 0f;
            if (loop)
            {
                Play();
            }
            else
            {
                Hide();
            }
        }

        private void OnDisable()
        {
            Stop();
        }

        /// <summary>Start one pulse from the current position.</summary>
        public void Play()
        {
            _playing = true;
            _time = 0f;
            _sinceStart = 0f;
            Apply(0f);
        }

        /// <summary>Start one pulse centred on the given anchored position.</summary>
        public void Play(Vector2 anchoredPosition)
        {
            RectTransform rect = transform as RectTransform;
            if (rect != null)
            {
                rect.anchoredPosition = anchoredPosition;
            }

            Play();
        }

        /// <summary>Cancel the current pulse and hide the ring.</summary>
        public void Stop()
        {
            _playing = false;
            Hide();
        }

        private void Update()
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            if (loop)
            {
                _sinceStart += dt;
                if (!_playing && _sinceStart >= Mathf.Max(0.05f, loopInterval))
                {
                    Play();
                }
            }

            if (!_playing)
            {
                return;
            }

            float length = Mathf.Max(0.05f, duration);
            _time += dt;
            float t = _time / length;
            if (t >= 1f)
            {
                _playing = false;
                Hide();
                return;
            }

            Apply(t);
        }

        private void Apply(float t)
        {
            if (target == null)
            {
                return;
            }

            float inv = 1f - t;
            float eased = 1f - inv * inv; // ease-out quad
            float scale = Mathf.Lerp(startScale, endScale, eased);
            transform.localScale = _baseScale * scale;

            float fadeIn = Mathf.Clamp(fadeInFraction, 0.05f, 0.95f);
            float alpha = t < fadeIn ? t / fadeIn : 1f - (t - fadeIn) / (1f - fadeIn);

            Color c = target.color;
            c.a = Mathf.Clamp01(alpha) * maxAlpha;
            target.color = c;
            target.enabled = true;
        }

        private void Hide()
        {
            if (target != null)
            {
                target.enabled = false;
            }

            transform.localScale = _baseScale;
        }
    }
}
