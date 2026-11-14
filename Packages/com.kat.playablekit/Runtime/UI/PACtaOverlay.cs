using UnityEngine;
using UnityEngine.UI;

namespace PlayableKit.UI
{
    // Persistent install overlay: the game logo and the Play Now button both open the store.
    // Keep it active for the whole playable.
    public sealed class PACtaOverlay : MonoBehaviour
    {
        // Browser previews report a full-screen safe area even when their device frame has a notch.
        private const float BrowserLogoTopInset = 0.1f;

        [SerializeField] private Button m_LogoButton;
        [SerializeField] private Button m_PlayNowButton;

        private Vector2 m_LogoAnchorMin;
        private Vector2 m_LogoAnchorMax;
        private bool m_HasLogoAnchors;

        private void Awake()
        {
            EnsureLunaManager();
            ApplyLogoSafeArea();
        }

        private void OnEnable()
        {
            if (m_LogoButton != null) m_LogoButton.onClick.AddListener(Install);
            if (m_PlayNowButton != null) m_PlayNowButton.onClick.AddListener(Install);
            ApplyLogoSafeArea();
        }

        private void OnDisable()
        {
            if (m_LogoButton != null) m_LogoButton.onClick.RemoveListener(Install);
            if (m_PlayNowButton != null) m_PlayNowButton.onClick.RemoveListener(Install);
        }

        private void Install()
        {
            if (PALunaManager.Instance != null) PALunaManager.Instance.GoToStore();
        }

        private void Start()
        {
            ApplyLogoSafeArea();
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyLogoSafeArea();
        }

        private void ApplyLogoSafeArea()
        {
            //if (m_LogoButton == null || Screen.width <= 0 || Screen.height <= 0) return;

            //RectTransform logo = m_LogoButton.transform as RectTransform;
            //if (logo == null) return;

            //if (!m_HasLogoAnchors)
            //{
            //    m_LogoAnchorMin = logo.anchorMin;
            //    m_LogoAnchorMax = logo.anchorMax;
            //    m_HasLogoAnchors = true;
            //}

            //Rect area = Screen.safeArea;
            //if (UsesFullScreenSafeArea(area)) area.yMax -= area.height * BrowserLogoTopInset;

            //Vector2 anchorMin = m_LogoAnchorMin;
            //Vector2 anchorMax = m_LogoAnchorMax;
            //anchorMin.x += area.xMin / Screen.width;
            //anchorMax.x += area.xMin / Screen.width;
            //anchorMin.y = area.yMax / Screen.height;
            //anchorMax.y = area.yMax / Screen.height;
            //logo.anchorMin = anchorMin;
            //logo.anchorMax = anchorMax;
        }

        private static bool UsesFullScreenSafeArea(Rect area)
        {
            return Mathf.Approximately(area.xMin, 0f) &&
                   Mathf.Approximately(area.yMin, 0f) &&
                   Mathf.Approximately(area.width, Screen.width) &&
                   Mathf.Approximately(area.height, Screen.height);
        }

        private static void EnsureLunaManager()
        {
            if (PALunaManager.Instance != null) return;
            new GameObject(nameof(PALunaManager)).AddComponent<PALunaManager>();
        }
    }
}
