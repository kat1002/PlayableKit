using System;
using BlackGemsGlobal.SeatAway.GamePlayEvent;
using PlayableUIKit;
using UnityEngine;

namespace Shared.UI.Adapters
{
    /// <summary>
    /// Project-specific adapter: wraps this repo's <see cref="GameEventManager"/>
    /// LandscapeScreen/VerticalScreen broadcast (already raised by
    /// <c>ScreenOrientationChecker</c>) as a <see cref="PlayableUIKit.IOrientationSource"/>, so
    /// <see cref="PlayableUIKit.BaseUIController"/> subclasses never reference
    /// <see cref="GameEventManager"/> directly. One instance per scene is enough — assign it on
    /// every controller that needs orientation reactions.
    /// </summary>
    public sealed class GameEventOrientationSource : MonoBehaviour, IOrientationSource
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
