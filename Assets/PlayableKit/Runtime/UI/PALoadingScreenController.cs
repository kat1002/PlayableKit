using UnityEngine;

namespace PlayableKit.UI
{
    /// <summary>
    /// A full-bleed cover shown by default (active in the scene, never activated in code, so
    /// there is no frame where it could be missing). Its only honest job here is to mask the
    /// one real layout hazard this project has: the WebGL canvas isn't sized to its final
    /// ad-slot dimensions until one frame after Awake(), which can pop the UI's orientation
    /// layout. PAUIControllerBase calls Hide() the instant that hazard has resolved — never on a
    /// fixed timer or a fake progress value, since there's no real async loading to represent.
    /// </summary>
    public class PALoadingScreenController : MonoBehaviour
    {
        [SerializeField] private GameObject coverRoot;

        private void Reset()
        {
            coverRoot = gameObject;
        }

        public void Hide()
        {
            var target = coverRoot != null ? coverRoot : gameObject;
            if (target.activeSelf) target.SetActive(false);
        }
    }
}
