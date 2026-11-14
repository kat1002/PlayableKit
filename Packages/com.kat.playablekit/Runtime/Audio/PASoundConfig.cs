using System;
using UnityEngine;

namespace PlayableKit.Audio
{
    /// <summary>One named sound effect played by <see cref="PASoundManager"/>.</summary>
    [Serializable]
    public sealed class PASoundEntry
    {
        public string id;
        public AudioClip clip;
        [Range(0f, 3f)] public float volume = 1f;
        [Tooltip("Minimum seconds between two plays of this entry.")]
        [Min(0f)] public float minInterval;
        [Tooltip("Random pitch offset (+/-) applied on each play.")]
        [Range(0f, 0.5f)] public float pitchJitter;
        [Tooltip("Seconds to skip at the start of the clip.")]
        [Min(0f)] public float startOffset;
    }

    /// <summary>Background music and string-id sound effects for <see cref="PASoundManager"/>.</summary>
    [CreateAssetMenu(fileName = "PASoundConfig", menuName = "PlayableKit/Sound Config")]
    public sealed class PASoundConfig : ScriptableObject
    {
        public AudioClip bgm;
        [Range(0f, 1f)] public float bgmVolume = 0.5f;
        public bool playBgmOnStart = true;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;
        [Tooltip("Number of sound effects that can overlap.")]
        [Min(1)] public int sfxChannels = 3;
        public PASoundEntry[] sfx;
    }
}
