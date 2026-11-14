using UnityEngine;

namespace PlayableKit
{
    /// <summary>
    /// Scene-level PlayableKit settings. Put one on the PALunaRoot (or any root object); it applies its values on Awake.
    /// Without a PAConfig in the scene, <see cref="PALog"/> keeps its built-in defaults.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class PAConfig : MonoBehaviour
    {
        private static PAConfig instance;

        public static PAConfig Instance { get { return instance; } }

        [Header("Logging (PALog)")]
        [Tooltip("Game state logs. Turn off before shipping if the network asks for a quiet console.")]
        [SerializeField] private bool m_EnableGameLog = true;
        [Tooltip("Lowest level that is printed.")]
        [SerializeField] private PALogLevel m_MinLogLevel = PALogLevel.Info;
        [Tooltip("Prefix each line with the Luna play time.")]
        [SerializeField] private bool m_ShowLogTime = true;

        public bool EnableGameLog { get { return m_EnableGameLog; } }

        private void Awake()
        {
            instance = this;
            Apply();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                Apply();
            }
        }
#endif

        public void Apply()
        {
            PALog.Enabled = m_EnableGameLog;
            PALog.MinLevel = m_MinLogLevel;
            PALog.ShowTime = m_ShowLogTime;
        }
    }
}
