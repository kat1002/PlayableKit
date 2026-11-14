using System;
using System.Collections.Generic;

namespace PlayableKit
{
    public static class GameEventManager
    {
        /// <summary>
        /// Event Id
        /// </summary>
        public enum EventId
        {
            GameLose,
            GameWin,
            LandscapeScreen,
            VerticalScreen,
            OnActiveUI,
            OnDeactiveUI,
            OnActiveCTA,
            OnDeactiveCTA,
            TimeEnd,
        }

        /// <summary>
        /// Call Back
        /// </summary>
        public delegate void GameEventCallBack();

        /// <summary>
        /// Event all call back in games
        /// </summary>
        private static Dictionary<EventId, List<GameEventCallBack>> _eventCallBacks = new Dictionary<EventId, List<GameEventCallBack>>();

        /// <summary>
        /// Register events
        /// </summary>
        /// <param name="eventId"></param>
        /// <param name="callBack"></param>
        public static void RegisterEvent(EventId eventId, GameEventCallBack callBack)
        {
            List<GameEventCallBack> _allCallBacks;
            if (!_eventCallBacks.TryGetValue(eventId, out _allCallBacks))
            {
                _allCallBacks = new List<GameEventCallBack>();
                _eventCallBacks[eventId] = _allCallBacks;
            }
            _allCallBacks.Add(callBack);
        }

        /// <summary>
        /// Unregister event
        /// </summary>
        /// <param name="eventId"></param>
        /// <param name="callBack"></param>
        public static void UnRegisterEvent(EventId eventId, GameEventCallBack callBack)
        {
            List<GameEventCallBack> _allCallBacks;
            if (_eventCallBacks.TryGetValue(eventId, out _allCallBacks))
            {
                _allCallBacks.Remove(callBack);
            }
        }

        /// <summary>
        /// Raised Event (reverse iteration for zero-GC and safe unregistration during invoke).
        /// </summary>
        /// <param name="eventId"></param>
        public static void RaisedEvent(EventId eventId)
        {
            List<GameEventCallBack> allCallback;
            if (_eventCallBacks.TryGetValue(eventId, out allCallback))
            {
                for (int i = allCallback.Count - 1; i >= 0; i--)
                {
                    if (allCallback[i] != null)
                    {
                        allCallback[i].Invoke();
                    }
                }
            }
        }
    }
}
