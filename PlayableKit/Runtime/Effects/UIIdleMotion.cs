using UnityEngine;

namespace PlayableKit
{
    /// <summary>
    /// Endless idle motion for UI: bob (Y), sway (Z rotation), pulse (scale) and fade (CanvasGroup alpha), each an
    /// optional sine channel. Update-driven, no tweens (Luna-safe). Base position/rotation/scale/alpha are cached on
    /// first OnEnable and restored in OnDisable; sway rotates around the base rotation, so mirrored (Y = 180) objects
    /// stay mirrored. Do not enable Bob together with DoMoveAToB on the same target (both write the position).
    /// </summary>
    public sealed class UIIdleMotion : MonoBehaviour
    {
        [Tooltip("Transform to animate. Empty = this transform.")]
        [SerializeField] private Transform target;

        [Header("Bob")]
        [SerializeField] private bool bob;
        [Tooltip("Peak offset up/down from the rest position, in UI units.")]
        [SerializeField] private float bobDistance = 10f;
        [SerializeField, Min(0.05f)] private float bobPeriod = 2f;

        [Header("Sway")]
        [SerializeField] private bool sway;
        [Tooltip("Peak tilt in degrees either side of the base rotation.")]
        [SerializeField] private float swayAngle = 5f;
        [SerializeField, Min(0.05f)] private float swayPeriod = 2.5f;

        [Header("Pulse")]
        [SerializeField] private bool pulse;
        [Tooltip("Peak scale multiplier of the base scale (1.04 = +4%).")]
        [SerializeField] private float pulseScale = 1.04f;
        [SerializeField, Min(0.05f)] private float pulsePeriod = 1.6f;

        [Header("Fade")]
        [SerializeField] private bool fade;
        [SerializeField] private CanvasGroup fadeGroup;
        [Tooltip("Lowest alpha, as a multiplier of the base alpha.")]
        [SerializeField, Range(0f, 1f)] private float fadeMinAlpha = 0.8f;
        [SerializeField, Min(0.05f)] private float fadePeriod = 3f;

        [Header("Timing")]
        [Tooltip("Start offset in cycles (0.25 = a quarter of a period).")]
        [SerializeField] private float phase;
        [Tooltip("Add a random start offset on first enable, so duplicates do not move in lockstep.")]
        [SerializeField] private bool randomPhase;
        [SerializeField] private bool useUnscaledTime;

        private const float TwoPi = Mathf.PI * 2f;

        private RectTransform _rect;
        private Vector2 _basePos;
        private Vector3 _baseLocalPos;
        private Quaternion _baseRot;
        private Vector3 _baseScale;
        private float _baseAlpha;
        private float _time;
        private float _randomOffset;
        private bool _cached;

        private Transform Target
        {
            get { return target != null ? target : transform; }
        }

        private void OnEnable()
        {
            var t = Target;
            if (!_cached)
            {
                _rect = t as RectTransform;
                _basePos = _rect != null ? _rect.anchoredPosition : Vector2.zero;
                _baseLocalPos = t.localPosition;
                _baseRot = t.localRotation;
                _baseScale = t.localScale;
                _baseAlpha = fadeGroup != null ? fadeGroup.alpha : 1f;
                _randomOffset = randomPhase ? Random.value : 0f;
                _cached = true;
            }
            _time = 0f;
            Apply();
        }

        private void OnDisable()
        {
            if (!_cached)
            {
                return;
            }
            Restore();
        }

        private void Update()
        {
            float dt = Mathf.Min(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime, 0.1f);
            _time += dt;
            Apply();
        }

        private void Apply()
        {
            var t = Target;
            float basePhase = phase + _randomOffset;

            if (bob)
            {
                float y = Mathf.Sin((_time / bobPeriod + basePhase) * TwoPi) * bobDistance;
                if (_rect != null)
                {
                    _rect.anchoredPosition = _basePos + new Vector2(0f, y);
                }
                else
                {
                    t.localPosition = _baseLocalPos + new Vector3(0f, y, 0f);
                }
            }

            if (sway)
            {
                // Offset the cycle a little so the channels do not peak together.
                float a = Mathf.Sin((_time / swayPeriod + basePhase + 0.17f) * TwoPi) * swayAngle;
                t.localRotation = _baseRot * Quaternion.Euler(0f, 0f, a);
            }

            if (pulse)
            {
                float w = 0.5f + 0.5f * Mathf.Sin((_time / pulsePeriod + basePhase + 0.31f) * TwoPi);
                t.localScale = _baseScale * Mathf.Lerp(1f, pulseScale, w);
            }

            if (fade && fadeGroup != null)
            {
                float w = 0.5f + 0.5f * Mathf.Sin((_time / fadePeriod + basePhase + 0.43f) * TwoPi);
                fadeGroup.alpha = _baseAlpha * Mathf.Lerp(1f, fadeMinAlpha, w);
            }
        }

        private void Restore()
        {
            var t = Target;
            if (bob)
            {
                if (_rect != null)
                {
                    _rect.anchoredPosition = _basePos;
                }
                else
                {
                    t.localPosition = _baseLocalPos;
                }
            }
            if (sway)
            {
                t.localRotation = _baseRot;
            }
            if (pulse)
            {
                t.localScale = _baseScale;
            }
            if (fade && fadeGroup != null)
            {
                fadeGroup.alpha = _baseAlpha;
            }
        }
    }
}
