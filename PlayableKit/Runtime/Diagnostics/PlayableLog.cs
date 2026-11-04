using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PlayableKit
{
    /// <summary>
    /// Tiny console logger for playables (shows in the Luna build's browser console too).
    /// Set <see cref="Enabled"/> to false before shipping.
    /// </summary>
    public static class PlayableLog
    {
        public static bool Enabled = true;

        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();

        public static void Info(string message)
        {
            if (Enabled)
            {
                Debug.Log("[LunaLog] Time: " + Time.unscaledTime.ToString("F1") + "s | " + message);
            }
        }

        public static void Click(string what)
        {
            Info("CLICK " + what);
        }

        public static void Cta(string what)
        {
            Info("CTA " + what);
        }

        /// <summary>Logs a screen tap and the topmost UI object under it ("-" if none).</summary>
        public static void Tap(Vector2 screenPos)
        {
            if (!Enabled)
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
            Info("TAP " + screenPos + " -> " + top);
        }
    }
}
