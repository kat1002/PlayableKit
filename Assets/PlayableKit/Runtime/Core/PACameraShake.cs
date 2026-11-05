using UnityEngine;

namespace PlayableKit
{
    /// <summary>
    /// Positional camera shake for the crash impact. Works on localPosition, so the camera
    /// can be a child of a rig that handles lane movement without the two fighting.
    /// </summary>
    public class PACameraShake : MonoBehaviour
    {
        private Vector3 _basePos;
        private bool _hasBase;
        private float _time;
        private float _duration;
        private float _amplitude;
        private float _continuousAmplitude;

        private void Awake()
        {
            if (!_hasBase) SetBase(transform.localPosition);
        }

        /// <summary>Call after the owner has finished positioning the camera.</summary>
        public void SetBase(Vector3 localPos)
        {
            _basePos = localPos;
            _hasBase = true;
        }

        public void Shake(float amplitude, float duration)
        {
            _amplitude = amplitude;
            _duration = duration;
            _time = duration;
        }

        /// <summary>Continuous low rumble whose strength tracks current speed (0 = still, 1 = max speed).</summary>
        public void SetContinuousIntensity(float t)
        {
            _continuousAmplitude = Mathf.Clamp01(t);
        }

        private void LateUpdate()
        {
            if (_time > 0f)
            {
                _time -= Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(_time / _duration);
                Vector3 offset = new Vector3(Random.value - 0.5f, Random.value - 0.5f, 0f) * (_amplitude * k);
                transform.localPosition = _basePos + offset;
                return;
            }

            if (_continuousAmplitude > 0f)
            {
                float nx = Mathf.PerlinNoise(Time.unscaledTime * 18f, 0f) - 0.5f;
                float ny = Mathf.PerlinNoise(0f, Time.unscaledTime * 18f) - 0.5f;
                Vector3 rumble = new Vector3(nx, ny, 0f) * (0.03f * _continuousAmplitude);
                transform.localPosition = _basePos + rumble;
            }
            else
            {
                transform.localPosition = _basePos;
            }
        }
    }
}
