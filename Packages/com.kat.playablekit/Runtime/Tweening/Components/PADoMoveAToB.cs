using UnityEngine;
using PlayableKit.Tweening;

namespace PlayableKit.Tweening
{
    public class PADoMoveAToB : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Position")]
        [SerializeField] private Vector3 posA;
        [SerializeField] private Vector3 posB;
        [SerializeField] private float duration = 0.25f;
        [SerializeField] private Ease ease = Ease.OutQuad;

        [Header("Loop")]
        [SerializeField] private bool useLoop = false;
        [SerializeField] private int loopCount = -1; // -1 = infinite
        [SerializeField] private LoopType loopType = LoopType.Yoyo;

        [Header("Behavior")]
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool setToAOnEnable = true;
        [SerializeField] private bool useLocalPosition = true;

        private Tween _tween;

        private void Reset()
        {
            target = transform;
            posA = Vector3.zero;
            posB = Vector3.up * 10f;
            duration = 0.25f;
            ease = Ease.OutQuad;
            useLoop = false;
            loopCount = -1;
            loopType = LoopType.Yoyo;
            playOnEnable = true;
            setToAOnEnable = true;
            useLocalPosition = true;
        }

        private void OnEnable()
        {
            if (target == null) target = transform;

            if (setToAOnEnable)
                SetPosition(posA);

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
            SetPosition(posA);

            _tween = useLocalPosition
                ? target.DOLocalMove(posB, duration)
                : target.DOMove(posB, duration);

            _tween
                .SetEase(ease)
                .SetUpdate(false);

            if (useLoop)
                _tween.SetLoops(loopCount, loopType);
        }

        public void Stop(bool complete = false)
        {
            if (_tween == null) return;

            if (complete)
                _tween.Complete();

            KillTween();
        }

        public void SetPositions(Vector3 a, Vector3 b)
        {
            posA = a;
            posB = b;
        }

        private void SetPosition(Vector3 pos)
        {
            if (useLocalPosition)
                target.localPosition = pos;
            else
                target.position = pos;
        }

        private void KillTween()
        {
            if (_tween == null) return;
            _tween.Kill();
            _tween = null;
        }
    }

}
