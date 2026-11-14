using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PlayableKit
{
    public enum PALogLevel
    {
        Verbose = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        None = 4
    }

    /// <summary>
    /// PlayableKit game-state logger. Format: <c>[PA][Class] 3.20s | message | state</c> (time is the Luna play time,
    /// <c>pre</c> before the first tap). It also shows in the Luna build's browser console. Luna-safe: string concatenation only.
    /// Preconfigured: Editor logs everything (Verbose), player and Luna builds log Info and up. Override per scene with a
    /// <see cref="PAConfig"/> component, at runtime via <see cref="Enabled"/> / <see cref="MinLevel"/>, or silence every
    /// log at compile time with the scripting define <c>PA_LOG_OFF</c>.
    /// </summary>
    public static class PALog
    {
        public const string Prefix = "[PA]";

        public static bool Enabled = true;
        public static bool ShowTime = true;

#if UNITY_EDITOR
        public static PALogLevel MinLevel = PALogLevel.Verbose;
#else
        public static PALogLevel MinLevel = PALogLevel.Info;
#endif

        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();

        public static bool IsOn(PALogLevel level)
        {
#if PA_LOG_OFF
            return false;
#else
            return Enabled && level != PALogLevel.None && level >= MinLevel;
#endif
        }

        public static void Verbose(string tag, string message, Object context = null)
        {
            Write(PALogLevel.Verbose, tag, message, context);
        }

        public static void Info(string tag, string message, Object context = null)
        {
            Write(PALogLevel.Info, tag, message, context);
        }

        public static void Warn(string tag, string message, Object context = null)
        {
            Write(PALogLevel.Warning, tag, message, context);
        }

        public static void Error(string tag, string message, Object context = null)
        {
            Write(PALogLevel.Error, tag, message, context);
        }

        /// <summary>Game-state line: <c>[PA][cls] time | msg | state</c>. Same call shape as the per-project PALog.</summary>
        public static void Log(string cls, string msg)
        {
            Write(PALogLevel.Info, cls, msg, null);
        }

        public static void Log(string cls, string msg, string state)
        {
            Write(PALogLevel.Info, cls, string.IsNullOrEmpty(state) ? msg : msg + " | " + state, null);
        }

        public static void Info(string message)
        {
            Write(PALogLevel.Info, null, message, null);
        }

        public static void Click(string what)
        {
            Write(PALogLevel.Info, "Click", what, null);
        }

        public static void Cta(string what)
        {
            Write(PALogLevel.Info, "Cta", what, null);
        }

        /// <summary>Logs a screen tap and the topmost UI object under it ("-" if none).</summary>
        public static void Tap(Vector2 screenPos)
        {
            if (!IsOn(PALogLevel.Info))
            {
                return;
            }

            string top = "no EventSystem";
            EventSystem es = EventSystem.current;
            if (es != null)
            {
                PointerEventData data = new PointerEventData(es);
                data.position = screenPos;
                Hits.Clear();
                es.RaycastAll(data, Hits);
                top = Hits.Count > 0 && Hits[0].gameObject != null ? Hits[0].gameObject.name : "-";
            }
            Write(PALogLevel.Info, "Tap", screenPos + " -> " + top, null);
        }

        private static string TimeText()
        {
            PALunaManager luna = PALunaManager.Instance;
            if (luna != null && luna.IsStarted)
            {
                return luna.PlayTime.ToString("F2") + "s";
            }
            return "pre " + Time.unscaledTime.ToString("F2") + "s";
        }

        private static void Write(PALogLevel level, string tag, string message, Object context)
        {
            if (!IsOn(level))
            {
                return;
            }

            string line = string.IsNullOrEmpty(tag) ? Prefix + " " : Prefix + "[" + tag + "] ";
            if (ShowTime)
            {
                line += TimeText() + " | ";
            }
            line += message;

            if (level == PALogLevel.Error)
            {
                Debug.LogError(line, context);
            }
            else if (level == PALogLevel.Warning)
            {
                Debug.LogWarning(line, context);
            }
            else
            {
                Debug.Log(line, context);
            }
        }
    }
}
