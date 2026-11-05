using UnityEngine;
using UnityEngine.EventSystems;

// Horizontal drag-scroll with inertia, plain code only (no ScrollRect / layout groups, which use
// DrivenRectTransformTracker and log a warning in Luna).
// Put it on the object that receives raycasts (the viewport, with a raycast-capable Graphic such as
// an Image with Raycast Target on), exactly like a ScrollRect.
public class PADragScroll : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform m_Viewport;

    // Moved horizontally. Anchors are forced to (0,0)-(0,1) and pivot to (0,0.5)
    public RectTransform m_Content;

    // Child of m_Content whose right edge defines the content width (e.g. "shelfs")
    public RectTransform m_SizeSource;

    public float m_PaddingRight = 0f;

    // Fraction of velocity left after one second (same meaning as ScrollRect.decelerationRate)
    public float m_Deceleration = 0.135f;

    public bool m_Inertia = true;

    bool m_Dragging;
    Vector2 m_PointerStart;
    float m_ContentStartX;
    float m_Velocity;
    float m_PrevX;
    float m_LastViewW = -1f;
    float m_LastContentW = -1f;

    public float horizontalNormalizedPosition
    {
        get
        {
            if (m_Content == null) return 0f;
            float range = ScrollRange();
            if (range <= 0.0001f) return 0f;
            return Mathf.Clamp01(-m_Content.anchoredPosition.x / range);
        }
        set
        {
            if (m_Content == null) return;
            UpdateBounds();
            SetContentX(-Mathf.Clamp01(value) * ScrollRange());
            m_Velocity = 0f;
        }
    }

    public void StopMovement()
    {
        m_Velocity = 0f;
    }

    void OnEnable()
    {
        m_Dragging = false;
        m_Velocity = 0f;
        m_LastViewW = -1f;
        m_LastContentW = -1f;
        UpdateBounds();
    }

    void OnDisable()
    {
        m_Dragging = false;
        m_Velocity = 0f;
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        m_Velocity = 0f;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (m_Content == null || m_Viewport == null) return;

        UpdateBounds();

        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Viewport, eventData.position, eventData.pressEventCamera, out local)) return;

        m_PointerStart = local;
        m_ContentStartX = m_Content.anchoredPosition.x;
        m_PrevX = m_ContentStartX;
        m_Velocity = 0f;
        m_Dragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!m_Dragging) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (m_Content == null || m_Viewport == null) return;

        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Viewport, eventData.position, eventData.pressEventCamera, out local)) return;

        SetContentX(m_ContentStartX + (local.x - m_PointerStart.x));
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        m_Dragging = false;
    }

    void LateUpdate()
    {
        if (m_Content == null || m_Viewport == null) return;

        UpdateBounds();

        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;

        float x = m_Content.anchoredPosition.x;

        if (m_Dragging)
        {
            // Smoothed velocity estimate from actual movement
            float v = (x - m_PrevX) / dt;
            m_Velocity = Mathf.Lerp(m_Velocity, v, Mathf.Clamp01(dt * 10f));
        }
        else if (m_Inertia && Mathf.Abs(m_Velocity) > 1f)
        {
            m_Velocity *= Mathf.Pow(Mathf.Clamp01(m_Deceleration), dt);
            if (Mathf.Abs(m_Velocity) < 1f) m_Velocity = 0f;

            float target = x + m_Velocity * dt;
            SetContentX(target);

            // Hit an edge: drop the remaining velocity
            if (Mathf.Abs(m_Content.anchoredPosition.x - target) > 0.001f) m_Velocity = 0f;
        }
        else
        {
            m_Velocity = 0f;
        }

        m_PrevX = m_Content.anchoredPosition.x;
    }

    // Writes content width from m_SizeSource and re-clamps when the content or viewport size changed
    void UpdateBounds()
    {
        if (m_Content == null || m_Viewport == null) return;

        m_Content.anchorMin = new Vector2(0f, 0f);
        m_Content.anchorMax = new Vector2(0f, 1f);
        m_Content.pivot = new Vector2(0f, 0.5f);

        float viewW = m_Viewport.rect.width;
        float contentW = m_Content.sizeDelta.x;
        if (m_SizeSource != null)
        {
            contentW = m_SizeSource.anchoredPosition.x + m_SizeSource.rect.width + m_PaddingRight;
        }

        if (!Mathf.Approximately(contentW, m_Content.sizeDelta.x))
        {
            Vector2 sd = m_Content.sizeDelta;
            sd.x = contentW;
            m_Content.sizeDelta = sd;
        }

        if (!Mathf.Approximately(viewW, m_LastViewW) || !Mathf.Approximately(contentW, m_LastContentW))
        {
            m_LastViewW = viewW;
            m_LastContentW = contentW;
            SetContentX(m_Content.anchoredPosition.x);
        }
    }

    // Distance the content can travel (>= 0)
    float ScrollRange()
    {
        if (m_Content == null || m_Viewport == null) return 0f;
        return Mathf.Max(0f, m_Content.rect.width - m_Viewport.rect.width);
    }

    void SetContentX(float x)
    {
        if (m_Content == null) return;
        x = Mathf.Clamp(x, -ScrollRange(), 0f);
        Vector2 pos = m_Content.anchoredPosition;
        if (Mathf.Approximately(pos.x, x)) return;
        pos.x = x;
        m_Content.anchoredPosition = pos;
    }
}
