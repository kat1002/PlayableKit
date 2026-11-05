using UnityEngine;
using Playable.Tweening;

namespace PlayableKit
{
    public class DoScaleAToB : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        
        [Header("Scale")]
        [SerializeField] private Vector3 sizeA = Vector3.one;
        [SerializeField] private Vector3 sizeB = Vector3.one * 1.2f;
        [SerializeField] private float duration = 0.25f;
        [SerializeField] private Ease ease = Ease.OutBack;

        [Header("Loop")]
        [SerializeField] private bool useLoop = false;
        [SerializeField] private int loopCount = -1; // -1 = infinite
        [SerializeField] private LoopType loopType = LoopType.Yoyo;

        [Header("Behavior")]
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool setToAOnEnable = true;
        [Tooltip("Run on unscaled time so the tween keeps playing while Time.timeScale is 0 (e.g. a tap prompt over a paused game).")]
        [SerializeField] private bool ignoreTimeScale = false;

        private Tween _tween;

        public Vector3 SizeA => sizeA;

        private void Reset()
        {
            target = transform;
            sizeA = Vector3.one;
            sizeB = Vector3.one * 1.2f;
            duration = 0.25f;
            ease = Ease.OutBack;
            useLoop = false;
            loopCount = -1;
            loopType = LoopType.Yoyo;
            playOnEnable = true;
            setToAOnEnable = true;
        }

        private void OnEnable()
        {
            if (target == null) target = transform;

            if (setToAOnEnable)
                target.localScale = sizeA;

            if (playOnEnable)
                Play();
        }

        private void OnDisable()
        {
            KillTween();
        }

        public void Play()
        {
            if (target == null) return;

            KillTween();

            target.localScale = sizeA;

            _tween = target
                .DOScale(sizeB, duration)
                .SetEase(ease)
                .SetUpdate(ignoreTimeScale);

            if (useLoop)
                _tween.SetLoops(loopCount, loopType);
        }

        public void Stop(bool resetToA = false)
        {
            KillTween();

            if (resetToA && target != null)
                target.localScale = sizeA;
        }

        public void SetSizes(Vector3 a, Vector3 b)
        {
            sizeA = a;
            sizeB = b;
        }

        private void KillTween()
        {
            if (_tween == null) return;
            _tween.Kill();
            _tween = null;
        }
    }

}
