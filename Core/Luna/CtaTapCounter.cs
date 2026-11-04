using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CtaTapCounter : MonoBehaviour
{
    public static CtaTapCounter Instance { get; private set; }

    [Header("Count Rule")]
    [SerializeField, Min(1)] private int max = 6;
    [SerializeField, Min(0)] private int count = 0;

    [Header("Rule Switch After First Complete")]
    [SerializeField] private bool switchMaxAfterFirstComplete = true;
    [SerializeField, Min(1)] private int maxAfterFirstComplete = 1;

    [Header("Count Input Mode")]
    [Tooltip("Count automatically on every screen tap / mouse down (safe for Playworks/Luna).")]
    [SerializeField] private bool autoCountGlobalPointerDown = true;

    [Header("Auto Bind Buttons")]
    [Tooltip("Optional UI buttons that also increment the tap counter on click.")]
    [SerializeField] private Button[] extraButtons;

    [Header("Target CTA")]
    [Tooltip("Optional fullscreen CTA button to activate when max taps reached.")]
    public GameObject fullscreenBtn;

    [Header("Events")]
    [Tooltip("Fires once when CountUp reaches max for the current cycle.")]
    public UnityEvent onDownWhenReachedOrMore;

    [Tooltip("Fires once on PointerUp after reaching max for the current cycle.")]
    public UnityEvent onUpWhenJustReached;

    private bool reachedThisPress = false;
    private bool hasCompletedAtLeastOnce = false;
    private bool switchedToAfterRule = false;

    // Anti-spam lock: once reached, ignore further CountUp calls until ConsumeCompleted_SwitchRuleOnce or SetMaxAndReset.
    private bool cycleCompletedLocked = false;
    private bool firedDownInCycle = false;
    private bool firedUpInCycle = false;

    private int lastAutoDownFrame = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (fullscreenBtn != null)
            fullscreenBtn.SetActive(false);

        BindExtraButtons();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!autoCountGlobalPointerDown) return;
        if (cycleCompletedLocked) return;

        if (IsPointerDownThisFrame())
        {
            if (lastAutoDownFrame == Time.frameCount) return;
            lastAutoDownFrame = Time.frameCount;
            CountUp();
        }
    }

    private static bool IsPointerDownThisFrame()
    {
        if (Input.GetMouseButtonDown(0))
            return true;

        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                if (Input.GetTouch(i).phase == TouchPhase.Began)
                    return true;
            }
        }

        return false;
    }

    public void CountUp()
    {
        if (cycleCompletedLocked) return;

        int targetMax = Mathf.Max(1, max);

        count++;
        if (count > targetMax)
            count = targetMax;

        if (count >= targetMax)
        {
            cycleCompletedLocked = true;
            hasCompletedAtLeastOnce = true;
            reachedThisPress = true;

            if (fullscreenBtn != null && !fullscreenBtn.activeSelf)
                fullscreenBtn.SetActive(true);

            if (!firedDownInCycle)
            {
                firedDownInCycle = true;
                if (onDownWhenReachedOrMore != null)
                    onDownWhenReachedOrMore.Invoke();
            }
        }
    }

    public void PointerUp()
    {
        if (!reachedThisPress) return;

        reachedThisPress = false;

        if (!firedUpInCycle)
        {
            firedUpInCycle = true;
            if (onUpWhenJustReached != null)
                onUpWhenJustReached.Invoke();
        }
    }

    // Call from fullscreenBtn onClick after opening store to arm the post-return tap rule.
    public void ConsumeCompleted_SwitchRuleOnce()
    {
        if (!hasCompletedAtLeastOnce) return;

        if (switchMaxAfterFirstComplete && !switchedToAfterRule)
        {
            max = Mathf.Max(1, maxAfterFirstComplete);
            switchedToAfterRule = true;
        }

        ResetCycleState(hideFullscreenBtn: true);
    }

    public void SetMaxAndReset(int newMax)
    {
        max = Mathf.Max(1, newMax);
        ResetCycleState(hideFullscreenBtn: true);
    }

    private void ResetCycleState(bool hideFullscreenBtn)
    {
        count = 0;
        reachedThisPress = false;
        cycleCompletedLocked = false;
        firedDownInCycle = false;
        firedUpInCycle = false;

        if (hideFullscreenBtn && fullscreenBtn != null)
            fullscreenBtn.SetActive(false);
    }

    public void RebindButtons()
    {
        BindExtraButtons();
    }

    private void BindExtraButtons()
    {
        if (extraButtons == null) return;

        for (int i = 0; i < extraButtons.Length; i++)
        {
            Button btn = extraButtons[i];
            if (btn == null) continue;
            btn.onClick.RemoveListener(CountUp);
            btn.onClick.AddListener(CountUp);
        }
    }
}
