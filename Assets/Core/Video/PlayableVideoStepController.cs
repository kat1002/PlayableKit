using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.Video;

[DisallowMultipleComponent]
[RequireComponent(typeof(VideoPlayer))]
[RequireComponent(typeof(AudioSource))]
public sealed class PlayableVideoStepController : MonoBehaviour
{
    private const float PlayRetryInterval = 0.2f;
    private const float AudioPlayRetryInterval = 0.5f;
    private const float EndEpsilon = 0.05f;
    private const float StoreReturnTapGuard = 0.75f;

    [System.Serializable]
    public sealed class VideoStep
    {
        public enum TapAction
        {
            ResumeVideo,
            OpenStore
        }

        [Min(0f)] public float pauseTime;
        public GameObject hook;
        public Button actionButton;
        public TapAction tapAction = TapAction.ResumeVideo;

        [Tooltip("OpenStore only: open the store on tap but keep the playable running; " +
                 "resume and play to the end when the user returns from the store.")]
        public bool resumeAfterStoreReturn;

        public UnityEvent onStepReached;

        [System.NonSerialized] public bool reached;
    }

    private enum FlowState
    {
        PlayingToStep,
        WaitingForStepTap,
        PlayingToEnd,
        WaitingForStoreReturn,
        Completed
    }

    [Header("Steps (Configured for 2-3 clicks)")]
    [SerializeField] private List<VideoStep> steps = new List<VideoStep>()
    {
        new VideoStep { pauseTime = 0.85f, tapAction = VideoStep.TapAction.ResumeVideo },
        new VideoStep { pauseTime = 6.1f, tapAction = VideoStep.TapAction.ResumeVideo }
    };

    [SerializeField, Min(0f), Tooltip("<= 0 or >= clip length => play to the end")]
    private float endTime = 0f;

    [SerializeField] private bool allowTapToStoreAfterLastStep = true;

    [Header("Scene References")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("Original Video Audio")]
    [SerializeField] private AudioClip originalVideoAudio;
    [SerializeField, Range(0f, 1f)] private float videoAudioVolume = 1f;
    [SerializeField, Min(0.01f)] private float audioSyncTolerance = 0.15f;

    [Header("Events")]
    [FormerlySerializedAs("onStepTwoReached")]
    [SerializeField] private UnityEvent onFinished;

    private AudioSource videoAudioSource;
    private FlowState state;
    private int activeStepIndex;
    private bool storeOpened;
    private bool resumeAfterInterruption;
    private bool gameStartedReported;
    private bool gameEndedReported;
    private bool videoStartPending;
    private float nextPlayAttemptAt;
    private float nextAudioPlayAttemptAt;
    private float storeReturnResumeArmedAt;
    private bool sawStoreInterruption;

    private struct BoundButton
    {
        public Button button;
        public UnityAction action;
    }
    private readonly List<BoundButton> boundButtons = new List<BoundButton>();

    private void Awake()
    {
        ResolveReferences();
        SortStepsChronologically();
        ConfigureVideoPlayer();
        ConfigureVideoAudio();
        HideAllHooks();
        BeginInitialPlayback();
    }

    // Gates must fire in time order. This runs once at startup rather than in
    // OnValidate so the Inspector list is never reordered while it is being edited.
    private void SortStepsChronologically()
    {
        if (steps == null)
        {
            return;
        }

        steps.Sort((a, b) =>
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            return a.pauseTime.CompareTo(b.pauseTime);
        });
    }

    private void OnEnable()
    {
        BindStepButtons();

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += HandleVideoEndReached;
        }

        Luna.Unity.LifeCycle.OnPause += HandleLunaPause;
        Luna.Unity.LifeCycle.OnResume += HandleLunaResume;
    }

    private void Start()
    {
        if (videoPlayer == null)
        {
            Debug.LogError("PlayableVideoStepController needs a VideoPlayer.", this);
            enabled = false;
            return;
        }

        if (!gameStartedReported)
        {
            gameStartedReported = true;
            Luna.Unity.LifeCycle.GameStarted();
        }
    }

    private void Update()
    {
        if (videoPlayer == null)
        {
            return;
        }

        AdvanceVideoStart();
        SynchronizePlayingVideoAudio();
        TryStartVideoAudioAfterUserGesture();

        switch (state)
        {
            case FlowState.PlayingToStep:
                if (HasStepAt(activeStepIndex) && videoPlayer.time >= steps[activeStepIndex].pauseTime)
                {
                    ReachStep(activeStepIndex);
                }
                break;

            case FlowState.PlayingToEnd:
                UpdatePlayingToEnd();
                break;

            case FlowState.WaitingForStoreReturn:
                if (Time.time >= storeReturnResumeArmedAt && TappedThisFrame())
                {
                    ResumeAfterStoreReturn();
                }
                break;
        }
    }

    private void OnDisable()
    {
        videoStartPending = false;

        UnbindStepButtons();

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= HandleVideoEndReached;
        }

        Luna.Unity.LifeCycle.OnPause -= HandleLunaPause;
        Luna.Unity.LifeCycle.OnResume -= HandleLunaResume;

        PauseVideoAudioAt(CurrentVideoTime());
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            HandleApplicationResumed();
        }
        else
        {
            HandleApplicationPaused();
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            HandleApplicationPaused();
        }
        else
        {
            HandleApplicationResumed();
        }
    }

    private void ResolveReferences()
    {
        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();
        }
    }

    private void ConfigureVideoPlayer()
    {
        if (videoPlayer == null)
        {
            return;
        }

        // Audio is supplied by the dedicated AudioSource below. Do not affect
        // the scene's music.MP3 AudioSource. The remaining video rendering
        // configuration is serialized in SampleScene because Luna exposes it
        // as read-only at script compile time.
        videoPlayer.isLooping = false;
        MuteVideoElementForAutoplay();
    }

    private void ConfigureVideoAudio()
    {
        if (videoAudioSource == null)
        {
            videoAudioSource = GetComponent<AudioSource>();
        }

        if (videoAudioSource == null)
        {
            videoAudioSource = gameObject.AddComponent<AudioSource>();
        }

        videoAudioSource.clip = originalVideoAudio;
        videoAudioSource.playOnAwake = false;
        videoAudioSource.loop = false;
        videoAudioSource.volume = videoAudioVolume;
    }

    private void BeginInitialPlayback()
    {
        if (videoPlayer == null)
        {
            return;
        }

        HideAllHooks();
        resumeAfterInterruption = false;
        storeOpened = false;
        activeStepIndex = 0;

        if (steps != null)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] != null)
                {
                    steps[i].reached = false;
                }
            }
        }

        state = HasStepAt(0) ? FlowState.PlayingToStep : FlowState.PlayingToEnd;
        PlayVideoAndItsAudio();
    }

    // Luna's Play() silently does nothing while its browser <video> is still
    // being created. Start immediately like the reference projects, then keep
    // retrying the same supported call until the player reports it is running.
    private void PlayVideoAndItsAudio()
    {
        if (videoPlayer == null)
        {
            return;
        }

        videoStartPending = true;
        MuteVideoElementForAutoplay();
        videoPlayer.Play();
        nextPlayAttemptAt = Time.time + PlayRetryInterval;
    }

    private void AdvanceVideoStart()
    {
        if (!videoStartPending || videoPlayer == null)
        {
            return;
        }

        if (videoPlayer.isPlaying)
        {
            videoStartPending = false;
            StartOrSynchronizeVideoAudio(true);
            return;
        }

        if (Time.time >= nextPlayAttemptAt)
        {
            MuteVideoElementForAutoplay();
            videoPlayer.Play();
            nextPlayAttemptAt = Time.time + PlayRetryInterval;
        }
    }

    // Luna creates the HTML <video> muted, but its direct-audio default can
    // unmute it before the first Play call. Browsers then reject autoplay.
    // Keep only the video element muted for startup; music.MP3 and the
    // dedicated original-audio source are not changed here.
    private void MuteVideoElementForAutoplay()
    {
        if (videoPlayer != null)
        {
            videoPlayer.SetDirectAudioMute(0, true);
        }
    }

    private void ReachStep(int index)
    {
        if (state != FlowState.PlayingToStep || !HasStepAt(index))
        {
            return;
        }

        var step = steps[index];
        step.reached = true;
        PauseVideoAndItsAudio();
        ActivateHook(step.hook);
        state = FlowState.WaitingForStepTap;
        // Fire this only after the hook is live in the hierarchy, so listeners can
        // safely drive components that sit on the hook or its children.
        step.onStepReached?.Invoke();
    }

    // SetActive(true) on the hook alone is not enough when a collapsed ancestor
    // (e.g. an orientation-specific container) keeps it inactive in the hierarchy.
    // Walk up and switch any such ancestor back on so the hook is really visible
    // before onStepReached runs.
    private static void ActivateHook(GameObject hook)
    {
        if (hook == null)
        {
            Debug.LogWarning("[VideoStepController] ReachStep: step has no hook assigned; onStepReached must activate its own UI.");
            return;
        }

        if (!hook.activeSelf)
        {
            hook.SetActive(true);
        }

        Transform ancestor = hook.transform.parent;
        while (ancestor != null && !hook.activeInHierarchy)
        {
            if (!ancestor.gameObject.activeSelf)
            {
                Debug.Log($"[VideoStepController] ReachStep: re-activating collapsed ancestor '{ancestor.name}' so hook '{hook.name}' is visible.");
                ancestor.gameObject.SetActive(true);
            }
            ancestor = ancestor.parent;
        }

        Debug.Log($"[VideoStepController] ReachStep: hook '{hook.name}' activeSelf={hook.activeSelf} activeInHierarchy={hook.activeInHierarchy}");
    }

    private void HandleStepAction(int index)
    {
        if (state != FlowState.WaitingForStepTap || !HasStepAt(index) || index != activeStepIndex)
        {
            return;
        }

        var step = steps[index];
        SetStepVisible(step.hook, false);

        if (step.tapAction == VideoStep.TapAction.OpenStore)
        {
            if (step.resumeAfterStoreReturn)
            {
                BeginWaitForStoreReturn();
            }
            else
            {
                Finish();
            }
            return;
        }

        AdvanceToNextSegment();
    }

    // Move past the current step: play to the next gate if there is one, otherwise
    // play straight to the end.
    private void AdvanceToNextSegment()
    {
        activeStepIndex++;
        if (HasStepAt(activeStepIndex))
        {
            state = FlowState.PlayingToStep;
            PlayVideoAndItsAudio();
        }
        else
        {
            EnterPlayingToEnd();
        }
    }

    private void EnterPlayingToEnd()
    {
        state = FlowState.PlayingToEnd;
        PlayVideoAndItsAudio();
    }

    // OpenStore step configured to keep the playable alive: open the store but stay
    // parked on the current frame until the user comes back (focus/OnResume or a tap).
    private void BeginWaitForStoreReturn()
    {
        PauseVideoAndItsAudio();
        state = FlowState.WaitingForStoreReturn;
        storeReturnResumeArmedAt = Time.time + StoreReturnTapGuard;
        sawStoreInterruption = false;

        // Not OpenStore(): must not latch storeOpened or report GameEnded here, so the
        // end-of-video Finish() still opens the store again as the end CTA.
        Luna.Unity.Playable.InstallFullGame();
    }

    // Returned from the store: pick the flow back up where it left off. Advancing
    // one step (not jumping to the end) keeps any later gate - e.g. step 3 - live.
    private void ResumeAfterStoreReturn()
    {
        if (state != FlowState.WaitingForStoreReturn)
        {
            return;
        }

        AdvanceToNextSegment();
    }

    private void UpdatePlayingToEnd()
    {
        if (allowTapToStoreAfterLastStep && TappedThisFrame())
        {
            Finish();
            return;
        }

        double currentTime = CurrentVideoTime();
        double clipLength = videoPlayer != null ? videoPlayer.length : 0d;

        if (endTime > 0f && (clipLength <= 0d || endTime < clipLength) && currentTime >= endTime)
        {
            Finish();
            return;
        }

        if (clipLength > 0d && currentTime >= clipLength - EndEpsilon)
        {
            Finish();
        }
    }

    private void HandleVideoEndReached(VideoPlayer vp)
    {
        if (state == FlowState.PlayingToEnd)
        {
            Finish();
        }
        else if (state == FlowState.PlayingToStep)
        {
            ReachStep(activeStepIndex);
        }
    }

    /// <summary>
    /// Completes the playable ad flow: pauses playback, invokes onFinished (endcard CTA),
    /// and triggers store install.
    /// </summary>
    public void Finish()
    {
        if (state == FlowState.Completed)
        {
            return;
        }

        state = FlowState.Completed;
        PauseVideoAndItsAudio();
        onFinished?.Invoke();
        OpenStore();
    }

    /// <summary>
    /// Directly triggers the Luna store installation call and reports game end.
    /// </summary>
    public void OpenStore()
    {
        if (storeOpened)
        {
            return;
        }

        storeOpened = true;
        ReportGameEnded();
        Luna.Unity.Playable.InstallFullGame();
    }

    private void ReportGameEnded()
    {
        if (gameEndedReported)
        {
            return;
        }

        gameEndedReported = true;
        Luna.Unity.LifeCycle.GameEnded();
    }

    private void HandleLunaPause()
    {
        HandleApplicationPaused();
    }

    private void HandleLunaResume()
    {
        HandleApplicationResumed();
    }

    private void HandleApplicationPaused()
    {
        if (IsPlayingSegment())
        {
            resumeAfterInterruption = true;
            videoStartPending = false;
            PauseVideoAndItsAudio();
        }
        else if (state == FlowState.WaitingForStoreReturn)
        {
            // The store actually took focus - now a regained focus means "user is back".
            sawStoreInterruption = true;
        }
    }

    private void HandleApplicationResumed()
    {
        if (state == FlowState.WaitingForStoreReturn)
        {
            // Only treat a regained focus as a store return if we saw focus leave
            // first; otherwise wait for the tap fallback in Update().
            if (sawStoreInterruption)
            {
                ResumeAfterStoreReturn();
            }
            return;
        }

        if (!resumeAfterInterruption)
        {
            return;
        }

        resumeAfterInterruption = false;

        if (IsPlayingSegment())
        {
            PlayVideoAndItsAudio();
        }
    }

    private void PauseVideoAndItsAudio()
    {
        videoStartPending = false;
        double pausedAt = CurrentVideoTime();

        if (videoPlayer != null)
        {
            videoPlayer.Pause();
        }

        // Do not seek after pausing. VideoPlayer seeks asynchronously, so the
        // displayed texture can remain on the pre-seek frame while playback
        // later resumes from the requested timestamp.
        PauseVideoAudioAt(pausedAt);
    }

    private void StartOrSynchronizeVideoAudio(bool forceSync)
    {
        if (videoAudioSource == null || videoAudioSource.clip == null)
        {
            return;
        }

        double videoTime = CurrentVideoTime();
        float audioTime = ClampAudioTime(videoTime);
        bool audioWasPlaying = videoAudioSource.isPlaying;

        if (!audioWasPlaying)
        {
            if (!forceSync && Time.time < nextAudioPlayAttemptAt)
            {
                return;
            }

            // AudioSource.time does not reliably stick while a source has
            // never been started. Create/resume its playback instance first,
            // then seek it to the video's current time.
            videoAudioSource.Play();
            nextAudioPlayAttemptAt = Time.time + AudioPlayRetryInterval;
        }

        if (forceSync || !audioWasPlaying ||
            Mathf.Abs(videoAudioSource.time - audioTime) > audioSyncTolerance)
        {
            videoAudioSource.time = audioTime;
        }
    }

    private void SynchronizePlayingVideoAudio()
    {
        if (videoPlayer == null || !videoPlayer.isPlaying || !IsPlayingSegment())
        {
            return;
        }

        StartOrSynchronizeVideoAudio(false);
    }

    private void PauseVideoAudioAt(double requestedTime)
    {
        if (videoAudioSource == null || videoAudioSource.clip == null)
        {
            return;
        }

        if (videoAudioSource.isPlaying)
        {
            videoAudioSource.Pause();
        }

        videoAudioSource.time = ClampAudioTime(requestedTime);
    }

    private void TryStartVideoAudioAfterUserGesture()
    {
        if (!IsPlayingSegment())
        {
            return;
        }

        if (TappedThisFrame())
        {
            StartOrSynchronizeVideoAudio(true);
        }
    }

    private bool IsPlayingSegment()
    {
        return state == FlowState.PlayingToStep || state == FlowState.PlayingToEnd;
    }

    private bool HasStepAt(int index)
    {
        return steps != null && index >= 0 && index < steps.Count && steps[index] != null;
    }

    private static bool TappedThisFrame()
    {
        if (Input.GetMouseButtonDown(0))
        {
            return true;
        }

        if (Input.touchCount > 0)
        {
            return Input.GetTouch(0).phase == TouchPhase.Began;
        }

        return false;
    }

    private void BindStepButtons()
    {
        UnbindStepButtons();

        if (steps == null)
        {
            return;
        }

        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step == null)
            {
                continue;
            }

            if (step.actionButton == null && step.hook != null)
            {
                step.actionButton = step.hook.GetComponentInChildren<Button>(true);
            }

            if (step.actionButton != null)
            {
                int index = i; // Closure capture: local copy
                UnityAction action = () => HandleStepAction(index);
                step.actionButton.onClick.AddListener(action);
                boundButtons.Add(new BoundButton { button = step.actionButton, action = action });
            }
            else if (step.tapAction == VideoStep.TapAction.ResumeVideo)
            {
                Debug.LogWarning($"[PlayableVideoStepController] Step {i} at {step.pauseTime}s has ResumeVideo action but no actionButton assigned or found on hook.", this);
            }
        }
    }

    private void UnbindStepButtons()
    {
        for (int i = 0; i < boundButtons.Count; i++)
        {
            if (boundButtons[i].button != null && boundButtons[i].action != null)
            {
                boundButtons[i].button.onClick.RemoveListener(boundButtons[i].action);
            }
        }
        boundButtons.Clear();
    }

    private void HideAllHooks()
    {
        if (steps == null)
        {
            return;
        }

        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] != null && steps[i].hook != null)
            {
                SetStepVisible(steps[i].hook, false);
            }
        }
    }

    private double CurrentVideoTime()
    {
        return videoPlayer == null ? 0d : videoPlayer.time;
    }

    private float ClampAudioTime(double requestedTime)
    {
        if (videoAudioSource == null || videoAudioSource.clip == null)
        {
            return 0f;
        }

        float maximum = Mathf.Max(0f, videoAudioSource.clip.length - 0.001f);
        return Mathf.Clamp((float)requestedTime, 0f, maximum);
    }

    private static void SetStepVisible(GameObject step, bool visible)
    {
        if (step != null && step.activeSelf != visible)
        {
            step.SetActive(visible);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        audioSyncTolerance = Mathf.Max(0.01f, audioSyncTolerance);

        if (steps == null)
        {
            return;
        }

        float lastGateTime = 0f;
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] == null)
            {
                continue;
            }

            steps[i].pauseTime = Mathf.Max(0f, steps[i].pauseTime);
            lastGateTime = Mathf.Max(lastGateTime, steps[i].pauseTime);
        }

        if (endTime > 0f && endTime < lastGateTime)
        {
            endTime = lastGateTime;
        }
    }
#endif
}
