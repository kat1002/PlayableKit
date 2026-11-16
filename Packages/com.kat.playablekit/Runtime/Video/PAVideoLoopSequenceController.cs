using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

using PlayableKit.UI;

namespace PlayableKit.Video
{
    // Luna compiles the kit and project scripts as one assembly, so a project's global TScreen
    // would win over the `using PlayableKit.UI` import. A namespace-level alias binds first.
    using TScreen = PlayableKit.UI.TScreen;

    public sealed class PAVideoLoopSequenceController : MonoBehaviour
    {
        [Serializable]
        private sealed class VideoPlayableView
        {
            [SerializeField] private TScreen screen = TScreen.None;
            [SerializeField] private GameObject root;
            [SerializeField] private Button button1;
            [SerializeField] private Button button2;
            [SerializeField] private Button button3;

            public TScreen Screen => screen;
            public GameObject Root => root;
            public Button Button1 => button1;
            public Button Button2 => button2;
            public Button Button3 => button3;

            public bool Matches(TScreen targetScreen)
            {
                return targetScreen != TScreen.None && screen == targetScreen;
            }

            public bool IsVisible()
            {
                return root != null && root.activeInHierarchy;
            }
        }

        private enum FlowState
        {
            WaitingForButton1,
            PlayingVideo1,
            WaitingForButton2,
            PlayingVideo2,
            LoopingVideo3
        }

        [Header("Scene References")]
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private PAScreenOrientationChecker screenOrientationChecker;
        [SerializeField] private VideoPlayableView[] videoPlayables;

        [Header("Legacy Fallback")]
        [SerializeField] private Button button1;
        [SerializeField] private Button button2;
        [SerializeField] private Button button3;

        [Header("Video Flow")]
        [SerializeField] private VideoClip[] startClips;
        [SerializeField] private VideoClip loopClip;

        private FlowState _state = FlowState.WaitingForButton1;
        private VideoPlayableView _currentPlayable;

        private void Awake()
        {
            ResolveReferences();
            ConfigureVideoPlayer();
            RefreshCurrentPlayable(GetPreferredScreen());
        }

        private void OnEnable()
        {
            GameEventManager.RegisterEvent(GameEventManager.EventId.LandscapeScreen, HandleLandscapeScreen);
            GameEventManager.RegisterEvent(GameEventManager.EventId.VerticalScreen, HandleVerticalScreen);
            RefreshCurrentPlayable(GetPreferredScreen());
        }

        private void Start()
        {
            RefreshCurrentPlayable(GetPreferredScreen());
        }

        private void OnDisable()
        {
            UnsubscribeVideoEvents();
            GameEventManager.UnRegisterEvent(GameEventManager.EventId.LandscapeScreen, HandleLandscapeScreen);
            GameEventManager.UnRegisterEvent(GameEventManager.EventId.VerticalScreen, HandleVerticalScreen);
        }

        public void OnButton1Pressed()
        {
            RefreshCurrentPlayable(GetPreferredScreen());

            if (_state != FlowState.WaitingForButton1 || videoPlayer == null)
            {
                return;
            }

            PlayVideo1();
        }

        public void OnButton2Pressed()
        {
            RefreshCurrentPlayable(GetPreferredScreen());

            if (_state != FlowState.WaitingForButton2 || videoPlayer == null)
            {
                return;
            }

            PlayVideo2();
        }

        private void ResolveReferences()
        {
            if (videoPlayer == null)
            {
                videoPlayer = GetComponent<VideoPlayer>();
            }

            if (screenOrientationChecker == null)
            {
                screenOrientationChecker = GetComponent<PAScreenOrientationChecker>();
            }
        }

        private void ConfigureVideoPlayer()
        {
            if (videoPlayer == null)
            {
                return;
            }

            VideoPlayerLoopUtility.ApplyConfiguredLoop(videoPlayer, shouldLoop: false);
        }

        private void HandleLandscapeScreen()
        {
            RefreshCurrentPlayable(TScreen.LandscapeScreen);
        }

        private void HandleVerticalScreen()
        {
            RefreshCurrentPlayable(TScreen.VerticalScreen);
        }

        private void RefreshCurrentPlayable(TScreen preferredScreen)
        {
            _currentPlayable = ResolveCurrentPlayable(preferredScreen);
            ApplyButtonsForCurrentState();
        }

        private VideoPlayableView ResolveCurrentPlayable(TScreen preferredScreen)
        {
            if (videoPlayables != null && videoPlayables.Length > 0)
            {
                foreach (var playable in videoPlayables)
                {
                    if (playable != null && playable.Matches(preferredScreen) && playable.IsVisible())
                    {
                        return playable;
                    }
                }

                foreach (var playable in videoPlayables)
                {
                    if (playable != null && playable.IsVisible())
                    {
                        return playable;
                    }
                }

                foreach (var playable in videoPlayables)
                {
                    if (playable != null && playable.Root != null)
                    {
                        return playable;
                    }
                }

                foreach (var playable in videoPlayables)
                {
                    if (playable != null && playable.Matches(preferredScreen))
                    {
                        return playable;
                    }
                }
            }

            return null;
        }

        private TScreen GetPreferredScreen()
        {
            if (screenOrientationChecker != null && screenOrientationChecker.currentScreen != TScreen.None)
            {
                return screenOrientationChecker.currentScreen;
            }

            return Screen.width > Screen.height ? TScreen.LandscapeScreen : TScreen.VerticalScreen;
        }

        private void ApplyButtonsForCurrentState()
        {
            HideAllButtons();

            switch (_state)
            {
                case FlowState.WaitingForButton1:
                    SetButtonActive(GetCurrentButton1(), true);
                    break;

                case FlowState.WaitingForButton2:
                    SetButtonActive(GetCurrentButton2(), true);
                    break;

                case FlowState.LoopingVideo3:
                    SetButtonActive(GetCurrentButton3(), true);
                    break;
            }
        }

        private void HideAllButtons()
        {
            if (videoPlayables != null)
            {
                foreach (var playable in videoPlayables)
                {
                    if (playable == null)
                    {
                        continue;
                    }

                    SetButtonActive(playable.Button1, false);
                    SetButtonActive(playable.Button2, false);
                    SetButtonActive(playable.Button3, false);
                }
            }

            SetButtonActive(button1, false);
            SetButtonActive(button2, false);
            SetButtonActive(button3, false);
        }

        private Button GetCurrentButton1()
        {
            return _currentPlayable != null && _currentPlayable.Button1 != null
                ? _currentPlayable.Button1
                : button1;
        }

        private Button GetCurrentButton2()
        {
            return _currentPlayable != null && _currentPlayable.Button2 != null
                ? _currentPlayable.Button2
                : button2;
        }

        private Button GetCurrentButton3()
        {
            return _currentPlayable != null && _currentPlayable.Button3 != null
                ? _currentPlayable.Button3
                : button3;
        }

        private static void SetButtonActive(Button targetButton, bool isActive)
        {
            if (targetButton != null)
            {
                targetButton.gameObject.SetActive(isActive);
            }
        }

        private void PlayVideo1()
        {
            if (!TryGetStartClip(0, out var clip))
            {
                return;
            }

            _state = FlowState.PlayingVideo1;
            RegisterVideo1End();
            ApplyButtonsForCurrentState();
            PlayClip(clip, shouldLoop: false);
        }

        private void PlayVideo2()
        {
            if (!TryGetStartClip(1, out var clip))
            {
                PlayVideo3Loop();
                return;
            }

            _state = FlowState.PlayingVideo2;
            RegisterVideo2End();
            ApplyButtonsForCurrentState();
            PlayClip(clip, shouldLoop: false);
        }

        private void PlayVideo3Loop()
        {
            if (loopClip == null)
            {
                return;
            }

            _state = FlowState.LoopingVideo3;
            RegisterVideo3LoopEnd();
            ApplyButtonsForCurrentState();
            PlayClip(loopClip, shouldLoop: true);
        }

        private void RegisterVideo1End()
        {
          //  UnsubscribeVideoEvents();
            videoPlayer.loopPointReached += HandleVideo1Ended;
        }

        private void RegisterVideo2End()
        {
            UnsubscribeVideoEvents();
            videoPlayer.loopPointReached += HandleVideo2Ended;
        }

        private void RegisterVideo3LoopEnd()
        {
            UnsubscribeVideoEvents();
    #if UNITY_LUNA
            videoPlayer.loopPointReached += HandleVideo3LoopEnded;
    #endif
        }

        private void PlayClip(VideoClip clip, bool shouldLoop)
        {
            if (videoPlayer == null || clip == null)
            {
                return;
            }
          //  videoPlayer.Stop();
            videoPlayer.clip = clip;
            VideoPlayerLoopUtility.ApplyConfiguredLoop(videoPlayer, shouldLoop);
            videoPlayer.Play();
        }

        private void HandleVideo1Ended(VideoPlayer finishedPlayer)
        {
            finishedPlayer.loopPointReached -= HandleVideo1Ended;
            _state = FlowState.WaitingForButton2;
            RefreshCurrentPlayable(GetPreferredScreen());
        }

        private void HandleVideo2Ended(VideoPlayer finishedPlayer)
        {
            finishedPlayer.loopPointReached -= HandleVideo2Ended;
            PlayVideo3Loop();
        }

    #if UNITY_LUNA
        private void HandleVideo3LoopEnded(VideoPlayer finishedPlayer)
        {
            VideoPlayerLoopUtility.RestartIfLooping(finishedPlayer, shouldLoop: true);
        }
    #endif

        private void UnsubscribeVideoEvents()
        {
            if (videoPlayer == null)
            {
                return;
            }

            videoPlayer.loopPointReached -= HandleVideo1Ended;
            videoPlayer.loopPointReached -= HandleVideo2Ended;
    #if UNITY_LUNA
            videoPlayer.loopPointReached -= HandleVideo3LoopEnded;
    #endif
        }

        private bool TryGetStartClip(int index, out VideoClip clip)
        {
            clip = null;

            if (startClips == null || index < 0 || index >= startClips.Length)
            {
                return false;
            }

            clip = startClips[index];
            return clip != null;
        }
    }
}
