using UnityEngine;

namespace PlayableKit.Video
{
    // Sizes/positions the start-screen chroma video per screen orientation.
    // Put on the "VideoPlay" RectTransform. Portrait defaults match the authored
    // values, so portrait is unchanged; tune the Landscape pair to taste.
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class PAVideoOrientationSizer : MonoBehaviour
    {
        [SerializeField] RectTransform target;   // defaults to this RectTransform

        [Header("Portrait (screen taller than wide)")]
        [SerializeField] Vector2 portraitSizeDelta   = new Vector2(495.04f, 880.04f);
        [SerializeField] Vector2 portraitAnchoredPos = new Vector2(-66f, 530.74f);

        [Header("Landscape (screen wider than tall)")]
        [SerializeField] Vector2 landscapeSizeDelta   = new Vector2(281f, 500f);
        [SerializeField] Vector2 landscapeAnchoredPos = new Vector2(0f, 120f);

        void OnEnable()
        {
            Apply();
        }

        void OnRectTransformDimensionsChange()
        {
            Apply();
        }

    #if UNITY_EDITOR
        void OnValidate()
        {
            if (isActiveAndEnabled) Apply();
        }
    #endif

        void Apply()
        {
            if (target == null) target = (RectTransform)transform;

            bool landscape = Screen.width > Screen.height;
            Vector2 size = landscape ? landscapeSizeDelta : portraitSizeDelta;
            Vector2 pos = landscape ? landscapeAnchoredPos : portraitAnchoredPos;

            if (target.sizeDelta != size) target.sizeDelta = size;
            if (target.anchoredPosition != pos) target.anchoredPosition = pos;
        }
    }
}
