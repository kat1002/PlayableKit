using PlayableKit.Tweening;
using UnityEngine;

namespace PlayableKit.Tweening
{
    /// <summary>
    /// Slides a UI element vertically to a target <c>anchoredPosition.y</c> - used to push the whole
    /// customize sheet (background, tabs, content) up from the bottom. One plain tween call,
    /// deliberately nothing more (no sequences, callbacks or lambdas) so Luna compiles it.
    ///
    /// It remembers its last target: while inactive it just snaps, and it re-applies that target on
    /// enable / disable, so an orientation flip mid-slide never leaves the hidden copy half-way.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class PAUiSlide : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float duration = 0.35f;
        [SerializeField] private Ease ease = Ease.OutBack;
        [Tooltip("Slide along X (a left-side panel) instead of Y (a bottom sheet).")]
        [SerializeField] private bool horizontal;

        private RectTransform _rect;
        private Tween _tween;
        private float _y;
        private bool _hasTarget;

        private RectTransform Rect => _rect != null ? _rect : (_rect = (RectTransform)transform);

        /// <summary>Jumps straight to <paramref name="y"/> (no animation).</summary>
        public void SnapTo(float y)
        {
            Kill();
            _y = y;
            _hasTarget = true;
            SetY(y);
        }

        /// <summary>Pushes to <paramref name="y"/> with the tween.</summary>
        public void SlideTo(float y)
        {
            Kill();
            _y = y;
            _hasTarget = true;
            if (!isActiveAndEnabled)
            {
                SetY(y);
                return;
            }

            _tween = (horizontal ? Rect.DOAnchorPosX(y, duration) : Rect.DOAnchorPosY(y, duration)).SetEase(ease);
        }

        private void OnEnable()
        {
            if (_hasTarget)
            {
                SetY(_y);
            }
        }

        private void OnDisable()
        {
            Kill();
            if (_hasTarget)
            {
                SetY(_y);
            }
        }

        private void SetY(float y)
        {
            var position = Rect.anchoredPosition;
            if (horizontal)
            {
                position.x = y;
            }
            else
            {
                position.y = y;
            }

            Rect.anchoredPosition = position;
        }

        private void Kill()
        {
            if (_tween != null)
            {
                _tween.Kill();
                _tween = null;
            }
        }
    }
}
