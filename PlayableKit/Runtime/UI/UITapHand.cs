using UnityEngine;
using UnityEngine.UI;

namespace PlayableKit
{
    /// <summary>
    /// "Tap here" instruction hand. Swaps an Image between a raised-hand sprite and a pressed-hand
    /// sprite on a repeating cycle, with an optional scale dip while pressed. Runs on unscaled time
    /// and uses no tweens or lambdas (Luna-safe). Sprites: Sprites/TapHand_1 (raised), TapHand_2 (pressed).
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class UITapHand : MonoBehaviour
    {
        [Header("Sprites")]
        [SerializeField] private Image target;
        [SerializeField] private Sprite raisedSprite;
        [SerializeField] private Sprite pressedSprite;
        [SerializeField] private bool setNativeSize = false;

        [Header("Timing")]
        [Tooltip("Seconds for one full raised-then-pressed cycle.")]
        [SerializeField] private float cycleSeconds = 1f;
        [Tooltip("Fraction of the cycle (0-1) spent in the pressed pose, at the end of the cycle.")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float pressedFraction = 0.4f;

        [Header("Press feel")]
        [SerializeField] private float pressedScale = 0.9f;

        [Header("Tap ring (optional)")]
        [Tooltip("Optional ring pulsed each time the hand presses. Leave empty for no ring.")]
        [SerializeField] private UITapRing tapRing;

        private Vector3 _baseScale = Vector3.one;
        private float _time;
        private bool _wasPressed;

        private void Reset()
        {
            target = GetComponent<Image>();
        }

        private void Awake()
        {
            if (target == null)
            {
                target = GetComponent<Image>();
            }

            _baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            _time = 0f;
            _wasPressed = false;
            Apply(false);
        }

        private void OnDisable()
        {
            transform.localScale = _baseScale;
            if (tapRing != null)
            {
                tapRing.Stop();
            }
        }

        private void Update()
        {
            float cycle = Mathf.Max(0.05f, cycleSeconds);
            _time += Time.unscaledDeltaTime;
            float phase = (_time % cycle) / cycle;
            bool pressed = phase >= 1f - pressedFraction;
            Apply(pressed);

            if (pressed && !_wasPressed && tapRing != null)
            {
                tapRing.Play();
            }

            _wasPressed = pressed;
        }

        private void Apply(bool pressed)
        {
            if (target == null)
            {
                return;
            }

            Sprite sprite = pressed ? pressedSprite : raisedSprite;
            if (sprite != null && target.sprite != sprite)
            {
                target.sprite = sprite;
                if (setNativeSize)
                {
                    target.SetNativeSize();
                }
            }

            transform.localScale = pressed ? _baseScale * pressedScale : _baseScale;
        }
    }
}
