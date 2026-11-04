using UnityEngine;

namespace PlayableTemplate.Core
{
    /// <summary>
    /// Single touch-point for the playable's sound: a two-source crossfaded loop (e.g. an
    /// engine or ambient bed), a shared one-shot source, and a global mute switch. Subclass
    /// and call CreateLoopSources(...) from OnAwake() to wire game-specific clips, and
    /// PlayOneShotSafe(...)/PlayOneShotSafeWithPitch(...) for game-specific one-shots.
    /// </summary>
    public class AudioManagerBase : MonoBehaviour
    {
        public static AudioManagerBase Instance { get; protected set; }

        private const string MutePrefsKey = "PlayableTemplate.Muted";

        [Header("Loop crossfade (e.g. idle -> high with speed)")]
        [Range(0f, 1f)] public float loopVolume = 0.6f;
        [Tooltip("Pitch at t=0 / t=1 in SetLoopCrossfadeIntensity.")]
        public Vector2 loopPitchRange = new Vector2(0.9f, 1.25f);

        [Header("One-shots")]
        [Tooltip("Light CTA/UI tap feedback. Leave empty until a clip is picked.")]
        [SerializeField] protected AudioClip tap;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;

        public static bool IsMuted { get; private set; }
        public static event System.Action<bool> OnMuteChanged;

        protected AudioSource sourceA;
        protected AudioSource sourceB;
        protected AudioSource oneShotSource;

        protected virtual void Awake()
        {
            Instance = this;

            oneShotSource = gameObject.AddComponent<AudioSource>();
            oneShotSource.playOnAwake = false;
            oneShotSource.spatialBlend = 0f;

            IsMuted = PlayerPrefs.GetInt(MutePrefsKey, 0) == 1;
            AudioListener.volume = IsMuted ? 0f : 1f;

            OnAwake();
        }

        /// <summary>Call CreateLoopSources(...) with your own clips here.</summary>
        protected virtual void OnAwake() { }

        protected AudioSource CreateLoopSource(AudioClip clip)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = 0f;
            return src;
        }

        protected void CreateLoopSources(AudioClip a, AudioClip b)
        {
            sourceA = CreateLoopSource(a);
            sourceB = CreateLoopSource(b);
        }

        public void StartLoopCrossfade()
        {
            if (sourceA != null && sourceA.clip != null) sourceA.Play();
            if (sourceB != null && sourceB.clip != null) sourceB.Play();
        }

        public void StopLoopCrossfade()
        {
            if (sourceA != null) sourceA.Stop();
            if (sourceB != null) sourceB.Stop();
        }

        /// <summary>0 = fully source A, 1 = fully source B.</summary>
        public void SetLoopCrossfadeIntensity(float t)
        {
            t = Mathf.Clamp01(t);
            if (sourceA != null) sourceA.volume = loopVolume * (1f - t);
            if (sourceB != null) sourceB.volume = loopVolume * t;

            float pitch = Mathf.Lerp(loopPitchRange.x, loopPitchRange.y, t);
            if (sourceA != null) sourceA.pitch = pitch;
            if (sourceB != null) sourceB.pitch = pitch;
        }

        public void PlayTap()
        {
            if (tap == null) return;
            PlayOneShotSafe(tap, sfxVolume * 0.6f, "PlayTap");
        }

        protected void PlayOneShotSafe(AudioClip clip, float volume, string label)
        {
            if (clip == null || oneShotSource == null) return;
            try { oneShotSource.PlayOneShot(clip, volume); }
            catch (System.Exception e) { Debug.LogWarning("[AudioManager] " + label + " failed: " + e.Message); }
        }

        protected void PlayOneShotSafeWithPitch(AudioClip clip, float volume, Vector2 pitchRange, string label)
        {
            if (clip == null || oneShotSource == null) return;
            // A clip that fails to decode on WebGL's stricter browser-native audio path can
            // throw deep inside PlayOneShot's native bridge; guard so one bad clip can never
            // take down the rest of that frame's Update() calls or corrupt the shared source.
            try
            {
                oneShotSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
                oneShotSource.PlayOneShot(clip, volume);
            }
            catch (System.Exception e) { Debug.LogWarning("[AudioManager] " + label + " failed: " + e.Message); }
            finally { oneShotSource.pitch = 1f; }
        }

        // ---- Mute ------------------------------------------------------------------------
        public void ToggleMute() => SetMuted(!IsMuted);

        public void SetMuted(bool muted)
        {
            IsMuted = muted;
            AudioListener.volume = muted ? 0f : 1f;
            PlayerPrefs.SetInt(MutePrefsKey, muted ? 1 : 0);
            OnMuteChanged?.Invoke(muted);
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
