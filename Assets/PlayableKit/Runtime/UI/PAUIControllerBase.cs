using PlayableKit;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlayableKit.UI
{
    /// <summary>
    /// Drives the screen-space overlay: tap zones, the tutorial hand, the survival progress
    /// bar, and the CTA end card. Generic orchestrator — usable as-is (wire portraitCore /
    /// landscapeCore directly in the Inspector), or subclass to add game-specific HUD.
    /// </summary>
    public class PAUIControllerBase : MonoBehaviour
    {
        [System.Serializable]
        public class UIRefsCore
        {
            public GameObject root;
            public RectTransform hand;
            public TextMeshProUGUI hintText;
            public GameObject startObj;
            public Image progressFill;
            public GameObject endCard;
            public PADoScaleAToB introPulseA;
            public PADoScaleAToB introPulseB;
        }

        [Header("Screen layouts")]
        [SerializeField] protected UIRefsCore portraitCore = new UIRefsCore();
        [SerializeField] protected UIRefsCore landscapeCore = new UIRefsCore();

        [SerializeField] protected PALoadingScreenController loadingScreen;
        [Tooltip("How often OnHudTick() is called while active, in seconds.")]
        public float hudTickInterval = 0.1f;

        protected UIRefsCore active;
        protected bool activeIsLandscape;

        protected bool _firstTapDone;
        protected bool _introVisible;
        protected float ProgressT { get; private set; }
        private bool _orientationChecked;
        private float _lastHudTime;
        private bool _endCardTracked;

        protected virtual void Awake()
        {
            BuildCoreRefs();
            ApplyOrientation(Orientation.IsLandscape);
        }

        /// <summary>Optional: overwrite portraitCore/landscapeCore from a subclass's own richer ref class.</summary>
        protected virtual void BuildCoreRefs() { }

        protected virtual void ApplyOrientation(bool isLandscape)
        {
            active = isLandscape ? landscapeCore : portraitCore;
            activeIsLandscape = isLandscape;

            if (portraitCore.root != null) portraitCore.root.SetActive(!isLandscape);
            if (landscapeCore.root != null) landscapeCore.root.SetActive(isLandscape);

            var scaler = GetComponent<CanvasScaler>();
            if (scaler != null)
                scaler.referenceResolution = isLandscape ? new Vector2(1920f, 1080f) : new Vector2(1080f, 1920f);
        }

        // ---- Public API used by PAGameManagerBase / input --------------------------------
        public virtual void ShowIntro()
        {
            _introVisible = true;
            if (active.hand != null) active.hand.gameObject.SetActive(true);
            if (active.hintText != null) active.hintText.gameObject.SetActive(true);
            if (active.startObj != null) active.startObj.SetActive(true);
            if (active.introPulseA != null) active.introPulseA.Play();
            if (active.introPulseB != null) active.introPulseB.Play();
        }

        public virtual void HideIntro()
        {
            _introVisible = false;
            if (active.hand != null) active.hand.gameObject.SetActive(false);
            if (active.hintText != null) active.hintText.gameObject.SetActive(false);
            if (active.startObj != null) active.startObj.SetActive(false);
            if (active.introPulseA != null) active.introPulseA.Stop(true);
            if (active.introPulseB != null) active.introPulseB.Stop(true);
        }

        public virtual void OnFirstTap()
        {
            if (_firstTapDone) return;
            _firstTapDone = true;
            PALunaManager.TrackEvent("first_interaction");
            HideIntro();
        }

        public virtual void SetProgress(float t)
        {
            ProgressT = Mathf.Clamp01(t);
            if (active.progressFill == null) return;
            active.progressFill.rectTransform.anchorMax = new Vector2(ProgressT, 1f);
        }

        public virtual void ShowEndCard()
        {
            if (active.endCard != null) active.endCard.SetActive(true);
            if (!_endCardTracked)
            {
                _endCardTracked = true;
                PALunaManager.TrackEvent("endcard_shown");
            }
        }

        public virtual void Cta()
        {
            if (PAAudioManagerBase.Instance != null) PAAudioManagerBase.Instance.PlayTap();
            if (PALunaManager.instance != null) PALunaManager.instance.GoToStore();
        }

        protected virtual void Update()
        {
            if (!_orientationChecked)
            {
                _orientationChecked = true;
                bool isLandscape = Orientation.IsLandscape;
                if (isLandscape != activeIsLandscape)
                {
                    ApplyOrientation(isLandscape);
                    if (_introVisible) ShowIntro();
                }
                if (loadingScreen != null) loadingScreen.Hide();
            }

            if (_introVisible && active.hand != null)
            {
                float s = 1f + Mathf.Sin(Time.time * 6f) * 0.12f;
                active.hand.localScale = new Vector3(s, s, 1f);
            }

            if (Time.unscaledTime - _lastHudTime >= hudTickInterval)
            {
                _lastHudTime = Time.unscaledTime;
                OnHudTick();
            }
        }

        /// <summary>Throttled (hudTickInterval) hook for game-specific HUD text/formatting.</summary>
        protected virtual void OnHudTick() { }
    }

    /// <summary>Tap anywhere on a full-screen catcher (end card dim / a fullscreen CTA overlay) fires the CTA.</summary>
    public class PACtaClickForwarder : MonoBehaviour, IPointerClickHandler
    {
        public PAUIControllerBase ui;
        public void OnPointerClick(PointerEventData eventData)
        {
            if (ui != null) ui.Cta();
        }
    }
}
