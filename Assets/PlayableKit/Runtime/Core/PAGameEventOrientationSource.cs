using System;
using PlayableKit.UI;
using UnityEngine;

namespace PlayableKit
{
    /// <summary>
    /// Project-specific adapter: wraps this repo's <see cref="GameEventManager"/>
    /// LandscapeScreen/VerticalScreen broadcast (already raised by
    /// <c>PAScreenOrientationChecker</c>) as a <see cref="PlayableKit.UI.IOrientationSource"/>, so
    /// <see cref="PlayableKit.UI.PABaseUIController"/> subclasses never reference
    /// <see cref="GameEventManager"/> directly. One instance per scene is enough — assign it on
    /// every controller that needs orientation reactions.
    /// </summary>
    public sealed class PAGameEventOrientationSource : MonoBehaviour, IOrientationSource
    {
        private event Action<UIOrientation> Changed;
        private UIOrientation _current = UIOrientation.Landscape;

        public UIOrientation Current => _current;

        private void OnEnable()
        {
            GameEventManager.RegisterEvent(GameEventManager.EventId.LandscapeScreen, HandleLandscape);
            GameEventManager.RegisterEvent(GameEventManager.EventId.VerticalScreen, HandlePortrait);
        }

        private void OnDisable()
        {
            GameEventManager.UnRegisterEvent(GameEventManager.EventId.LandscapeScreen, HandleLandscape);
            GameEventManager.UnRegisterEvent(GameEventManager.EventId.VerticalScreen, HandlePortrait);
        }

        public void Subscribe(Action<UIOrientation> onChanged) => Changed += onChanged;
        public void Unsubscribe(Action<UIOrientation> onChanged) => Changed -= onChanged;

        private void HandleLandscape()
        {
            _current = UIOrientation.Landscape;
            Changed?.Invoke(_current);
        }

        private void HandlePortrait()
        {
            _current = UIOrientation.Portrait;
            Changed?.Invoke(_current);
        }
    }
}
