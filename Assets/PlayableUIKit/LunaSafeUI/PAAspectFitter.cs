using UnityEngine;

// Replacement for UnityEngine.UI.AspectRatioFitter that does not use DrivenRectTransformTracker
// (its Clear call logs a warning in Luna).
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class PAAspectFitter : MonoBehaviour
{
    // Values match UnityEngine.UI.AspectRatioFitter.AspectMode
    public enum Mode
    {
        None = 0,
        WidthControlsHeight = 1,
        HeightControlsWidth = 2,
        FitInParent = 3,
        EnvelopeParent = 4,
    }

    public Mode m_Mode = Mode.FitInParent;

    public float m_Ratio = 1f;

    RectTransform m_Rect;
    Vector2 m_LastParentSize = new Vector2(-1f, -1f);
    Mode m_LastMode = (Mode)(-1);
    float m_LastRatio = -1f;

    public static Mode FromUnity(int unityAspectMode)
    {
        switch (unityAspectMode)
        {
            case 1: return Mode.WidthControlsHeight;
            case 2: return Mode.HeightControlsWidth;
            case 3: return Mode.FitInParent;
            case 4: return Mode.EnvelopeParent;
            default: return Mode.None;
        }
    }

    void OnEnable()
    {
        Apply(true);
    }

    void LateUpdate()
    {
        Apply(false);
    }

    void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled) Apply(false);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        m_LastMode = (Mode)(-1);
    }
#endif

    public void Apply(bool force)
    {
        if (m_Rect == null) m_Rect = (RectTransform)transform;

        RectTransform parent = m_Rect.parent as RectTransform;
        if (parent == null || m_Mode == Mode.None) return;

        Vector2 p = parent.rect.size;
        if (!force && p == m_LastParentSize && m_Mode == m_LastMode && Mathf.Approximately(m_Ratio, m_LastRatio)) return;

        m_LastParentSize = p;
        m_LastMode = m_Mode;
        m_LastRatio = m_Ratio;

        float ratio = m_Ratio > 0.0001f ? m_Ratio : 1f;

        switch (m_Mode)
        {
            case Mode.FitInParent:
            case Mode.EnvelopeParent:
            {
                // Fit: limited by width when the parent is narrower than the ratio; Envelope is the opposite
                bool useWidth = p.x / Mathf.Max(p.y, 0.0001f) < ratio;
                if (m_Mode == Mode.EnvelopeParent) useWidth = !useWidth;

                float w, h;
                if (useWidth)
                {
                    w = p.x;
                    h = p.x / ratio;
                }
                else
                {
                    h = p.y;
                    w = p.y * ratio;
                }

                m_Rect.anchorMin = Vector2.zero;
                m_Rect.anchorMax = Vector2.one;
                m_Rect.sizeDelta = new Vector2(w - p.x, h - p.y);
                // Keep the box centred in the parent for any pivot
                m_Rect.anchoredPosition = new Vector2((m_Rect.pivot.x - 0.5f) * w, (m_Rect.pivot.y - 0.5f) * h);
                break;
            }
            case Mode.WidthControlsHeight:
            {
                float h = m_Rect.rect.width / ratio;
                float anchorH = p.y * (m_Rect.anchorMax.y - m_Rect.anchorMin.y);
                m_Rect.sizeDelta = new Vector2(m_Rect.sizeDelta.x, h - anchorH);
                break;
            }
            case Mode.HeightControlsWidth:
            {
                float w = m_Rect.rect.height * ratio;
                float anchorW = p.x * (m_Rect.anchorMax.x - m_Rect.anchorMin.x);
                m_Rect.sizeDelta = new Vector2(w - anchorW, m_Rect.sizeDelta.y);
                break;
            }
        }
    }
}
