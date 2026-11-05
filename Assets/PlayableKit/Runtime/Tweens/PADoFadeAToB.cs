using UnityEngine;
using PlayableKit.Tweening;

namespace PlayableKit
{
    public class PADoFadeAToB : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private CanvasGroup target;

        [Header("Fade")]
        [SerializeField] private float valueA = 1f;
        [SerializeField] private float valueB = 0f;
        [SerializeField] private float duration = 0.25f;
        [SerializeField] private Ease ease = Ease.Linear;

        [Header("Loop")]
        [SerializeField] private bool useLoop = false;
        [SerializeField] private int loopCount = -1; // -1 = infinite
        [SerializeField] private LoopType loopType = LoopType.Yoyo;

        [Header("Behavior")]
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool setToAOnEnable = true;

        private Tween _tween;

        private void Reset()
        {
            target = GetComponent<CanvasGroup>();

            valueA = 1f;
            valueB = 0f;
            duration = 0.25f;
            ease = Ease.Linear;

            useLoop = false;
            loopCount = -1;
            loopType = LoopType.Yoyo;

            playOnEnable = true;
            setToAOnEnable = true;

            if (target != null)
                target.alpha = setToAOnEnable ? valueA : valueB;
        }

        private void OnEnable()
        {
            if (target == null)
                target = GetComponent<CanvasGroup>();

            if (target == null)
                return;

            if (setToAOnEnable)
                target.alpha = valueA;

            if (playOnEnable)
                Play();
        }

        private void OnDisable()
        {
            KillTween();
        }

        public void Play()
        {
            if (target == null)
                return;

            KillTween();

            target.alpha = valueA;

            _tween = target
                .DOFade(valueB, duration)
                .SetEase(ease)
                .SetUpdate(false);

            if (useLoop)
                _tween.SetLoops(loopCount, loopType);
        }

        public void Stop(bool complete = false)
        {
            if (_tween == null)
                return;

            if (complete)
                _tween.Complete();

            KillTween();
        }

        public void SetFadeValues(float a, float b)
        {
            valueA = a;
            valueB = b;
        }

        private void KillTween()
        {
            if (_tween == null)
                return;

            _tween.Kill();
            _tween = null;
        }
    }

}
