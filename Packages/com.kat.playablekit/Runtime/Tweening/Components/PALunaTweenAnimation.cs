using PlayableKit.Tweening;
using UnityEngine;

namespace PlayableKit.Tweening
{
    [DisallowMultipleComponent]
    public sealed class PALunaTweenAnimation : MonoBehaviour
    {
        public enum AnimationType
        {
            LocalMove,
            LocalRotate,
            Scale,
            Move,
            Fade
        }

        [SerializeField] private Component m_Target;
        [SerializeField] private AnimationType m_AnimationType;
        [SerializeField] private Vector3 m_EndValue;
        [SerializeField] private float m_EndValueFloat;
        [SerializeField, Min(0.01f)] private float m_Duration = 0.5f;
        [SerializeField, Min(0f)] private float m_Delay;
        [SerializeField] private Ease m_Ease = Ease.OutQuad;
        [SerializeField] private int m_Loops = -1;
        [SerializeField] private LoopType m_LoopType = LoopType.Restart;
        [SerializeField] private bool m_IsRelative;
        [SerializeField] private bool m_IsFrom;
        [SerializeField] private bool m_IsIndependentUpdate;
        [SerializeField] private bool m_AutoPlay = true;

        public Tween tween { get; private set; }

        private void Awake()
        {
            CreateTween();
        }

        private void OnEnable()
        {
            if (m_AutoPlay && tween != null && !tween.IsPlaying())
            {
                tween.Play();
            }
        }

        private void OnDestroy()
        {
            if (tween != null)
            {
                tween.Kill();
            }
        }

        public void DORestart()
        {
            if (tween == null || !tween.IsActive())
            {
                CreateTween();
            }

            if (tween != null)
            {
                tween.Restart();
            }
        }

        private void CreateTween()
        {
            Component target = m_Target != null ? m_Target : transform;
            Transform targetTransform = target.transform;

            Tweener tweener;
            switch (m_AnimationType)
            {
                case AnimationType.LocalMove:
                    tweener = targetTransform.DOLocalMove(m_EndValue, m_Duration);
                    break;
                case AnimationType.LocalRotate:
                    tweener = targetTransform.DOLocalRotate(m_EndValue, m_Duration);
                    break;
                case AnimationType.Scale:
                    tweener = targetTransform.DOScale(m_EndValue, m_Duration);
                    break;
                case AnimationType.Move:
                    tweener = targetTransform.DOMove(m_EndValue, m_Duration);
                    break;
                case AnimationType.Fade:
                    UnityEngine.UI.Graphic graphic = target as UnityEngine.UI.Graphic;
                    if (graphic == null)
                    {
                        Debug.LogError("Fade requires a Unity UI Graphic target.", this);
                        return;
                    }

                    tweener = graphic.DOFade(m_EndValueFloat, m_Duration);
                    break;
                default:
                    Debug.LogError("Unsupported Luna tween animation type.", this);
                    return;
            }

            tweener.SetDelay(m_Delay);
            tweener.SetEase(m_Ease);
            tweener.SetLoops(m_Loops, m_LoopType);
            tweener.SetRelative(m_IsRelative);
            tweener.SetUpdate(m_IsIndependentUpdate);

            if (m_IsFrom)
            {
                tweener.From();
            }

            tween = tweener;
            tween.Pause();
        }
    }
}
