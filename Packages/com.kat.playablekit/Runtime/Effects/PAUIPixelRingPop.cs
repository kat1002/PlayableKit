using PlayableKit.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableKit.Effects
{
    /// <summary>
    /// Pixel-art ring pop. Duplicates an inactive template Image poolSize times in Awake, then Play()
    /// grows the next ring from startScale through peakScale to endScale in stair steps (scale snapped to
    /// scaleStep multiples) while fading out. Tweens are killed in OnDisable.
    /// </summary>
    public sealed class PAUIPixelRingPop : MonoBehaviour
    {
        [Tooltip("Inactive child Image used as the ring template (Point-filtered sprite). Duplicated once in Awake.")]
        [SerializeField] private Image template;
        [SerializeField, Min(1)] private int poolSize = 3;
        [SerializeField] private float startScale = 0.15f;
        [SerializeField] private float peakScale = 1f;
        [SerializeField] private float endScale = 1.4f;
        [SerializeField, Min(0.01f)] private float duration = 0.45f;
        [Tooltip("Scale is snapped to multiples of this so the ring grows in visible steps.")]
        [SerializeField, Min(0.001f)] private float scaleStep = 0.1f;
        [Tooltip("Ring size in canvas units at scale 1.")]
        [SerializeField] private float baseSize = 480f;
        [SerializeField] private bool useUnscaledTime = true;

        private RectTransform _area;
        private RectTransform[] _rings;
        private Image[] _images;
        private Tween[] _scaleTweens;
        private Tween[] _fadeTweens;
        private int _next;

        private void Awake()
        {
            _area = (RectTransform)transform;

            if (template == null)
            {
                _rings = new RectTransform[0];
                _images = new Image[0];
                _scaleTweens = new Tween[0];
                _fadeTweens = new Tween[0];
                return;
            }

            template.gameObject.SetActive(false);
            _rings = new RectTransform[poolSize];
            _images = new Image[poolSize];
            _scaleTweens = new Tween[poolSize];
            _fadeTweens = new Tween[poolSize];

            for (var i = 0; i < poolSize; i++)
            {
                var img = Instantiate(template, _area, false);
                img.name = "Ring_" + i;
                img.raycastTarget = false;

                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(baseSize, baseSize);
                img.gameObject.SetActive(false);

                _rings[i] = rt;
                _images[i] = img;
            }
        }

        private void OnDisable()
        {
            if (_rings == null)
            {
                return;
            }

            for (var i = 0; i < _rings.Length; i++)
            {
                Kill(i);
                _images[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Plays one ring centred at anchoredPos (in this object's local canvas space).</summary>
        public void Play(Vector2 anchoredPos)
        {
            if (_rings == null || _rings.Length == 0)
            {
                return;
            }

            var i = _next;
            _next = (_next + 1) % _rings.Length;

            Kill(i);

            var rt = _rings[i];
            var img = _images[i];
            rt.anchoredPosition = anchoredPos;
            rt.localScale = Vector3.one * Snap(startScale);
            var c = img.color;
            img.color = new Color(c.r, c.g, c.b, 1f);
            img.gameObject.SetActive(true);
            rt.SetAsLastSibling();

            _scaleTweens[i] = DOVirtual.Float(0f, 1f, duration, t =>
            {
                var s = t < 0.6f
                    ? Mathf.Lerp(startScale, peakScale, t / 0.6f)
                    : Mathf.Lerp(peakScale, endScale, (t - 0.6f) / 0.4f);
                rt.localScale = Vector3.one * Snap(s);
            }).SetEase(Ease.OutQuad).SetUpdate(useUnscaledTime);

            var fadeDelay = duration * 0.4f;
            var index = i;
            _fadeTweens[i] = img.DOFade(0f, duration - fadeDelay)
                .SetDelay(fadeDelay)
                .SetUpdate(useUnscaledTime)
                .OnComplete(() =>
                {
                    img.gameObject.SetActive(false);
                    _scaleTweens[index] = null;
                    _fadeTweens[index] = null;
                });
        }

        private float Snap(float s)
        {
            return Mathf.Round(s / scaleStep) * scaleStep;
        }

        private void Kill(int i)
        {
            if (_scaleTweens[i] != null)
            {
                _scaleTweens[i].Kill();
                _scaleTweens[i] = null;
            }

            if (_fadeTweens[i] != null)
            {
                _fadeTweens[i].Kill();
                _fadeTweens[i] = null;
            }
        }
    }
}
