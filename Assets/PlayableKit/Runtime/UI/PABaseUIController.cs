using UnityEngine;

namespace PlayableKit.UI
{
    /// <summary>
    /// Base class for every playable-ad UI controller in this kit. Owns nothing project-specific —
    /// only the things every such controller tends to duplicate by hand: reacting to a
    /// landscape/portrait flip and calling "go to store". Both are optional, swappable dependencies
    /// (a small interface + a <see cref="MonoBehaviour"/> field cast once in <c>Awake</c>, matching
    /// this codebase's existing <c>ILastTargetGate</c>-style convention), not hard requirements —
    /// a controller that needs neither simply leaves the fields unassigned.
    ///
    /// Subclasses never override Unity's own <c>Awake</c>/<c>OnEnable</c>/<c>OnDisable</c>/
    /// <c>Update</c> — they override <see cref="OnControllerAwake"/>/<see cref="OnControllerEnable"/>/
    /// <see cref="OnControllerDisable"/>/<see cref="OnControllerUpdate"/>/
    /// <see cref="OnOrientationChanged"/> instead, so there is no "remember to call base.OnEnable()"
    /// foot-gun.
    ///
    /// Deliberately does not auto-discover or drive any sibling components — a scene's UI
    /// controller should be one clearly-readable class that owns its own buttons/state directly
    /// (named methods are self-documenting in a Button's OnClick dropdown; a pile of same-typed
    /// plug-in components is not). A concern genuinely complex/self-contained enough to deserve its
    /// own class (e.g. a multi-step popup) should be a plain component the controller holds a named
    /// `[SerializeField]` reference to and calls directly — not something auto-discovered.
    /// </summary>
    public abstract class PABaseUIController : MonoBehaviour
    {
        [Header("Base (optional — leave unassigned if this controller doesn't need it)")]
        [Tooltip("A MonoBehaviour implementing IOrientationSource. Leave empty if this controller " +
                 "doesn't react to orientation changes.")]
        [SerializeField] private MonoBehaviour orientationSourceBehaviour;

        [Tooltip("A MonoBehaviour implementing IStoreGateway. Leave empty if this controller never " +
                 "sends the player to the store directly.")]
        [SerializeField] private MonoBehaviour storeGatewayBehaviour;

        private IOrientationSource _orientationSource;
        private IStoreGateway _storeGateway;

        /// <summary>Landscape until the first orientation event (if any) arrives.</summary>
        protected UIOrientation CurrentOrientation { get; private set; } = UIOrientation.Landscape;

        /// <summary>Null when no store gateway is assigned — null-check at call sites.</summary>
        public IStoreGateway Store => _storeGateway;

        /// <summary>A CTA presenter a subclass can construct in <see cref="OnControllerAwake"/> —
        /// lets multiple internal moments reuse one CTA panel safely, as long as only one is ever
        /// shown at a time. Null until a subclass sets it.</summary>
        public CtaPresenter Cta { get; protected set; }

        private void Awake()
        {
            _orientationSource = orientationSourceBehaviour as IOrientationSource;
            _storeGateway = storeGatewayBehaviour as IStoreGateway;
            OnControllerAwake();
        }

        private void OnEnable()
        {
            if (_orientationSource != null)
            {
                CurrentOrientation = _orientationSource.Current;
                _orientationSource.Subscribe(HandleOrientationChanged);
            }

            OnControllerEnable();
        }

        private void OnDisable()
        {
            if (_orientationSource != null)
            {
                _orientationSource.Unsubscribe(HandleOrientationChanged);
            }
            OnControllerDisable();
        }

        private void Update()
        {
            OnControllerUpdate();
        }

        private void HandleOrientationChanged(UIOrientation orientation)
        {
            CurrentOrientation = orientation;
            OnOrientationChanged(orientation);
        }

        /// <summary>Runs once, in Awake, after the optional dependencies above are resolved.</summary>
        protected virtual void OnControllerAwake() { }

        /// <summary>Runs every OnEnable, after subscribing to the orientation source (if any).</summary>
        protected virtual void OnControllerEnable() { }

        /// <summary>Runs every OnDisable, after unsubscribing from the orientation source (if any).</summary>
        protected virtual void OnControllerDisable() { }

        /// <summary>Runs every frame. Override this instead of declaring your own <c>Update()</c>.</summary>
        protected virtual void OnControllerUpdate() { }

        /// <summary>Override to react to a landscape/portrait flip (e.g. reset stuck held-input
        /// state). No-op by default — safe to leave unoverridden.</summary>
        protected virtual void OnOrientationChanged(UIOrientation orientation) { }

        // ---- Oriented<T> helpers ----

        /// <summary>Sets both the Hor and Ver GameObject active/inactive together — only one is
        /// ever actually visible at a time (the other's whole layout root is inactive), so this is
        /// simply "broadcast to both".</summary>
        protected static void SetActive(Oriented<GameObject> item, bool active)
        {
            // Unity == null, not ?. - unassigned serialized fields are "fake null" objects in
            // the Editor that ?. doesn't catch, so SetActive on them threw.
            if (item.Hor != null)
            {
                item.Hor.SetActive(active);
            }
            if (item.Ver != null)
            {
                item.Ver.SetActive(active);
            }
        }

        /// <summary>Picks whichever of Hor/Ver is currently active in the hierarchy — only one
        /// ever is, since the orientation flip deactivates the other layout root wholesale.</summary>
        protected static T GetActiveInHierarchy<T>(Oriented<T> item) where T : Component
        {
            if (item.Hor != null && item.Hor.gameObject.activeInHierarchy)
            {
                return item.Hor;
            }

            if (item.Ver != null && item.Ver.gameObject.activeInHierarchy)
            {
                return item.Ver;
            }

            return null;
        }
    }
}
