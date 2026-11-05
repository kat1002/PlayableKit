using UnityEngine;
using UnityEngine.UI;

namespace PlayableKit
{
    /// <summary>Speed-driven wind feedback for the fall: overlay, vignette, wind loop and camera
    /// wind/shake, all scaled by the target's downward speed.</summary>
    public sealed class FallWindFx : MonoBehaviour
    {
        private const float LandKickAmp = 0.35f;
        private const float LandKickDuration = 0.35f;

        [Header("References")]
        [SerializeField, Tooltip("Optional. Must implement IWindFxReceiver (e.g. a camera that shakes).")] private MonoBehaviour windReceiver;
        [SerializeField] private Graphic windOverlay;
        [SerializeField] private Graphic vignette;
        [SerializeField] private AudioSource windLoop;

        [Header("Speed mapping")]
        [SerializeField, Min(0f)] private float minSpeed = 2f;
        [SerializeField, Min(0.01f)] private float maxSpeed = 18f;
        [Tooltip("Only downward speed counts.")]
        [SerializeField] private bool verticalOnly = true;
        [SerializeField, Min(0.01f)] private float intensityResponse = 3f;
        [SerializeField, Min(0.01f)] private float fadeIn = 0.25f;
        [SerializeField, Min(0.01f)] private float landFadeOut = 0.2f;

        [Header("Overlay / vignette")]
        [Tooltip("x = alpha at zero intensity, y = alpha at full intensity.")]
        [SerializeField] private Vector2 overlayAlpha = new Vector2(0.25f, 1f);
        [SerializeField] private Vector2 vignetteAlpha = new Vector2(0f, 0.55f);

        [Header("Wind audio")]
        [SerializeField] private Vector2 windVolume = new Vector2(0.25f, 1f);
        [SerializeField] private Vector2 windPitch = new Vector2(0.9f, 1.2f);

        private IWindFxReceiver _receiver;
        private Transform _target;
        private Vector3 _lastPosition;
        private bool _hasLastPosition;
        private bool _running;
        private bool _landing;
        private float _intensity;
        private float _fadeInTimer;
        private float _landTimer;

        private void Awake()
        {
            _receiver = windReceiver as IWindFxReceiver;
        }

        public void Begin(Transform target)
        {
            _target = target;
            _hasLastPosition = false;
            _running = true;
            _landing = false;
            _intensity = 0f;
            _fadeInTimer = 0f;
            _landTimer = 0f;

            SetAlpha(windOverlay, 0f, true);
            SetAlpha(vignette, 0f, true);

            if (windLoop != null)
            {
                windLoop.volume = 0f;
                if (!windLoop.isPlaying)
                {
                    windLoop.Play();
                }
            }

            if (_receiver != null)
            {
                _receiver.SetWindIntensity(0f);
            }
        }

        public void Land()
        {
            if (!_running || _landing)
            {
                return;
            }

            _landing = true;
            _landTimer = 0f;
            if (_receiver != null)
            {
                _receiver.Kick(LandKickAmp, LandKickDuration);
            }
        }

        public void StopImmediate()
        {
            _running = false;
            _landing = false;
            _intensity = 0f;
            _target = null;

            if (windLoop != null)
            {
                windLoop.volume = 0f;
                windLoop.Stop();
            }

            SetAlpha(windOverlay, 0f, false);
            SetAlpha(vignette, 0f, false);
            if (windOverlay != null)
            {
                windOverlay.gameObject.SetActive(false);
            }

            if (vignette != null)
            {
                vignette.gameObject.SetActive(false);
            }

            if (_receiver != null)
            {
                _receiver.SetWindIntensity(0f);
            }
        }

        private void Update()
        {
            if (!_running)
            {
                return;
            }

            // The fall runs at timeScale 1 (the slow-mo ends before the fall starts), so scaled time matches
            // the target's velocity.
            var dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            var velocity = Vector3.zero;
            if (_target != null)
            {
                var position = _target.position;
                if (_hasLastPosition)
                {
                    velocity = (position - _lastPosition) / dt;
                }

                _lastPosition = position;
                _hasLastPosition = true;
            }

            var speed = verticalOnly ? Mathf.Max(0f, -velocity.y) : velocity.magnitude;
            // Ratchet: once the wind has built up it holds until the player hits the ground (Land()).
            var target = Mathf.Max(Mathf.InverseLerp(minSpeed, maxSpeed, speed), _intensity);
            _intensity = Mathf.Lerp(_intensity, target, 1f - Mathf.Exp(-intensityResponse * dt));

            _fadeInTimer += dt;
            var envelope = Mathf.Clamp01(_fadeInTimer / fadeIn);
            if (_landing)
            {
                _landTimer += dt;
                envelope *= 1f - Mathf.Clamp01(_landTimer / landFadeOut);
                if (_landTimer >= landFadeOut)
                {
                    StopImmediate();
                    return;
                }
            }

            Apply(_intensity * envelope, envelope, dt);
        }

        private void Apply(float intensity, float envelope, float dt)
        {
            SetAlpha(windOverlay, Mathf.Lerp(overlayAlpha.x, overlayAlpha.y, intensity) * envelope, false);

            SetAlpha(vignette, Mathf.Clamp01(Mathf.Lerp(vignetteAlpha.x, vignetteAlpha.y, intensity)) * envelope, false);

            if (windLoop != null)
            {
                windLoop.volume = Mathf.Lerp(windVolume.x, windVolume.y, intensity) * envelope;
                windLoop.pitch = Mathf.Lerp(windPitch.x, windPitch.y, intensity);
            }

            if (_receiver != null)
            {
                _receiver.SetWindIntensity(intensity);
            }
        }

        private static void SetAlpha(Graphic graphic, float alpha, bool activate)
        {
            if (graphic == null)
            {
                return;
            }

            if (activate && !graphic.gameObject.activeSelf)
            {
                graphic.gameObject.SetActive(true);
            }

            var color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        private void OnDisable()
        {
            if (_running)
            {
                StopImmediate();
            }
        }
    }
}
