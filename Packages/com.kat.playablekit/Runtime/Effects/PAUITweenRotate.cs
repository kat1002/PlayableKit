using PlayableKit.Tweening;
using UnityEngine;

namespace PlayableKit.Effects
{
    /// <summary>
    /// Endless Z spin for UI (e.g. victory light rays), with an optional scale pulse. Tweens start in
    /// OnEnable and are killed in OnDisable, so it is safe inside panels toggled with SetActive.
    /// </summary>
    public sealed class PAUITweenRotate : MonoBehaviour
    {
        [Tooltip("Transform to spin. Empty = this transform.")]
        [SerializeField] private Transform target;
        [Tooltip("Negative = clockwise on screen.")]
        [SerializeField] private float degreesPerSecond = -30f;
        [SerializeField] private Ease ease = Ease.Linear;
        [SerializeField] private bool useUnscaledTime;

        [Header("Optional pulse")]
        [SerializeField] private bool pulse;
        [SerializeField] private float pulseScale = 1.08f;
        [SerializeField, Min(0.05f)] private float pulseDuration = 0.8f;

        private Tween _spinTween;
        private Tween _pulseTween;
        private Vector3 _baseScale;
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
                _baseScale = t.localScale;
                _cached = true;
            }
            var e = t.localEulerAngles;
            t.localEulerAngles = new Vector3(e.x, e.y, 0f);
            t.localScale = _baseScale;
            var speed = Mathf.Abs(degreesPerSecond);
            if (speed > 0.01f)
            {
                _spinTween = t.DOLocalRotate(new Vector3(e.x, e.y, 360f * Mathf.Sign(degreesPerSecond)), 360f / speed, RotateMode.FastBeyond360)
                    .SetEase(ease).SetLoops(-1, LoopType.Restart).SetUpdate(useUnscaledTime);
            }
            if (pulse)
            {
                _pulseTween = t.DOScale(_baseScale * pulseScale, pulseDuration)
                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(useUnscaledTime);
            }
        }

        private void OnDisable()
        {
            if (_spinTween != null)
            {
                _spinTween.Kill();
            }
            if (_pulseTween != null)
            {
                _pulseTween.Kill();
            }
            _spinTween = null;
            _pulseTween = null;
            if (_cached)
            {
                Target.localScale = _baseScale;
            }
        }
    }
}
