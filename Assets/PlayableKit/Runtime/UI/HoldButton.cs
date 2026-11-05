using UnityEngine;
using UnityEngine.EventSystems;

namespace PlayableKit
{
    /// <summary>Press-and-hold UI control (e.g. gear lever / brake). Needs a raycast-target Graphic on the same object.</summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Tooltip("Optional handle graphic that slides down while the button is held.")]
        [SerializeField] private RectTransform leverHandle;
        [SerializeField] private float handleDownOffset = -238f;
        [SerializeField, Min(1f)] private float handleMoveSpeed = 2000f;

        private Vector2 handleRestPos;
        private bool handleCached;

        public bool IsHeld { get; private set; }

        private void Awake()
        {
            if (leverHandle == null) return;
            handleRestPos = leverHandle.anchoredPosition;
            handleCached = true;
        }

        private void Update()
        {
            if (!handleCached) return;
            Vector2 target = IsHeld ? handleRestPos + new Vector2(0f, handleDownOffset) : handleRestPos;
            leverHandle.anchoredPosition = Vector2.MoveTowards(leverHandle.anchoredPosition, target, handleMoveSpeed * Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            IsHeld = false;
            if (handleCached) leverHandle.anchoredPosition = handleRestPos;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsHeld = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsHeld = false;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            IsHeld = false;
        }
    }
}
