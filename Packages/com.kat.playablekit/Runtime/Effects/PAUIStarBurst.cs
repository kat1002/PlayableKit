using PlayableKit.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableKit.Effects
{
    /// <summary>
    /// Burst of UI stars fanning outward and falling.
    /// Clones an inactive template Image child count times in Awake, then plays a one-shot fan burst.
    /// </summary>
    public sealed class PAUIStarBurst : MonoBehaviour
    {
        [Tooltip("Inactive child Image used as the star template. Duplicated once in Awake.")]
        [SerializeField] private Image template;
        [SerializeField, Range(1, 30)] private int count = 14;
        [Tooltip("Random burst star size range (min, max).")]
        [SerializeField] private Vector2 sizeRange = new Vector2(40f, 90f);
        [Tooltip("Random burst travel distance range (min, max).")]
        [SerializeField] private Vector2 distanceRange = new Vector2(260f, 520f);
        [Tooltip("How far a burst star falls after reaching its peak, in UI units.")]
        [SerializeField, Min(0f)] private float fallDistance = 380f;
        [Tooltip("Random burst star lifetime range in seconds (min, max).")]
        [SerializeField] private Vector2 lifeRange = new Vector2(0.9f, 1.4f);
        [Tooltip("Half-width of the fan in degrees around straight up.")]
        [SerializeField, Min(0f)] private float spreadAngle = 70f;
        [Tooltip("Stars pop in at the centre, fly out evenly in a full circle and fade/shrink (Update-driven, no " +
                 "tweens), instead of the fountain fan that rises and falls.")]
        [SerializeField] private bool radial;
        [SerializeField] private float spinDegrees = 360f;
        [SerializeField, Min(0f)] private float staggerDelay = 0.02f;
        [SerializeField] private bool playOnEnable;
        [SerializeField] private bool useUnscaledTime;

        private RectTransform _area;
        private RectTransform[] _stars;
        private Image[] _images;
        private float[] _alphas;
        private Sequence _sequence;

        private void Awake()
        {
            _area = (RectTransform)transform;

            if (template == null)
            {
                _stars = new RectTransform[0];
                _images = new Image[0];
                _alphas = new float[0];
                return;
            }

            template.gameObject.SetActive(false);
            _stars = new RectTransform[count];
            _images = new Image[count];
            _alphas = new float[count];

            for (int i = 0; i < count; i++)
            {
                var img = Instantiate(template, _area, false);
                img.name = "BurstStar_" + i;
                img.raycastTarget = false;

                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;
                img.gameObject.SetActive(false);

                _stars[i] = rt;
                _images[i] = img;
                _alphas[i] = img.color.a;
            }
        }

        private void OnEnable()
        {
            if (playOnEnable)
            {
                Play();
            }
        }

        private void OnDisable()
        {
            Stop();
        }

        /// <summary>
        /// Stops the burst and hides every star. Call it before hiding the burst's parent: Luna builds do not
        /// reliably run OnDisable on children of a deactivated parent, so stars could otherwise stay on screen.
        /// </summary>
        public void Stop()
        {
            KillTween();
            _radialPlaying = false;
            HideAll();
        }

        private void OnDestroy()
        {
            KillTween();
        }

        private void KillTween()
        {
            if (_sequence != null)
            {
                _sequence.Kill();
                _sequence = null;
            }
        }

        private void HideAll()
        {
            if (_images == null) return;
            for (int i = 0; i < _images.Length; i++)
            {
                if (_images[i] != null) _images[i].gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Fan angle in degrees for burst star index of count: 90 is straight up, spread is the half-width.
        /// </summary>
        public static float CalculateBurstAngle(int index, int count, float spread)
        {
            if (count <= 1) return 90f;
            return 90f - spread + 2f * spread * index / (count - 1);
        }

        /// <summary>
        /// Plays the one-shot star burst animation.
        /// </summary>
        public void Play()
        {
            if (_stars == null || _stars.Length == 0) return;

            KillTween();
            if (radial)
            {
                PlayRadial();
                return;
            }
            var burst = TweenLite.Sequence();
            burst.SetUpdate(useUnscaledTime);

            int total = _stars.Length;
            for (int i = 0; i < total; i++)
            {
                var rt = _stars[i];
                var img = _images[i];
                float baseAlpha = _alphas[i];

                float angle = CalculateBurstAngle(i, total, spreadAngle) + Random.Range(-8f, 8f);
                float dist = Random.Range(distanceRange.x, distanceRange.y);
                float life = Random.Range(lifeRange.x, lifeRange.y);
                float size = Random.Range(sizeRange.x, sizeRange.y);
                float spin = Random.value < 0.5f ? -spinDegrees : spinDegrees;
                float radians = angle * Mathf.Deg2Rad;
                float targetX = Mathf.Cos(radians) * dist;
                float peakY = Mathf.Sin(radians) * dist;
                float t0 = i * staggerDelay;

                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(size, size);
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;

                Color c = img.color;
                c.a = baseAlpha;
                img.color = c;
                img.gameObject.SetActive(false);

                var starObj = img.gameObject;
                float riseTime = life * 0.4f;
                float fallTime = life * 0.6f;
                float fadeStart = life * 0.6f;
                float fadeDuration = life * 0.4f;

                var starSeq = TweenLite.Sequence();
                starSeq.AppendInterval(t0);
                starSeq.AppendCallback(() => starObj.SetActive(true));
                starSeq.Join(rt.DOAnchorPosX(targetX, life).SetEase(Ease.OutQuad));
                starSeq.Join(rt.DOAnchorPosY(peakY, riseTime).SetEase(Ease.OutQuad));
                starSeq.Join(rt.DOLocalRotate(new Vector3(0f, 0f, spin), life, RotateMode.FastBeyond360).SetEase(Ease.Linear));

                // Fall phase
                var fallSeq = TweenLite.Sequence();
                fallSeq.AppendInterval(t0 + riseTime);
                fallSeq.Append(rt.DOAnchorPosY(peakY - fallDistance, fallTime).SetEase(Ease.InQuad));
                burst.Join(fallSeq);

                // Fade phase
                var fadeSeq = TweenLite.Sequence();
                fadeSeq.AppendInterval(t0 + fadeStart);
                fadeSeq.Append(img.DOFade(0f, fadeDuration));
                fadeSeq.AppendCallback(() => starObj.SetActive(false));
                burst.Join(fadeSeq);

                burst.Join(starSeq);
            }

            _sequence = burst;
        }

        // Radial mode runs from Update with no tweens: each star pops in (scale 0 -> 1, OutBack) at the centre,
        // flies straight out evenly around a full circle, holds fully visible, then fades and shrinks at the end.
        // Every star is hidden when its life ends, so a repeated or interrupted burst never leaves stars behind.
        private float[] _angle, _dist, _life, _delay, _spin;
        private float _elapsed;
        private bool _radialPlaying;

        private void PlayRadial()
        {
            int total = _stars.Length;
            if (_angle == null || _angle.Length != total)
            {
                _angle = new float[total];
                _dist = new float[total];
                _life = new float[total];
                _delay = new float[total];
                _spin = new float[total];
            }
            for (int i = 0; i < total; i++)
            {
                _angle[i] = (360f * i / total + Random.Range(-8f, 8f)) * Mathf.Deg2Rad;
                _dist[i] = Random.Range(distanceRange.x, distanceRange.y);
                _life[i] = Random.Range(lifeRange.x, lifeRange.y);
                _delay[i] = i * staggerDelay;
                _spin[i] = Random.value < 0.5f ? -spinDegrees : spinDegrees;
                float size = Random.Range(sizeRange.x, sizeRange.y);

                RectTransform rt = _stars[i];
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(size, size);
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.zero;
                SetAlpha(i, 1f);
                _images[i].gameObject.SetActive(false);
            }
            _elapsed = 0f;
            _radialPlaying = true;
        }

        private void Update()
        {
            if (!_radialPlaying) return;
            // Luna can keep updating under a deactivated parent: stop instead of re-showing stars there.
            if (!gameObject.activeInHierarchy)
            {
                Stop();
                return;
            }
            _elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            bool any = false;
            for (int i = 0; i < _stars.Length; i++)
            {
                float local = _elapsed - _delay[i];
                GameObject star = _images[i].gameObject;
                if (local < 0f)
                {
                    any = true;
                    continue;
                }
                if (local >= _life[i])
                {
                    if (star.activeSelf) star.SetActive(false);
                    continue;
                }
                any = true;
                if (!star.activeSelf) star.SetActive(true);

                float p = local / _life[i];
                float travel = 1f - (1f - p) * (1f - p); // OutQuad
                RectTransform rt = _stars[i];
                rt.anchoredPosition = new Vector2(Mathf.Cos(_angle[i]), Mathf.Sin(_angle[i])) * (_dist[i] * travel);
                rt.localRotation = Quaternion.Euler(0f, 0f, _spin[i] * p);

                // Quick pop (first 15%), full size and alpha until 75%, then fade and shrink to 60%.
                float scale;
                if (p < 0.15f) scale = OutBack(p / 0.15f);
                else if (p > 0.75f) scale = Mathf.Lerp(1f, 0.6f, (p - 0.75f) / 0.25f);
                else scale = 1f;
                rt.localScale = new Vector3(scale, scale, 1f);
                SetAlpha(i, p > 0.75f ? 1f - (p - 0.75f) / 0.25f : 1f);
            }
            if (!any) _radialPlaying = false;
        }

        private static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        private void SetAlpha(int index, float k)
        {
            Color c = _images[index].color;
            c.a = _alphas[index] * k;
            _images[index].color = c;
        }
    }
}
