using UnityEngine;
using UnityEngine.UI;

namespace PlayableKit.Effects
{
    /// <summary>
    /// Falling UI particles (snow, rain, petals, confetti, any sprite). Duplicates an inactive template Image
    /// child count times in Awake (no Instantiate afterwards) and integrates every particle in Update:
    /// no tween per particle, no ParticleSystem, no allocation per frame (Luna-safe).
    /// Every particle has a depth d in [0,1] that sets size, fall speed and alpha together (near = big, fast, opaque).
    /// Pick preset Snow or Rain, or Custom and tune the ranges. Rain is velocity-aligned streaks (stretch = height / width).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class PAUIFallingFx : MonoBehaviour
    {
        public enum Preset { Custom, Snow, Rain }

        /// <summary>Hard cap on the pool size (cost cap for Luna).</summary>
        public const int MaxCount = 120;

        private const float MaxDt = 0.1f;
        private const float TwoPi = Mathf.PI * 2f;

        [Tooltip("Snow / Rain overwrite the fields below when changed in the Inspector. Custom leaves them alone.")]
        [SerializeField] private Preset preset = Preset.Snow;
        [HideInInspector, SerializeField] private Preset appliedPreset = Preset.Snow;

        [Tooltip("Inactive child Image used as the particle template (sprite/material). Duplicated once in Awake.")]
        [SerializeField] private Image template;
        [Tooltip("Optional sprites, assigned to particles in contiguous blocks. Empty = the template's own sprite.")]
        [SerializeField] private Sprite[] sprites = new Sprite[0];
        [SerializeField, Range(0, MaxCount)] private int count = 40;

        [Tooltip("Particle width in canvas units (far .. near).")]
        [SerializeField] private Vector2 sizeRange = new Vector2(14f, 34f);
        [Tooltip("Height = width * stretch. Rain uses a large value for streaks.")]
        [SerializeField, Min(0.1f)] private float stretch = 1f;
        [Tooltip("Fall speed in canvas units per second (far .. near).")]
        [SerializeField] private Vector2 fallSpeedRange = new Vector2(60f, 140f);
        [Tooltip("Horizontal drift in canvas units per second (+ = right). Wraps around the area.")]
        [SerializeField] private float wind = 12f;
        [Tooltip("Sine sway amplitude in canvas units.")]
        [SerializeField] private Vector2 swayAmplitudeRange = new Vector2(10f, 40f);
        [Tooltip("Sine sway frequency in Hz.")]
        [SerializeField] private Vector2 swayFrequencyRange = new Vector2(0.1f, 0.3f);
        [Tooltip("Spin in degrees per second.")]
        [SerializeField] private Vector2 spinSpeedRange = new Vector2(-30f, 30f);
        [Tooltip("Rotate each particle so its local down points along its velocity (rain streaks). Disables spin.")]
        [SerializeField] private bool alignToVelocity;
        [Tooltip("Alpha multiplier (far .. near).")]
        [SerializeField] private Vector2 alphaRange = new Vector2(0.45f, 0.9f);
        [SerializeField] private Color color = Color.white;
        [Tooltip("Spawn / despawn distance outside this rect, in canvas units.")]
        [SerializeField, Min(0f)] private float margin = 40f;
        [Tooltip("On Play the area is already full of particles instead of filling from the top.")]
        [SerializeField] private bool prewarm = true;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool useUnscaledTime;

        private RectTransform _area;
        private RectTransform[] _rt;
        private Image[] _img;
        private Vector2[] _base;
        private Vector2[] _vel;
        private float[] _age;
        private float[] _swayAmp;
        private float[] _swayFreq;
        private float[] _swayPhase;
        private float[] _rot;
        private float[] _spin;
        private bool[] _alive;
        private bool _playing;

        public bool IsPlaying
        {
            get { return _playing; }
        }

        // ---- Pure helpers (EditMode-tested) ----

        public static int ClampCount(int n)
        {
            return Mathf.Clamp(n, 0, MaxCount);
        }

        /// <summary>Z rotation in degrees that turns local down onto v. Zero velocity gives 0.</summary>
        public static float StreakAngle(Vector2 v)
        {
            return v.sqrMagnitude < 1e-6f ? 0f : Mathf.Atan2(v.x, -v.y) * Mathf.Rad2Deg;
        }

        public static float SwayOffset(float amp, float freq, float phase, float age)
        {
            return amp * Mathf.Sin(TwoPi * freq * age + phase);
        }

        public static float WrapX(float x, float min, float max)
        {
            return max <= min ? x : min + Mathf.Repeat(x - min, max - min);
        }

        /// <summary>Start Y of a particle: prewarm spreads over the area, else staggered above the top edge.</summary>
        public static float InitialY(float yMin, float yMax, float margin, bool prewarm, float u)
        {
            return prewarm
                ? Mathf.Lerp(yMin - margin, yMax + margin, u)
                : yMax + margin + u * (yMax - yMin + 2f * margin);
        }

        // ---- Presets ----

        public void ApplyPreset(Preset p)
        {
            switch (p)
            {
                case Preset.Snow:
                    count = 40;
                    sizeRange = new Vector2(14f, 34f);
                    stretch = 1f;
                    fallSpeedRange = new Vector2(60f, 140f);
                    wind = 12f;
                    swayAmplitudeRange = new Vector2(10f, 40f);
                    swayFrequencyRange = new Vector2(0.1f, 0.3f);
                    spinSpeedRange = new Vector2(-30f, 30f);
                    alignToVelocity = false;
                    alphaRange = new Vector2(0.45f, 0.9f);
                    color = Color.white;
                    margin = 40f;
                    break;
                case Preset.Rain:
                    count = 80;
                    sizeRange = new Vector2(3f, 5f);
                    stretch = 14f;
                    fallSpeedRange = new Vector2(1600f, 2400f);
                    wind = -200f;
                    swayAmplitudeRange = Vector2.zero;
                    swayFrequencyRange = Vector2.zero;
                    spinSpeedRange = Vector2.zero;
                    alignToVelocity = true;
                    alphaRange = new Vector2(0.2f, 0.45f);
                    color = new Color(0.8f, 0.9f, 1f, 1f);
                    margin = 80f;
                    break;
            }
        }

        private void Reset()
        {
            ApplyPreset(preset);
            appliedPreset = preset;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (preset != appliedPreset)
            {
                ApplyPreset(preset);
                appliedPreset = preset;
            }
        }
#endif

        // ---- Lifecycle ----

        private void Awake()
        {
            _area = (RectTransform)transform;
            int n = template == null ? 0 : ClampCount(count);

            _rt = new RectTransform[n];
            _img = new Image[n];
            _base = new Vector2[n];
            _vel = new Vector2[n];
            _age = new float[n];
            _swayAmp = new float[n];
            _swayFreq = new float[n];
            _swayPhase = new float[n];
            _rot = new float[n];
            _spin = new float[n];
            _alive = new bool[n];

            if (n == 0)
            {
                return;
            }

            template.gameObject.SetActive(false);
            int spriteCount = sprites != null ? sprites.Length : 0;

            for (int i = 0; i < n; i++)
            {
                Image img = Instantiate(template, _area, false);
                img.name = "Fx_" + i;
                img.raycastTarget = false;
                if (spriteCount > 0)
                {
                    img.sprite = sprites[i * spriteCount / n];
                }

                RectTransform rt = img.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                img.enabled = false;
                img.gameObject.SetActive(true);

                _rt[i] = rt;
                _img[i] = img;
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
            HideAll();
            _playing = false;
        }

        // ---- Public API ----

        /// <summary>Starts (or resumes) emission; every particle not currently falling spawns now.</summary>
        public void Play()
        {
            _playing = true;
            if (_alive == null)
            {
                return;
            }

            for (int i = 0; i < _alive.Length; i++)
            {
                if (!_alive[i])
                {
                    Spawn(i, true);
                }
            }
        }

        /// <summary>Stops emission. clear = hide everything now, else particles finish falling out.</summary>
        public void Stop(bool clear)
        {
            _playing = false;
            if (clear)
            {
                HideAll();
            }
        }

        private void HideAll()
        {
            if (_alive == null)
            {
                return;
            }

            for (int i = 0; i < _alive.Length; i++)
            {
                _alive[i] = false;
                _img[i].enabled = false;
            }
        }

        // ---- Simulation ----

        private void Spawn(int i, bool first)
        {
            Rect r = _area.rect;
            float m = Mathf.Max(margin, sizeRange.y * stretch);
            float d = Random.value;

            float w = Mathf.Lerp(sizeRange.x, sizeRange.y, d);
            _rt[i].sizeDelta = new Vector2(w, w * stretch);

            _vel[i] = new Vector2(wind, -Mathf.Lerp(fallSpeedRange.x, fallSpeedRange.y, d));

            Color c = color;
            c.a *= Mathf.Lerp(alphaRange.x, alphaRange.y, d);
            _img[i].color = c;

            float x = Random.Range(r.xMin, r.xMax);
            float y = first ? InitialY(r.yMin, r.yMax, m, prewarm, Random.value) : r.yMax + m;
            _base[i] = new Vector2(x, y);

            _swayAmp[i] = Random.Range(swayAmplitudeRange.x, swayAmplitudeRange.y);
            _swayFreq[i] = Random.Range(swayFrequencyRange.x, swayFrequencyRange.y);
            _swayPhase[i] = Random.Range(0f, TwoPi);

            if (alignToVelocity)
            {
                _rot[i] = StreakAngle(_vel[i]);
                _spin[i] = 0f;
            }
            else
            {
                _rot[i] = Random.Range(0f, 360f);
                _spin[i] = Random.Range(spinSpeedRange.x, spinSpeedRange.y);
            }

            _age[i] = 0f;
            _rt[i].localRotation = Quaternion.Euler(0f, 0f, _rot[i]);
            _rt[i].anchoredPosition = _base[i] - r.center;
            _img[i].enabled = true;
            _alive[i] = true;
        }

        private void Update()
        {
            if (_alive == null || _alive.Length == 0)
            {
                return;
            }

            float dt = Mathf.Min(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime, MaxDt);
            Rect r = _area.rect;
            float m = Mathf.Max(margin, sizeRange.y * stretch);
            Vector2 center = r.center;
            float xMin = r.xMin - m;
            float xMax = r.xMax + m;
            float yMin = r.yMin - m;

            for (int i = 0; i < _alive.Length; i++)
            {
                if (!_alive[i])
                {
                    continue;
                }

                _age[i] += dt;
                Vector2 p = _base[i] + _vel[i] * dt;
                p.x = WrapX(p.x, xMin, xMax);
                _base[i] = p;

                if (p.y < yMin)
                {
                    if (_playing)
                    {
                        Spawn(i, false);
                    }
                    else
                    {
                        _alive[i] = false;
                        _img[i].enabled = false;
                    }

                    continue;
                }

                float sway = SwayOffset(_swayAmp[i], _swayFreq[i], _swayPhase[i], _age[i]);
                _rt[i].anchoredPosition = new Vector2(p.x + sway - center.x, p.y - center.y);

                if (_spin[i] != 0f)
                {
                    _rot[i] += _spin[i] * dt;
                    _rt[i].localRotation = Quaternion.Euler(0f, 0f, _rot[i]);
                }
            }
        }
    }
}
