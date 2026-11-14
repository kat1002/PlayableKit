using UnityEngine;
using UnityEngine.EventSystems;

namespace PlayableKit.Flow
{
    /// <summary>Tap anywhere on a full-screen catcher (end card dim / a fullscreen CTA overlay) fires the CTA.</summary>
    public class PACtaClickForwarder : MonoBehaviour, IPointerClickHandler
    {
        public PAGameUIController ui;
        public void OnPointerClick(PointerEventData eventData)
        {
            if (ui != null) ui.Cta();
        }
    }
}
