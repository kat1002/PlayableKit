using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using PlayableKit.Tweening;
using UnityEngine.UI;

namespace PlayableKit
{
    public class PATweenRotateButton : PATweenButton
    {
        Vector3 firstRotate = new Vector3();

        public Vector3 rotateAngle = new Vector3(0, 0, -90);

        private void Awake()
        {
            firstRotate = transform.localRotation.eulerAngles;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            var button = gameObject.GetComponent<Button>();
            if (button == null || button.enabled == false || button.onClick == null) return;

            if (tweenObject != null)
            {
                tweenObject.transform.DOLocalRotate(rotateAngle, duration).SetUpdate(ignoreTimeScale);
            }
            else
            {
                gameObject.transform.DOLocalRotate(rotateAngle, duration).SetUpdate(ignoreTimeScale);
            }
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            var button = gameObject.GetComponent<Button>();
            if (button == null || button.onClick == null) return;

            if (tweenObject != null)
            {
                tweenObject.transform.DOLocalRotate(firstRotate, duration).SetUpdate(ignoreTimeScale);
            }
            else
                gameObject.transform.DOLocalRotate(firstRotate, duration).SetUpdate(ignoreTimeScale);
        }
    }
}
