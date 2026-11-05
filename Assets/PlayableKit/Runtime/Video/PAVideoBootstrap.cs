using System.Collections;
using UnityEngine;
using UnityEngine.Video;

namespace PlayableKit.Video
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class PAVideoBootstrap : MonoBehaviour
    {
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool playLoop = true;
        [SerializeField] private float retrySeconds = 3f;

        [Header("Start-screen soundtrack")]
        // The video's own audio track is left muted (Luna only partially supports
        // UnityEngine.Video audio) - the soundtrack is played from this standalone clip
        // instead. Left empty = no sound, no-op. The AudioSource is created at runtime on
        // this GameObject, so it is torn down with the "VideoPlay" object on level pick.
        [SerializeField] private AudioClip introAudio;
        [SerializeField, Range(0f, 1f)] private float introVolume = 1f;

        private AudioSource introAudioSource;


        private void Awake()
        {
            CacheVideoPlayer();
            ConfigureVideoPlayer();
            SetupIntroAudio();
        }

        private void OnEnable()
        {
            if (playOnStart)
            {
                StartCoroutine(PlayMutedWhenReady());
                PlayIntroAudio();
            }
        }

        private void OnDisable()
        {
            if (introAudioSource != null)
            {
                introAudioSource.Stop();
            }
        }



        private void CacheVideoPlayer()
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

            VideoClip clip = videoPlayer.clip;
            videoPlayer.playOnAwake = false;
            videoPlayer.source = VideoSource.VideoClip;

            if (clip != null)
            {
                videoPlayer.clip = clip;
            }

            videoPlayer.SetDirectAudioMute(0, true);
        }

        private void SetupIntroAudio()
        {
            if (introAudio == null || introAudioSource != null)
            {
                return;
            }

            introAudioSource = gameObject.AddComponent<AudioSource>();
            introAudioSource.clip = introAudio;
            introAudioSource.loop = true;
            introAudioSource.playOnAwake = false;
            introAudioSource.spatialBlend = 0f; // 2D
            introAudioSource.volume = introVolume;
        }

        private void PlayIntroAudio()
        {
            if (introAudioSource != null && !introAudioSource.isPlaying)
            {
                introAudioSource.Play();
            }
        }

        private IEnumerator PlayMutedWhenReady()
        {
            float endTime = Time.realtimeSinceStartup + retrySeconds;

            while (Time.realtimeSinceStartup <= endTime)
            {
                ConfigureVideoPlayer();

                if (videoPlayer == null)
                {
                    yield break;
                }

                if (videoPlayer.clip != null && !videoPlayer.isPlaying)
                {
                    videoPlayer.Play();
                    videoPlayer.isLooping = playLoop;

                }


                if (videoPlayer.isPlaying)
                {
                    yield break;
                }

                yield return null;
            }
        }
    }
}
