using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace PlayableKit.Video
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VideoPlayer))]
    public sealed class PAAutoPlayVideoOnEnable : MonoBehaviour
    {
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private RawImage videoOutput;
        [SerializeField, Min(0f)] private float delay;
        [SerializeField] private bool restartFromBeginning;

        private const float RetryInterval = 0.2f;

        private bool playPending;
        private bool restartPending;
        private float playAt;
        private float nextPlayAttemptAt;
        private Texture assignedTexture;

        private void Awake()
        {
            ResolveVideoPlayer();

            if (videoPlayer == null)
            {
                Debug.LogError("PAAutoPlayVideoOnEnable requires a VideoPlayer.", this);
            }
        }

        private void OnEnable()
        {
            PlayVideo();
        }

        private void OnDisable()
        {
            playPending = false;
            restartPending = false;
        }

        /// <summary>
        /// Starts the configured VideoPlayer after this component is enabled.
        /// Luna creates the browser video element asynchronously, so retries stay alive
        /// until the player actually reports that it is running.
        /// </summary>
        public void PlayVideo()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            ResolveVideoPlayer();

            if (videoPlayer == null)
            {
                Debug.LogError("PAAutoPlayVideoOnEnable requires a VideoPlayer.", this);
                return;
            }

            playPending = true;
            restartPending = restartFromBeginning;
            playAt = Time.time + delay;
            nextPlayAttemptAt = playAt;
        }

        private void Update()
        {
            SyncVideoOutput();

            if (!playPending || Time.time < nextPlayAttemptAt)
            {
                return;
            }

            if (videoPlayer == null)
            {
                playPending = false;
                return;
            }

            if (restartPending)
            {
                if (videoPlayer.canSetTime)
                {
                    videoPlayer.time = 0d;
                }
                restartPending = false;
            }

            videoPlayer.Play();

            if (videoPlayer.isPlaying)
            {
                playPending = false;
                return;
            }

            nextPlayAttemptAt = Time.time + RetryInterval;
        }

        private void ResolveVideoPlayer()
        {
            if (videoPlayer == null)
            {
                videoPlayer = GetComponent<VideoPlayer>();
            }
            if (videoOutput == null)
            {
                videoOutput = GetComponent<RawImage>();
            }
        }

        private void SyncVideoOutput()
        {
            if (videoPlayer == null || videoOutput == null)
            {
                return;
            }

            Texture videoTexture = videoPlayer.texture;

            if (videoTexture != null && !ReferenceEquals(videoTexture, assignedTexture))
            {
                assignedTexture = videoTexture;
                videoOutput.texture = videoTexture;
            }
        }

    #if UNITY_EDITOR
        private void OnValidate()
        {
            delay = Mathf.Max(0f, delay);
        }
    #endif
    }
}
