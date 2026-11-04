using UnityEngine;
using UnityEngine.EventSystems;

namespace PlayableKit
{
    /// <summary>
    /// Generic drag-to-rotate input. Sits on a full-screen transparent Image behind other UI so touches on
    /// buttons and sliders never reach it. Forwards drag input to an optional <see cref="IDragRotateTarget"/>
    /// (e.g. an orbit camera), or, when no target is set, rotates <c>fallbackPivot</c> around world Y.
    ///
    /// Uses the UI pointer events rather than raw <c>Input.GetTouch</c>, and works out the drag distance itself
    /// from the pointer <i>position</i> (last x / y remembered here) instead of trusting
    /// <c>PointerEventData.delta</c>: in WebGL / Luna a frame without a fresh input event can replay the
    /// previous delta, which reads as a stutter when applied to a rotation.
    /// </summary>
    public sealed class UiDragRotate : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const int NoPointer = int.MinValue;

        [SerializeField, Tooltip("Optional. Must implement IDragRotateTarget.")] private MonoBehaviour target;
        [SerializeField, Tooltip("Rotated around world Y when no target is set.")] private Transform fallbackPivot;
        [SerializeField] private float degreesPerPixel = 0.25f;

        private IDragRotateTarget _target;
        private int _pointerId = NoPointer;
        private Vector2 _lastPosition;

        private void Awake()
        {
            _target = target as IDragRotateTarget;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || _pointerId != NoPointer)
            {
                return;
            }

            _pointerId = eventData.pointerId;
            _lastPosition = eventData.position;
            if (_target != null)
            {
                _target.Hold(true);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerId != _pointerId)
            {
                return;
            }

            var position = eventData.position;
            var delta = position - _lastPosition;
            _lastPosition = position;

            if (_target != null)
            {
                _target.Drag(delta.x, delta.y);
            }
            else if (fallbackPivot != null)
            {
                fallbackPivot.Rotate(0f, -delta.x * degreesPerPixel, 0f, Space.World);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerId != _pointerId)
            {
                return;
            }

            _pointerId = NoPointer;
            if (_target != null)
            {
                _target.Hold(false);
            }
        }

        private void OnDisable()
        {
            // Panel hidden mid-drag: let the target coast / auto-orbit again.
            if (_pointerId != NoPointer)
            {
                _pointerId = NoPointer;
                if (_target != null)
                {
                    _target.Hold(false);
                }
            }
        }
    }
}
