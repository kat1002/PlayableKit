using PlayableKit.Tweening;
using UnityEngine;

namespace PlayableKit.Tweening
{
    /// <summary>
    /// One page of a landscape customize panel (menu list / Body / Wheel / Nitro). The panel stays put and
    /// the pages move inside it with one rule: they always leave out of the panel's LEFT edge (<see cref="Hide"/>) and
    /// always come in from outside the LEFT edge, moving right (<see cref="Show"/>). Switching pages is therefore
    /// "old page out to the left, then new page in from the left": <see cref="Show"/> waits <see cref="showDelay"/>
    /// (about one <see cref="hideDuration"/>) so the entrance starts as the exit ends. The parent panel clips the
    /// page (RectMask2D), so nothing draws outside it.
    ///
    /// The motion is plain PlayableKit.Tweening, like <see cref="PAUiSlide"/> - deliberately no callbacks or lambdas (Luna). The
    /// entrance delay is the tween's own delay (the page is active but parked outside the panel meanwhile); the
    /// deactivate-after-hide is a timer checked in <see cref="Update"/>. While the panel's tree is inactive
    /// (portrait is showing) every call just flips the active flag, so a later orientation flip finds the pages in
    /// the right state and at rest.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class PAUiPageSlide : MonoBehaviour
    {
        [Header("Out (to the left)")]
        [SerializeField, Min(0.05f)] private float hideDuration = 0.2f;
        [SerializeField] private Ease hideEase = Ease.InCubic;

        [Header("In (from the left)")]
        [SerializeField, Min(0.05f)] private float showDuration = 0.3f;
        [SerializeField] private Ease showEase = Ease.OutCubic;
        [Tooltip("Wait before the entrance starts - about the outgoing page's hideDuration, so it comes in as the old page leaves.")]
        [SerializeField, Min(0f)] private float showDelay = 0.2f;

        private RectTransform _rect;
        private Tween _tween;
        private float _restX;
        private bool _hasRest;
        private bool _hiding;
        private float _hideAt;

        private RectTransform Rect => _rect != null ? _rect : (_rect = (RectTransform)transform);

        // The page slides by the parent panel's width, which always clears it.
        private float Distance
        {
            get
            {
                var parent = Rect.parent as RectTransform;
                return parent != null ? parent.rect.width : 600f;
            }
        }

        // True while the panel this page lives in is actually on screen.
        private bool PanelVisible => transform.parent == null || transform.parent.gameObject.activeInHierarchy;

        /// <summary>
        /// Brings the page in from outside the left edge, moving right, after <see cref="showDelay"/>. A page that
        /// is still sliding out turns around from where it is, at once and without a jump.
        /// </summary>
        public void Show()
        {
            EnsureRest();
            if (!PanelVisible)
            {
                SnapActive(true);
                return;
            }

            if (_hiding)
            {
                Kill();
                _hiding = false;
                _tween = Rect.DOAnchorPosX(_restX, showDuration).SetEase(showEase);
                return;
            }

            if (gameObject.activeSelf)
            {
                return;
            }

            Kill();
            SetX(_restX - Distance);
            gameObject.SetActive(true);
            _tween = Rect.DOAnchorPosX(_restX, showDuration).SetEase(showEase).SetDelay(showDelay);
        }

        /// <summary>Pushes the page out of the left edge and deactivates it afterwards.</summary>
        public void Hide()
        {
            EnsureRest();
            if (!PanelVisible)
            {
                SnapActive(false);
                return;
            }

            if (!gameObject.activeSelf || _hiding)
            {
                return;
            }

            // Still parked outside, waiting for its entrance delay: nothing to see, just drop it.
            var outside = _restX - Distance;
            if (Rect.anchoredPosition.x <= outside + 1f)
            {
                SnapActive(false);
                return;
            }

            Kill();
            _hiding = true;
            _hideAt = Time.time + hideDuration;
            _tween = Rect.DOAnchorPosX(outside, hideDuration).SetEase(hideEase);
        }

        /// <summary>Active or not, at rest, no animation.</summary>
        public void SnapActive(bool active)
        {
            EnsureRest();
            Kill();
            _hiding = false;
            SetX(_restX);
            gameObject.SetActive(active);
        }

        private void Update()
        {
            if (_hiding && Time.time >= _hideAt)
            {
                SnapActive(false);
            }
        }

        private void OnEnable()
        {
            // The panel was switched off in the middle of this page's slide-out: it must not come back visible.
            if (_hiding)
            {
                SnapActive(false);
            }
        }

        private void OnDisable()
        {
            Kill();
            if (_hasRest)
            {
                SetX(_restX);
            }
        }

        // Captured on first use - a page that has never slid is at rest.
        private void EnsureRest()
        {
            if (!_hasRest)
            {
                _restX = Rect.anchoredPosition.x;
                _hasRest = true;
            }
        }

        private void SetX(float x)
        {
            var position = Rect.anchoredPosition;
            position.x = x;
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
