using UnityEngine;
using UnityEngine.UI;

namespace PlayableKit.Analytics
{
    /// <summary>Add to a Button to send a "btn_&lt;eventName&gt;" analytics event on click.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class PAAnalyticsButton : MonoBehaviour
    {
        [SerializeField] private string eventName;

        private Button _button;

        private void OnEnable()
        {
            _button = GetComponent<Button>();
            if (_button != null)
            {
                _button.onClick.AddListener(OnClicked);
            }
        }

        private void OnDisable()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClicked);
            }
        }

        private void OnClicked()
        {
            PlayableAnalytics.Button(string.IsNullOrEmpty(eventName) ? gameObject.name : eventName);
        }
    }
}
