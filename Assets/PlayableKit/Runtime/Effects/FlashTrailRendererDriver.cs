using UnityEngine;
using UnityEngine.Serialization;

namespace PlayableKit
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrailRenderer))]
    public class FlashTrailRendererDriver : MonoBehaviour
    {
        [SerializeField] Transform motionSource;
        [SerializeField] float startEmitSpeed = 0.75f;
        [SerializeField] float stopEmitSpeed = 0.3f;
        [SerializeField] float fullEffectSpeed = 6f;
        [SerializeField] float noiseFrequency = 11f;
        [FormerlySerializedAs("maxJitter")]
        [SerializeField] Vector3 maxRotationJitter = new Vector3(0.16f, 0.08f, 0.1f);
        [FormerlySerializedAs("positionSmoothTime")]
        [SerializeField] float rotationSmoothTime = 0.035f;
        [SerializeField] float minTrailTime = 0.18f;
        [SerializeField] float maxTrailTime = 0.55f;
        [SerializeField] float minWidth = 0.18f;
        [SerializeField] float maxWidth = 0.42f;

        TrailRenderer trail;
        Vector3 baseLocalPosition;
        Quaternion baseLocalRotation;
        Vector3 lastSourcePosition;
        bool initialized;
        bool isEmitting;
        float noiseSeedX;
        float noiseSeedY;
        float noiseSeedZ;

        void Awake()
        {
            trail = GetComponent<TrailRenderer>();
            ResolveMotionSource();
            CacheBaseTransform();
            noiseSeedX = Random.value * 100f;
            noiseSeedY = Random.value * 100f;
            noiseSeedZ = Random.value * 100f;
            ResetLocalTransform();
            ApplyStoppedState(true);
        }

        void OnEnable()
        {
            ResolveMotionSource();
            CacheBaseTransform();
            if (motionSource != null)
                lastSourcePosition = motionSource.position;

            initialized = false;
            ResetLocalTransform();
            ApplyStoppedState(true);
        }

        void LateUpdate()
        {
            ResolveMotionSource();
            if (motionSource == null)
            {
                ResetLocalTransform();
                return;
            }

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 currentPosition = motionSource.position;

            if (!initialized)
            {
                initialized = true;
                lastSourcePosition = currentPosition;
                ResetLocalTransform();
                return;
            }

            float speed = (currentPosition - lastSourcePosition).magnitude / dt;
            lastSourcePosition = currentPosition;

            if (isEmitting)
            {
                if (speed <= stopEmitSpeed)
                    ApplyStoppedState(false);
            }
            else if (speed >= startEmitSpeed)
            {
                isEmitting = true;
                trail.emitting = true;
            }

            if (!isEmitting)
            {
                transform.localPosition = baseLocalPosition;
                SmoothLocalRotation(baseLocalRotation, dt);
                return;
            }

            float speed01 = Mathf.InverseLerp(startEmitSpeed, fullEffectSpeed, speed);
            float noiseTime = Time.time * noiseFrequency;
            Vector3 rotationOffset = new Vector3(
                SignedNoise(noiseSeedX, noiseTime) * maxRotationJitter.x,
                SignedNoise(noiseSeedY, noiseTime * 1.17f) * maxRotationJitter.y,
                SignedNoise(noiseSeedZ, noiseTime * 0.93f) * maxRotationJitter.z) * (Mathf.Rad2Deg * speed01);

            transform.localPosition = baseLocalPosition;
            SmoothLocalRotation(baseLocalRotation * Quaternion.Euler(rotationOffset), dt);

            trail.time = Mathf.Lerp(minTrailTime, maxTrailTime, speed01);
            trail.widthMultiplier = Mathf.Lerp(minWidth, maxWidth, speed01);
        }

        void CacheBaseTransform()
        {
            baseLocalPosition = transform.localPosition;
            baseLocalRotation = transform.localRotation;
        }

        void ResetLocalTransform()
        {
            transform.localPosition = baseLocalPosition;
            transform.localRotation = baseLocalRotation;
        }

        void SmoothLocalRotation(Quaternion targetRotation, float dt)
        {
            if (rotationSmoothTime <= 0f)
            {
                transform.localRotation = targetRotation;
                return;
            }

            float t = 1f - Mathf.Exp(-dt / rotationSmoothTime);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, t);
        }

        void ResolveMotionSource()
        {
            if (motionSource == null && transform.parent != null)
                motionSource = transform.parent;
        }

        void ApplyStoppedState(bool clearTrail)
        {
            isEmitting = false;
            if (trail == null)
                trail = GetComponent<TrailRenderer>();

            trail.emitting = false;
            trail.time = minTrailTime;
            trail.widthMultiplier = minWidth;
            if (clearTrail)
                trail.Clear();
        }

        static float SignedNoise(float seed, float timeValue)
        {
            return Mathf.PerlinNoise(seed, timeValue) * 2f - 1f;
        }
    }
}
