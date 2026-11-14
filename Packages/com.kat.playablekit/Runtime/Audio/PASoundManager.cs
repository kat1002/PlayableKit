using System.Collections.Generic;
using UnityEngine;

namespace PlayableKit.Audio
{
    /// <summary>
    /// Config-driven sound manager: one looping BGM source plus a small pool of SFX channels
    /// played by string id. A new sound takes the first idle channel, else the oldest one.
    /// Mute is handled by <see cref="PAAudioManagerBase"/>.
    /// </summary>
    public class PASoundManager : PAAudioManagerBase
    {
        [SerializeField] private PASoundConfig config;

        private AudioSource _bgm;
        private AudioSource[] _sfx;
        private readonly Dictionary<string, int> _indexById = new Dictionary<string, int>();
        private float[] _entryLastTime;
        private float[] _channelStart;
        private int[] _channelEntry;

        protected override void OnAwake()
        {
            if (config == null) return;

            _bgm = gameObject.AddComponent<AudioSource>();
            _bgm.loop = true;
            _bgm.playOnAwake = false;
            _bgm.spatialBlend = 0f;

            int channels = Mathf.Max(1, config.sfxChannels);
            _sfx = new AudioSource[channels];
            _channelStart = new float[channels];
            _channelEntry = new int[channels];
            for (int i = 0; i < channels; i++)
            {
                _sfx[i] = gameObject.AddComponent<AudioSource>();
                _sfx[i].playOnAwake = false;
                _sfx[i].spatialBlend = 0f;
                _channelEntry[i] = -1;
            }

            int count = config.sfx != null ? config.sfx.Length : 0;
            _entryLastTime = new float[count];
            for (int i = 0; i < count; i++)
            {
                _entryLastTime[i] = -999f;
                string id = config.sfx[i].id;
                if (string.IsNullOrEmpty(id)) continue;
                if (_indexById.ContainsKey(id))
                {
                    Debug.LogWarning("[PASoundManager] Duplicate sound id '" + id + "' ignored.");
                    continue;
                }
                _indexById.Add(id, i);
            }
        }

        private void Start()
        {
            if (config != null && config.playBgmOnStart) PlayBgm();
        }

        /// <summary>Plays the sound with the given id on the active manager, if any.</summary>
        public static void Play(string id)
        {
            var manager = Instance as PASoundManager;
            if (manager != null) manager.PlaySfx(id);
        }

        public void PlayBgm()
        {
            if (_bgm == null || config.bgm == null) return;
            _bgm.clip = config.bgm;
            _bgm.volume = config.bgmVolume;
            if (!_bgm.isPlaying) _bgm.Play();
        }

        public void StopBgm()
        {
            if (_bgm != null) _bgm.Stop();
        }

        public void SetBgmVolume(float volume)
        {
            if (_bgm != null) _bgm.volume = Mathf.Clamp01(volume);
        }

        /// <summary>Returns the entry index for an id, or -1 when unknown.</summary>
        public int IndexOf(string id)
        {
            int index;
            return id != null && _indexById.TryGetValue(id, out index) ? index : -1;
        }

        public void PlaySfx(string id)
        {
            PlaySfx(IndexOf(id));
        }

        public void PlaySfx(int index)
        {
            if (IsMuted || config == null || _sfx == null) return;
            if (index < 0 || index >= config.sfx.Length) return;

            PASoundEntry entry = config.sfx[index];
            if (entry.clip == null) return;

            float now = Time.unscaledTime;
            if (now - _entryLastTime[index] < entry.minInterval) return;
            _entryLastTime[index] = now;

            int channel = PickChannel();
            AudioSource source = _sfx[channel];
            source.clip = entry.clip;
            source.volume = Mathf.Clamp01(entry.volume * config.sfxVolume);
            source.pitch = 1f + Random.Range(-entry.pitchJitter, entry.pitchJitter);
            source.time = entry.startOffset > 0f
                ? Mathf.Min(entry.startOffset, entry.clip.length - 0.01f)
                : 0f;
            source.Play();

            _channelStart[channel] = now;
            _channelEntry[channel] = index;
        }

        public void StopSfx(string id)
        {
            int index = IndexOf(id);
            if (_sfx == null || index < 0) return;
            for (int i = 0; i < _sfx.Length; i++)
            {
                if (_channelEntry[i] == index && _sfx[i].isPlaying) _sfx[i].Stop();
            }
        }

        public bool IsSfxPlaying(string id)
        {
            int index = IndexOf(id);
            if (_sfx == null || index < 0) return false;
            for (int i = 0; i < _sfx.Length; i++)
            {
                if (_channelEntry[i] == index && _sfx[i].isPlaying) return true;
            }
            return false;
        }

        /// <summary>First idle channel, else the one that started playing earliest.</summary>
        private int PickChannel()
        {
            int oldest = 0;
            for (int i = 0; i < _sfx.Length; i++)
            {
                if (!_sfx[i].isPlaying) return i;
                if (_channelStart[i] < _channelStart[oldest]) oldest = i;
            }
            return oldest;
        }
    }
}
