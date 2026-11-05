using PlayableKit.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableKit
{
    public enum UIShineDirection
    {
        RightToLeft = 0,
        LeftToRight = 1,
        PingPong = 2
    }

    /// <summary>
    /// Drives a diagonal "shine" sweep across a UI Image (e.g. an icon) through a shine shader's
    /// _ShineLocation property. Clones the material in Awake so shared assets are untouched. Direction, looping,
    /// loop count and play-on-enable are configurable (loop is off by default). The shine angle is aspect-corrected
    /// from the drawn area, so it looks the same on non-square images; Play/PlayNow/Stop can be called from code or the
    /// Inspector buttons. Started in OnEnable (if playOnEnable) and killed in OnDisable (panel is toggled with
    /// SetActive). Update-driven, no Sequence (Luna-safe).
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class PAUIShine : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float sweepDuration = 0.7f;
        [Tooltip("Pause between sweeps.")]
        [SerializeField, Min(0f)] private float interval = 1.5f;
        [Tooltip("Delay before the first sweep (lets the pop-in finish).")]
        [SerializeField, Min(0f)] private float startDelay = 0.6f;
        [SerializeField] private Ease ease = Ease.InOutSine;
        [SerializeField] private bool useUnscaledTime;

        [Header("Playback")]
        [Tooltip("Start automatically when enabled. If off, call Play().")]
        [SerializeField] private bool playOnEnable = true;
        [Tooltip("Repeat the sweep. Off = a single sweep per Play.")]
        [SerializeField] private bool loop = false;
        [Tooltip("Number of sweeps when Loop is on. -1 = infinite. PingPong counts each one-way pass.")]
        [SerializeField, Min(-1)] private int loopCount = -1;
        [Tooltip("RightToLeft, LeftToRight, or PingPong (alternates with no reset jump).")]
        [SerializeField] private UIShineDirection direction = UIShineDirection.RightToLeft;

        [Header("Look")]
        [Tooltip("Band angle in degrees from vertical, as seen on screen. + = leans like '/', - = like '\\', 0 = vertical. Overrides the material's Shine Angle.")]
        [SerializeField, Range(-80f, 80f)] private float angle = 17f;
        [Tooltip("Band width as a fraction of the image width (0.15 = 15%). Overrides the material's Shine Width.")]
        [SerializeField, Range(0.01f, 1f)] private float bandWidth = 0.15f;

        private static readonly int ShineLocationId = Shader.PropertyToID("_ShineLocation");
        private static readonly int ShineUVRectId = Shader.PropertyToID("_ShineUVRect");
        private static readonly int ShineAspectId = Shader.PropertyToID("_ShineAspect");
        private static readonly int ShineAngleId = Shader.PropertyToID("_ShineAngle");
        private static readonly int ShineWidthId = Shader.PropertyToID("_ShineWidth");

        private Image _image;
        private Material _mat;
        private enum Phase { Idle, Delay, Sweep, Interval }
        private Phase _phase;
        private float _timer;
        private float _delay;
        private int _loopsTotal;
        private int _sweepsDone;
        private float _from;
        private float _to;
        private bool _reverse;
        private bool _playRequested;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _mat = new Material(_image.material);
            _mat.name += " (Instance)";
            _image.material = _mat;

            if (!_mat.HasProperty(ShineLocationId))
            {
                Debug.LogWarning("PAUIShine: material has no _ShineLocation property, disabling.", this);
                enabled = false;
                return;
            }

            // Shader samples the sprite inside an atlas, so it needs the sprite's UV rect.
            // Computed from textureRect rather than Sprites.DataUtility, which Luna may not implement.
            var sprite = _image.sprite;
            if (sprite != null && sprite.texture != null)
            {
                var r = sprite.textureRect;
                float w = sprite.texture.width, h = sprite.texture.height;
                _mat.SetVector(ShineUVRectId, new Vector4(r.x / w, r.y / h, r.width / w, r.height / h));
            }

            RefreshGeometry();
        }

        // Angle is in screen degrees; convert to UV slant using the drawn area's aspect so it looks the same on any shape.
        private void RefreshGeometry()
        {
            var rt = (RectTransform)transform;
            float aspect = 1f;
            var sp = _image.sprite;
            if (_image.preserveAspect && sp != null && sp.rect.height > 0f)
            {
                aspect = sp.rect.width / sp.rect.height;
            }
            else if (rt.rect.height > 0f)
            {
                aspect = rt.rect.width / rt.rect.height;
            }
            _mat.SetFloat(ShineAspectId, aspect);
            _mat.SetFloat(ShineAngleId, angle);
            _mat.SetFloat(ShineWidthId, bandWidth);

            float slant = Mathf.Tan(angle * Mathf.Deg2Rad) / aspect;
            float hw = bandWidth * 0.5f;
            float pad = hw + Mathf.Abs(slant) * 0.5f;
            _from = 1f + pad;
            _to = -pad;
        }

        public void Play()
        {
            PlayInternal(startDelay);
        }

        public void PlayNow()
        {
            PlayInternal(0f);
        }

        public void Stop()
        {
            _playRequested = false;
            StopPlayback();
            _reverse = false;
            if (_mat != null)
            {
                SetLocation(_from);
            }
        }

        [ContextMenu("Play Shine")]
        private void ContextPlay()
        {
            if (Application.isPlaying)
            {
                PlayNow();
            }
        }

        [ContextMenu("Stop Shine")]
        private void ContextStop()
        {
            if (Application.isPlaying)
            {
                Stop();
            }
        }

        private void PlayInternal(float delay)
        {
            if (!isActiveAndEnabled || _mat == null)
            {
                _playRequested = true;
                return;
            }

            StartPlayback(delay);
        }

        private void OnEnable()
        {
            if (_mat == null)
            {
                return;
            }

            SetLocation(_from);

            if (playOnEnable || _playRequested)
            {
                _playRequested = false;
                StartPlayback(startDelay);
            }
        }

        private void OnDisable()
        {
            StopPlayback();
            _reverse = false;

            if (_mat != null)
            {
                SetLocation(_from);
            }
        }

        private void OnDestroy()
        {
            if (_mat != null)
            {
                Destroy(_mat);
            }
        }

        private int ResolveLoops()
        {
            if (!loop)
            {
                return 1;
            }

            return loopCount < 0 ? -1 : Mathf.Max(1, loopCount);
        }

        private void ApplyProgress(float t)
        {
            float a = _reverse ? _to : _from;
            float b = _reverse ? _from : _to;
            SetLocation(Mathf.LerpUnclamped(a, b, t));
        }

        private void StopPlayback()
        {
            _phase = Phase.Idle;
            _timer = 0f;
        }

        private void StartPlayback(float delay)
        {
            StopPlayback();
            RefreshGeometry();
            _reverse = direction == UIShineDirection.LeftToRight;
            ApplyProgress(0f);

            _loopsTotal = ResolveLoops();
            _sweepsDone = 0;
            _delay = delay;
            _phase = delay > 0f ? Phase.Delay : Phase.Sweep;
        }

        // Update-driven instead of a Sequence (not Luna-safe). Both endpoints are off-screen (pad in RefreshGeometry),
        // so the restart jump between sweeps is invisible.
        private void Update()
        {
            if (_phase == Phase.Idle || _mat == null)
            {
                return;
            }

            float dt = Mathf.Min(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime, 0.1f);
            _timer += dt;

            switch (_phase)
            {
                case Phase.Delay:
                    if (_timer >= _delay)
                    {
                        _timer = 0f;
                        _phase = Phase.Sweep;
                    }
                    break;

                case Phase.Sweep:
                    float p = Mathf.Clamp01(_timer / sweepDuration);
                    ApplyProgress(TweenLite.ApplyEase(ease, p));
                    if (p >= 1f)
                    {
                        _sweepsDone++;
                        _timer = 0f;
                        _phase = (_loopsTotal >= 0 && _sweepsDone >= _loopsTotal) ? Phase.Idle : Phase.Interval;
                    }
                    break;

                case Phase.Interval:
                    if (_timer >= interval)
                    {
                        if (direction == UIShineDirection.PingPong)
                        {
                            _reverse = !_reverse;
                        }
                        ApplyProgress(0f);
                        _timer = 0f;
                        _phase = Phase.Sweep;
                    }
                    break;
            }
        }

        private void SetLocation(float value)
        {
            _mat.SetFloat(ShineLocationId, value);
            if (_image != null)
            {
                _image.SetMaterialDirty();
            }
        }
    }
}
