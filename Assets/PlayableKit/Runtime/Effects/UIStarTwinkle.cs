using Playable.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableKit
{
    /// <summary>
    /// Twinkling UI stars ("bling"). Duplicates an inactive template Image child count times
    /// in Awake (no per-frame Instantiate), then loops each star: delay, pop in, spin, hold, pop out, new spot.
    /// Restarted in OnEnable and hidden in OnDisable (panel is toggled with SetActive). Update-driven, no Sequence (Luna-safe).
    /// </summary>
    public sealed class UIStarTwinkle : MonoBehaviour
    {
        [Tooltip("Inactive child Image used as the star template (sprite/material). Duplicated once in Awake.")]
        [SerializeField] private Image template;
        [SerializeField, Range(1, 24)] private int count = 8;
        [Tooltip("0 = anywhere in the ellipse of this rect; 0.45 = keep out of the inner 45% (image centre).")]
        [SerializeField, Range(0f, 0.95f)] private float innerRadius = 0.45f;
        [SerializeField] private Vector2 sizeRange = new Vector2(40f, 90f);
        [SerializeField] private Vector2 delayRange = new Vector2(0.05f, 0.6f);
        [SerializeField] private Vector2 holdRange = new Vector2(0.15f, 0.4f);
        [SerializeField, Min(0.01f)] private float popInDuration = 0.25f;
        [SerializeField, Min(0.01f)] private float popOutDuration = 0.3f;
        [SerializeField] private float spinDegrees = 90f;
        [SerializeField] private Color colorA = Color.white;
        [SerializeField] private Color colorB = new Color(1f, 0.95f, 0.6f, 1f);
        [SerializeField] private bool useUnscaledTime;

        private enum Stage { Wait, PopIn, Hold, PopOut }

        private RectTransform _area;
        private RectTransform[] _stars;
        private Image[] _images;
        private Stage[] _stage;
        private float[] _timer;
        private float[] _delay;
        private float[] _hold;
        private float[] _rotZ;
        private Color[] _tint;

        private void Awake()
        {
            _area = (RectTransform)transform;

            if (template == null)
            {
                _stars = new RectTransform[0];
                _images = new Image[0];
                AllocState(0);
                return;
            }

            template.gameObject.SetActive(false);
            _stars = new RectTransform[count];
            _images = new Image[count];
            AllocState(count);

            for (var i = 0; i < count; i++)
            {
                var img = Instantiate(template, _area, false);
                img.name = "Star_" + i;
                img.raycastTarget = false;

                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.localScale = Vector3.zero;
                img.gameObject.SetActive(true);

                _stars[i] = rt;
                _images[i] = img;
            }
        }

        private void AllocState(int n)
        {
            _stage = new Stage[n];
            _timer = new float[n];
            _delay = new float[n];
            _hold = new float[n];
            _rotZ = new float[n];
            _tint = new Color[n];
        }

        private void OnEnable()
        {
            if (_stars == null)
            {
                return;
            }

            for (var i = 0; i < _stars.Length; i++)
            {
                SpawnStar(i, true);
            }
        }

        private void OnDisable()
        {
            if (_stars == null)
            {
                return;
            }

            for (var i = 0; i < _stars.Length; i++)
            {
                _stage[i] = Stage.Wait;
                _stars[i].localScale = Vector3.zero;
            }
        }

        private void SpawnStar(int i, bool first)
        {
            var star = _stars[i];
            var img = _images[i];

            // Randomise now, while the star is invisible (scale 0 / alpha 0).
            var size = Random.Range(sizeRange.x, sizeRange.y);
            var rect = _area.rect;
            var angle = Random.Range(0f, Mathf.PI * 2f);
            var r = Random.Range(innerRadius, 1f);
            var halfW = Mathf.Max(0f, rect.width * 0.5f - size * 0.5f);
            var halfH = Mathf.Max(0f, rect.height * 0.5f - size * 0.5f);
            star.anchoredPosition = rect.center + new Vector2(Mathf.Cos(angle) * halfW * r, Mathf.Sin(angle) * halfH * r);
            star.sizeDelta = new Vector2(size, size);
            star.localScale = Vector3.zero;

            _rotZ[i] = Random.Range(0f, 360f);
            star.localEulerAngles = new Vector3(0f, 0f, _rotZ[i]);

            var tint = Color.Lerp(colorA, colorB, Random.value);
            _tint[i] = tint;
            img.color = new Color(tint.r, tint.g, tint.b, 0f);

            _hold[i] = Random.Range(holdRange.x, holdRange.y);
            var delay = Random.Range(delayRange.x, delayRange.y);
            if (first)
            {
                // Stagger the first wave.
                delay += Random.Range(0f, popInDuration + holdRange.y + popOutDuration);
            }

            _delay[i] = delay;
            _timer[i] = 0f;
            _stage[i] = Stage.Wait;
        }

        private void Update()
        {
            if (_stars == null)
            {
                return;
            }

            float dt = Mathf.Min(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime, 0.1f);

            for (var i = 0; i < _stars.Length; i++)
            {
                var star = _stars[i];
                var tint = _tint[i];
                _timer[i] += dt;
                var t = _timer[i];

                switch (_stage[i])
                {
                    case Stage.Wait:
                        if (t >= _delay[i])
                        {
                            _timer[i] = t - _delay[i];
                            _stage[i] = Stage.PopIn;
                        }
                        break;

                    case Stage.PopIn:
                    {
                        float p = Mathf.Clamp01(t / popInDuration);
                        star.localScale = Vector3.one * TweenLite.ApplyEase(Ease.OutBack, p);
                        _images[i].color = new Color(tint.r, tint.g, tint.b, tint.a * Mathf.Clamp01(t / (popInDuration * 0.8f)));
                        if (p >= 1f)
                        {
                            _timer[i] = t - popInDuration;
                            _stage[i] = Stage.Hold;
                        }
                        break;
                    }

                    case Stage.Hold:
                        star.localScale = Vector3.one;
                        _images[i].color = tint;
                        if (t >= _hold[i])
                        {
                            _timer[i] = t - _hold[i];
                            _stage[i] = Stage.PopOut;
                        }
                        break;

                    case Stage.PopOut:
                    {
                        float p = Mathf.Clamp01(t / popOutDuration);
                        star.localScale = Vector3.one * (1f - TweenLite.ApplyEase(Ease.InBack, p));
                        _images[i].color = new Color(tint.r, tint.g, tint.b, tint.a * (1f - p));
                        if (p >= 1f)
                        {
                            SpawnStar(i, false);
                            continue;
                        }
                        break;
                    }
                }

                if (_stage[i] != Stage.Wait)
                {
                    // Linear spin spread over the whole visible life.
                    float elapsed = _stage[i] == Stage.PopIn ? _timer[i]
                        : _stage[i] == Stage.Hold ? popInDuration + _timer[i]
                        : popInDuration + _hold[i] + _timer[i];
                    float life = popInDuration + _hold[i] + popOutDuration;
                    star.localEulerAngles = new Vector3(0f, 0f, _rotZ[i] + spinDegrees * Mathf.Clamp01(elapsed / life));
                }
            }
        }
    }
}
