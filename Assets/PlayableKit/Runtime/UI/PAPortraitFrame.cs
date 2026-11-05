using UnityEngine;

namespace PlayableKit.UI
{
    // Keeps a portrait playable inside a centered portrait frame when the screen is landscape.
    // Put it on the gameplay camera. A second camera with a lower depth must clear the screen,
    // so the area outside the frame has a color. Screen Space - Camera canvases follow the frame;
    // Screen Space - Overlay canvases keep the full screen.
    [RequireComponent(typeof(Camera))]
    public sealed class PAPortraitFrame : MonoBehaviour
    {
        [Tooltip("Width divided by height of the frame in landscape.")]
        [SerializeField, Min(0.1f)] private float m_FrameAspect = 640f / 1136f;

        private Camera m_Camera;
        private int m_ScreenWidth;
        private int m_ScreenHeight;

        private void Awake()
        {
            m_Camera = GetComponent<Camera>();
            Apply();
        }

        private void Update()
        {
            if (Screen.width != m_ScreenWidth || Screen.height != m_ScreenHeight) Apply();
        }

        private void Apply()
        {
            m_ScreenWidth = Screen.width;
            m_ScreenHeight = Screen.height;
            if (m_ScreenWidth <= m_ScreenHeight || m_ScreenHeight <= 0)
            {
                m_Camera.rect = new Rect(0f, 0f, 1f, 1f);
                return;
            }

            float width = Mathf.Min(1f, m_FrameAspect * m_ScreenHeight / m_ScreenWidth);
            m_Camera.rect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
        }
    }
}
