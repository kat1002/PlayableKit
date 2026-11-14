using UnityEngine;
using static PlayableKit.GameEventManager;

namespace PlayableKit.UI
{
    /// <summary>
    /// Fits this RectTransform to Screen.safeArea using normalised anchors.
    /// Re-applied on orientation events and when the safe area or screen size changes.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class PASafeArea : MonoBehaviour
    {
        private RectTransform rect;
        private Rect lastSafeArea;
        private int lastWidth;
        private int lastHeight;

        private void Awake()
        {
            rect = (RectTransform)transform;
            RegisterEvent(EventId.LandscapeScreen, Apply);
            RegisterEvent(EventId.VerticalScreen, Apply);
        }

        private void OnDestroy()
        {
            UnRegisterEvent(EventId.LandscapeScreen, Apply);
            UnRegisterEvent(EventId.VerticalScreen, Apply);
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != lastSafeArea || Screen.width != lastWidth || Screen.height != lastHeight)
                Apply();
        }

        private void Apply()
        {
            if (rect == null) return;
            if (Screen.width <= 0 || Screen.height <= 0) return;

            Rect area = Screen.safeArea;
            Vector2 min = area.position;
            Vector2 max = area.position + area.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            lastSafeArea = area;
            lastWidth = Screen.width;
            lastHeight = Screen.height;
        }
    }
}
