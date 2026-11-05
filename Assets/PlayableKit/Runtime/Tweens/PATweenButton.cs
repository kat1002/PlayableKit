using System;
using PlayableKit.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlayableKit
{
    public enum TweenButtonType
    {
        Default,
        BigXSmallY,
        Rotate
    }

    public class PATweenButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public float duration = 0.1f;
        public GameObject tweenObject;
        public bool ignoreTimeScale = true;
        Vector2 firstScale = new Vector2(1,1);

        public TweenButtonType tweenType;

        private void Awake()
        {
            firstScale = transform.localScale;
        }

        public void SetScale(Vector2 scale)
        {
            firstScale = scale;
        }    

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            var button = gameObject.GetComponent<Button>();
            if (button == null || button.enabled == false || button.onClick == null) return;

            if (tweenObject != null)
            {
                switch (tweenType)
                {
                    case TweenButtonType.BigXSmallY:
                        tweenObject.transform.DOScale(new Vector2(1.1f * firstScale.x, 0.9f * firstScale.y), duration).SetUpdate(ignoreTimeScale);
                        break;
                    case TweenButtonType.Default:
                    default:
                        tweenObject.transform.DOScale(0.9f * firstScale, duration).SetUpdate(ignoreTimeScale);
                        break;
                }
            }
            else
            {
                switch (tweenType)
                {
                    case TweenButtonType.BigXSmallY:
                        gameObject.transform.DOScale(new Vector2(1.1f * firstScale.x, 0.9f * firstScale.y), duration).SetUpdate(ignoreTimeScale);
                        break;
                    case TweenButtonType.Default:
                    default:
                        gameObject.transform.DOScale(0.9f * firstScale, duration).SetUpdate(ignoreTimeScale);
                        break;
                }                
            }
        }

        public virtual void OnPointerUp(PointerEventData eventData)
        {
            var button = gameObject.GetComponent<Button>();
            if (button == null || button.onClick == null) return;

            if (tweenObject != null)
            {
                tweenObject.transform.DOScale(1f * firstScale, duration).SetUpdate(ignoreTimeScale);
            }
            else
                gameObject.transform.DOScale(1f * firstScale, duration).SetUpdate(ignoreTimeScale);
        }
    }
}
