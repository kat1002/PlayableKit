using PlayableKit.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableKit
{
    /// <summary>
    /// Pixel-art smoke puffs. Duplicates an inactive template Image poolSize times in Awake, then Burst()
    /// flings count puffs outward (OutQuad), growing and fading, with rotation snapped to 90 degree steps.
    /// Tweens are killed in OnDisable.
    /// </summary>
    public sealed class PAUIPixelSmokeBurst : MonoBehaviour
    {
        [Tooltip("Inactive child Image used as the puff template. Duplicated once in Awake.")]
        [SerializeField] private Image template;
        [Tooltip("Random sprite per puff (Point-filtered). Empty = keep the template sprite.")]
        [SerializeField] private Sprite[] variants;
        [SerializeField, Min(1)] private int poolSize = 32;
        [SerializeField] private Vector2 sizeRange = new Vector2(48f, 96f);
        [SerializeField, Min(0.01f)] private float duration = 0.5f;
        [SerializeField] private bool useUnscaledTime = true;

        private RectTransform _area;
        private RectTransform[] _puffs;
        private Image[] _images;
        private Sequence[] _sequences;
        private int _next;

        private void Awake()
        {
            _area = (RectTransform)transform;

            if (template == null)
            {
                _puffs = new RectTransform[0];
                _images = new Image[0];
                _sequences = new Sequence[0];
                return;
            }

            template.gameObject.SetActive(false);
            _puffs = new RectTransform[poolSize];
            _images = new Image[poolSize];
            _sequences = new Sequence[poolSize];

            for (var i = 0; i < poolSize; i++)
            {
                var img = Instantiate(template, _area, false);
                img.name = "Puff_" + i;
                img.raycastTarget = false;

                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                img.gameObject.SetActive(false);

                _puffs[i] = rt;
                _images[i] = img;
            }
        }

        private void OnDisable()
        {
            if (_puffs == null)
            {
                return;
            }

            for (var i = 0; i < _puffs.Length; i++)
            {
                if (_sequences[i] != null)
                {
                    _sequences[i].Kill();
                    _sequences[i] = null;
                }

                _images[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Spawns count puffs at anchoredPos (this object's local canvas space), flung minDist..maxDist away.</summary>
        public void Burst(Vector2 anchoredPos, int count, float minDist, float maxDist)
        {
            if (_puffs == null || _puffs.Length == 0)
            {
                return;
            }

            for (var n = 0; n < count; n++)
            {
                var i = _next;
                _next = (_next + 1) % _puffs.Length;

                if (_sequences[i] != null)
                {
                    _sequences[i].Kill();
                    _sequences[i] = null;
                }

                var rt = _puffs[i];
                var img = _images[i];

                var size = Random.Range(sizeRange.x, sizeRange.y);
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var dist = Random.Range(minDist, maxDist);
                var target = anchoredPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

                if (variants != null && variants.Length > 0)
                {
                    img.sprite = variants[Random.Range(0, variants.Length)];
                }

                var c = img.color;
                img.color = new Color(c.r, c.g, c.b, 1f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = anchoredPos;
                rt.localScale = Vector3.one * 0.4f;
                rt.localEulerAngles = new Vector3(0f, 0f, 90f * Random.Range(0, 4));
                img.gameObject.SetActive(true);
                rt.SetAsLastSibling();

                var seq = TweenLite.Sequence();
                seq.Append(rt.DOAnchorPos(target, duration).SetEase(Ease.OutQuad));
                seq.Join(rt.DOScale(1.2f, duration).SetEase(Ease.OutQuad));
                seq.Join(img.DOFade(0f, duration));
                seq.SetUpdate(useUnscaledTime);

                var index = i;
                seq.OnComplete(() =>
                {
                    _images[index].gameObject.SetActive(false);
                    _sequences[index] = null;
                });
                _sequences[i] = seq;
            }
        }

        /// <summary>Converts a screen point into local space of space (overlay canvas: null camera).</summary>
        public static bool ScreenToLocal(RectTransform space, Vector2 screen, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(space, screen, null, out local);
        }
    }
}
