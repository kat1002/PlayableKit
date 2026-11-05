using System;
using UnityEngine;
using static PlayableKit.GameEventManager;

namespace PlayableKit.UI
{
    [Serializable]
    public class PARectPreset
    {
        public Vector2 anchorMin = new Vector2(0.5f, 0.5f);
        public Vector2 anchorMax = new Vector2(0.5f, 0.5f);
        public Vector2 pivot = new Vector2(0.5f, 0.5f);
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector3 localScale = Vector3.one;
        public bool active = true;

        public void Capture(RectTransform rect)
        {
            anchorMin = rect.anchorMin;
            anchorMax = rect.anchorMax;
            pivot = rect.pivot;
            anchoredPosition = rect.anchoredPosition;
            sizeDelta = rect.sizeDelta;
            localScale = rect.localScale;
            active = rect.gameObject.activeSelf;
        }

        public void Apply(RectTransform rect, bool applyActive)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = anchoredPosition;
            rect.localScale = localScale;
            if (applyActive && rect.gameObject.activeSelf != active)
                rect.gameObject.SetActive(active);
        }
    }

    /// <summary>
    /// Applies a full RectTransform preset per orientation. Follows the Landscape/Vertical events
    /// raised by PAScreenOrientationChecker / GameEventManager. Use the context menus to capture the current rect or preview a preset.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class PALayout : MonoBehaviour
    {
        public PARectPreset m_Landscape = new PARectPreset();
        public PARectPreset m_Vertical = new PARectPreset();

        // Opt-in: also SetActive(preset.active). An inactive object stops receiving OnEnable but keeps its event registration.
        public bool m_ApplyActive;

        private RectTransform rect;

        private RectTransform Rect
        {
            get
            {
                if (rect == null) rect = (RectTransform)transform;
                return rect;
            }
        }

        private void Reset()
        {
            m_Landscape.Capture((RectTransform)transform);
            m_Vertical.Capture((RectTransform)transform);
        }

        private void Awake()
        {
            RegisterEvent(EventId.LandscapeScreen, OnLandscape);
            RegisterEvent(EventId.VerticalScreen, OnVertical);
        }

        private void OnDestroy()
        {
            UnRegisterEvent(EventId.LandscapeScreen, OnLandscape);
            UnRegisterEvent(EventId.VerticalScreen, OnVertical);
        }

        private void OnEnable()
        {
            // Events only fire on change, so apply the current orientation when enabled.
            Apply(PAScreenOrientationChecker.GetCurrentScreen());
        }

        private void OnLandscape() { Apply(TScreen.LandscapeScreen); }
        private void OnVertical() { Apply(TScreen.VerticalScreen); }

        public void Apply(TScreen screen)
        {
            PARectPreset p = screen == TScreen.LandscapeScreen ? m_Landscape : m_Vertical;
            p.Apply(Rect, m_ApplyActive);
        }

        [ContextMenu("Capture current as Landscape")]
        private void CaptureLandscape() { m_Landscape.Capture(Rect); }

        [ContextMenu("Capture current as Vertical")]
        private void CaptureVertical() { m_Vertical.Capture(Rect); }

        [ContextMenu("Preview Landscape")]
        private void PreviewLandscape() { m_Landscape.Apply(Rect, m_ApplyActive); }

        [ContextMenu("Preview Vertical")]
        private void PreviewVertical() { m_Vertical.Apply(Rect, m_ApplyActive); }
    }
}
