using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PlayableUIKit
{
    /// <summary>
    /// Shows/hides a Hor+Ver CTA panel pair (plus an optional shared fullscreen CTA), and
    /// optionally supports retargeting a reused CTA button's copy + click action at runtime — the
    /// "one panel serves multiple CTA moments" pattern (e.g. a "get out of the car" prompt, then
    /// later the final "PLAY NOW" CTA, reusing the same button).
    ///
    /// Plain C# class, not a MonoBehaviour — compose one per controller that owns a CTA moment,
    /// construct it once (e.g. in Awake) and call Show()/Hide()/SetCopyAndTarget() from your own
    /// logic. No reflection, no container.
    /// </summary>
    public sealed class CtaPresenter
    {
        private readonly Oriented<GameObject> _ctaPanel;
        private readonly GameObject _fullscreenCta;
        private readonly Oriented<TMP_Text> _ctaLabel;
        private readonly Oriented<Button> _ctaButton;
        private readonly Button _fullscreenCtaButton;

        public CtaPresenter(
            Oriented<GameObject> ctaPanel,
            GameObject fullscreenCta = null,
            Oriented<TMP_Text> ctaLabel = default,
            Oriented<Button> ctaButton = default,
            Button fullscreenCtaButton = null)
        {
            _ctaPanel = ctaPanel;
            _fullscreenCta = fullscreenCta;
            _ctaLabel = ctaLabel;
            _ctaButton = ctaButton;
            _fullscreenCtaButton = fullscreenCtaButton;
        }

        public void Show()
        {
            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            // Unity == null, not ?. - fake-null (unassigned) objects slip past ?. and throw.
            if (_ctaPanel.Hor != null)
            {
                _ctaPanel.Hor.SetActive(visible);
            }
            if (_ctaPanel.Ver != null)
            {
                _ctaPanel.Ver.SetActive(visible);
            }
            if (_fullscreenCta != null)
            {
                _fullscreenCta.SetActive(visible);
            }
        }

        /// <summary>Retargets copy + click action for a reused CTA button/panel, for a controller
        /// that shows more than one distinct CTA moment through the same UI. Does not itself
        /// show/hide the panel — call <see cref="Show"/> after, if needed.</summary>
        public void SetCopyAndTarget(string text, UnityAction target)
        {
            _ctaLabel.SetText(text);
            Retarget(_ctaButton.Hor, target);
            Retarget(_ctaButton.Ver, target);
            Retarget(_fullscreenCtaButton, target);
        }

        /// <summary>Retargets only the click action, keeping the CTA's scene-authored copy.
        /// Does not itself show/hide the panel.</summary>
        public void SetTarget(UnityAction target)
        {
            Retarget(_ctaButton.Hor, target);
            Retarget(_ctaButton.Ver, target);
            Retarget(_fullscreenCtaButton, target);
        }

        private static void Retarget(Button button, UnityAction target)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(target);
        }
    }
}
