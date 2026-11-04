using Playable.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableKit
{
    /// <summary>
    /// Standalone praise text fly-up effect.
    /// Spawns or pools identical praise text items from a prefab and animates them flying up in a fan spread.
    /// </summary>
    public sealed class UIPraiseFly : MonoBehaviour
    {
        [Tooltip("Prefab instantiated for each praise fly element (must contain Image).")]
        [SerializeField] private Image m_Prefab;
        [Tooltip("Sprites corresponding to each step (e.g. index 0 = Awesome, 1 = Great Job, 2 = Perfect).")]
        [SerializeField] private Sprite[] m_StepSprites = new Sprite[0];
        [Tooltip("Target fly offsets relative to this container (e.g. top-left, top, top-right).")]
        [SerializeField] private Vector2[] m_Offsets = new Vector2[]
        {
            new Vector2(-220f, 150f),
            new Vector2(0f, 210f),
            new Vector2(220f, 150f)
        };

        [Header("Animation")]
        [SerializeField, Min(0.01f)] private float m_Duration = 1.1f;
        [SerializeField, Min(0.01f)] private float m_FadeIn = 0.15f;
        [SerializeField, Min(0.01f)] private float m_FadeOut = 0.35f;
        [SerializeField, Min(0f)] private float m_StartScale = 0.5f;
        [SerializeField, Min(1f)] private float m_Overshoot = 1.15f;
        [SerializeField] private float m_Tilt = 20f;
        [SerializeField, Min(0f)] private float m_Stagger = 0.06f;
        [Tooltip("If true, items rotate into target tilt as they fly up. If false, items start tilted from the start point.")]
        [SerializeField] private bool m_RotateOnFly = false;
        [SerializeField] private bool m_UseUnscaledTime;

        private RectTransform _container;
        private Image[] _pooledImages = new Image[0];
        private RectTransform[] _pooledRects = new RectTransform[0];
        private Sequence _sequence;

        private void Awake()
        {
            _container = (RectTransform)transform;
            InitPool();
        }

        private void InitPool()
        {
            if (m_Prefab == null) return;

            int count = m_Offsets != null && m_Offsets.Length > 0 ? m_Offsets.Length : 3;
            _pooledImages = new Image[count];
            _pooledRects = new RectTransform[count];

            for (int i = 0; i < count; i++)
            {
                var instance = Instantiate(m_Prefab, _container, false);
                instance.name = "PraiseItem_" + i;
                instance.raycastTarget = false;
                instance.gameObject.SetActive(false);

                _pooledImages[i] = instance;
                _pooledRects[i] = instance.rectTransform;
            }

            if (count == 3 && _pooledRects.Length >= 2 && _pooledRects[1] != null)
            {
                _pooledRects[1].SetAsLastSibling();
            }
        }

        private void OnDisable()
        {
            KillTween();
            HideAll();
        }

        private void OnDestroy()
        {
            KillTween();
        }

        public void KillTween()
        {
            if (_sequence != null)
            {
                _sequence.Kill();
                _sequence = null;
            }
        }

        private void HideAll()
        {
            if (_pooledImages == null) return;
            for (int i = 0; i < _pooledImages.Length; i++)
            {
                if (_pooledImages[i] != null) _pooledImages[i].gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Plays the praise fly animation for the specified step index (0 = step 1, 1 = step 2, etc.).
        /// All flying items use the same sprite mapped to this step.
        /// </summary>
        public void Play(int stepIndex)
        {
            Sprite sprite = null;
            if (m_StepSprites != null && stepIndex >= 0 && stepIndex < m_StepSprites.Length)
            {
                sprite = m_StepSprites[stepIndex];
            }

            Play(sprite);
        }

        /// <summary>
        /// Plays the praise fly animation with a specific sprite on all flying items.
        /// </summary>
        public void Play(Sprite sprite)
        {
            if (_pooledImages == null || _pooledImages.Length == 0)
            {
                if (m_Prefab != null) InitPool();
                if (_pooledImages == null || _pooledImages.Length == 0) return;
            }

            KillTween();

            var seq = TweenLite.Sequence();
            seq.SetUpdate(m_UseUnscaledTime);

            float d = m_Duration;
            float fadeOut = Mathf.Min(m_FadeOut, d);
            int count = _pooledImages.Length;

            if (count == 3 && _pooledRects != null && _pooledRects.Length >= 2 && _pooledRects[1] != null)
            {
                _pooledRects[1].SetAsLastSibling();
            }

            for (int i = 0; i < count; i++)
            {
                var img = _pooledImages[i];
                var rt = _pooledRects[i];
                Vector2 offset = m_Offsets != null && i < m_Offsets.Length ? m_Offsets[i] : Vector2.zero;
                float targetTilt = offset.x == 0f ? 0f : -Mathf.Sign(offset.x) * m_Tilt;
                float t0 = i * m_Stagger;

                if (sprite != null)
                {
                    img.sprite = sprite;
                    img.preserveAspect = true;
                    img.SetNativeSize();
                }

                img.raycastTarget = false;
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one * m_StartScale;
                rt.localRotation = Quaternion.Euler(0f, 0f, m_RotateOnFly ? 0f : targetTilt);

                Color c = img.color;
                c.a = 0f;
                img.color = c;
                img.gameObject.SetActive(true);

                var itemObj = img.gameObject;
                var itemSeq = TweenLite.Sequence();
                itemSeq.AppendInterval(t0);
                itemSeq.Join(rt.DOAnchorPos(offset, d).SetEase(Ease.OutCubic));
                itemSeq.Join(img.DOFade(1f, m_FadeIn));

                if (m_RotateOnFly)
                {
                    itemSeq.Join(DOVirtual.Float(0f, targetTilt, d * 0.45f, v => rt.localRotation = Quaternion.Euler(0f, 0f, v)).SetEase(Ease.OutCubic));
                }

                // Scale punch & settle
                var scaleSeq = TweenLite.Sequence();
                scaleSeq.AppendInterval(t0);
                scaleSeq.Append(rt.DOScale(Vector3.one * m_Overshoot, 0.22f).SetEase(Ease.OutQuad));
                scaleSeq.Append(rt.DOScale(Vector3.one, 0.15f).SetEase(Ease.InOutQuad));
                seq.Join(scaleSeq);

                // Fade out & deactivate
                var fadeSeq = TweenLite.Sequence();
                fadeSeq.AppendInterval(t0 + d - fadeOut);
                fadeSeq.Append(img.DOFade(0f, fadeOut));
                fadeSeq.AppendCallback(() => itemObj.SetActive(false));
                seq.Join(fadeSeq);

                seq.Join(itemSeq);
            }

            _sequence = seq;
        }
    }
}
