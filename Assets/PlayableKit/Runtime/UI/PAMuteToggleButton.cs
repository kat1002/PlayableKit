using TMPro;
using UnityEngine;

namespace PlayableKit.UI
{
    /// <summary>
    /// Drives a header mute button. Uses a text label rather than icon sprites so it needs no
    /// extra art assets and always renders with Luna's builtin-font requirement. Wire a Button's
    /// OnClick to OnClick() in the Inspector.
    /// </summary>
    public class PAMuteToggleButton : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private string unmutedText = "SOUND: ON";
        [SerializeField] private string mutedText = "SOUND: OFF";

        private void OnEnable()
        {
            PAAudioManagerBase.OnMuteChanged += Refresh;
            Refresh(PAAudioManagerBase.IsMuted);
        }

        private void OnDisable()
        {
            PAAudioManagerBase.OnMuteChanged -= Refresh;
        }

        private void Refresh(bool muted)
        {
            if (label != null) label.text = muted ? mutedText : unmutedText;
        }

        public void OnClick()
        {
            PAAudioManagerBase.Instance?.ToggleMute();
        }
    }
}
