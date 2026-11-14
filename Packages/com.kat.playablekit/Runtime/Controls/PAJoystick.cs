using UnityEngine;
using UnityEngine.EventSystems;

namespace PlayableKit.Controls
{
    /// <summary>
    /// Floating on-screen joystick. Put it on a transparent Image (Raycast Target on) that covers
    /// the control area. Tracks a single pointer so a second finger (e.g. a jump button) never
    /// interferes. Uses RectTransformUtility, so it works with any CanvasScaler, orientation and
    /// Overlay/Camera canvas.
    /// </summary>
    public sealed class PAJoystick : MonoBehaviour, IMoveInput, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Visuals (children of this object)")]
        [Tooltip("Joystick base ring.")]
        [SerializeField] private RectTransform background;
        [Tooltip("Joystick knob (child of the background).")]
        [SerializeField] private RectTransform handle;

        [Header("Settings")]
        [Tooltip("How far the knob may travel, in multiples of the background radius.")]
        [SerializeField] private float handleRange = 1f;
        [Tooltip("Pushes below this magnitude (0-1) count as no input.")]
        [SerializeField] private float deadZone = 0.1f;
        [Tooltip("On: the joystick appears where the touch lands. Off: it stays at its start position.")]
        [SerializeField] private bool floating = true;
        [Tooltip("On: hide the joystick while not touched (only used when floating is on).")]
        [SerializeField] private bool hideWhenIdle = false;

        private RectTransform _zone;
        private Vector2 _startAnchoredPos;
        private int _pointerId = int.MinValue;
        private Vector2 _direction;

        /// <summary>Push direction, magnitude 0..1.</summary>
        public Vector2 Value { get { return _direction; } }

        public bool IsPressed { get { return _pointerId != int.MinValue; } }

        private void Awake()
        {
            _zone = (RectTransform)transform;

            if (background != null)
            {
                // Anchor to the centre so anchoredPosition is correct regardless of zone size.
                background.anchorMin = background.anchorMax = new Vector2(0.5f, 0.5f);
                background.pivot = new Vector2(0.5f, 0.5f);
                _startAnchoredPos = background.anchoredPosition;
            }
            if (handle != null)
            {
                handle.anchorMin = handle.anchorMax = new Vector2(0.5f, 0.5f);
                handle.pivot = new Vector2(0.5f, 0.5f);
            }

            ResetStick();
        }

        private void OnEnable()
        {
            ResetStick();
        }

        private void OnDisable()
        {
            // The panel can be disabled mid-touch (orientation change) without a PointerUp.
            ResetStick();
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (_pointerId != int.MinValue) return; // another finger already controls the stick
            _pointerId = e.pointerId;

            if (floating && background != null)
            {
                Vector2 local;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _zone, e.position, e.pressEventCamera, out local))
                {
                    // local is relative to the zone pivot, anchoredPosition to the zone centre.
                    background.anchoredPosition = ClampInsideZone(local - _zone.rect.center);
                }
                if (hideWhenIdle) background.gameObject.SetActive(true);
            }

            UpdateStick(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != _pointerId) return;
            UpdateStick(e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != _pointerId) return;
            ResetStick();
        }

        private void UpdateStick(PointerEventData e)
        {
            if (background == null) return;

            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    background, e.position, e.pressEventCamera, out local))
                return;

            float radius = Mathf.Max(1f, background.rect.width * 0.5f);
            Vector2 raw = local / radius; // background centre = (0,0)
            Vector2 clamped = Vector2.ClampMagnitude(raw, 1f);

            // Dead zone, rescaled so the output ramps smoothly from 0.
            float mag = clamped.magnitude;
            if (mag < deadZone)
            {
                _direction = Vector2.zero;
            }
            else
            {
                float scaled = (mag - deadZone) / (1f - deadZone);
                _direction = clamped.normalized * Mathf.Clamp01(scaled);
            }

            if (handle != null)
                handle.anchoredPosition = clamped * radius * handleRange;
        }

        private void ResetStick()
        {
            _pointerId = int.MinValue;
            _direction = Vector2.zero;

            if (handle != null) handle.anchoredPosition = Vector2.zero;

            if (background != null)
            {
                if (floating) background.anchoredPosition = _startAnchoredPos;
                if (floating && hideWhenIdle) background.gameObject.SetActive(false);
            }
        }

        /// <summary>Keeps the joystick inside the zone when the touch lands near an edge.</summary>
        private Vector2 ClampInsideZone(Vector2 pos)
        {
            if (background == null) return pos;
            Vector2 half = _zone.rect.size * 0.5f - background.rect.size * 0.5f;
            if (half.x < 0f) half.x = 0f;
            if (half.y < 0f) half.y = 0f;
            pos.x = Mathf.Clamp(pos.x, -half.x, half.x);
            pos.y = Mathf.Clamp(pos.y, -half.y, half.y);
            return pos;
        }
    }
}
