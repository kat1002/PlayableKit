using System;

namespace PlayableUIKit
{
    /// <summary>
    /// Whatever this project's orientation broadcast mechanism is (an event bus, a direct poll,
    /// Unity's own <c>ScreenOrientation</c>...), a <see cref="BaseUIController"/> only needs this
    /// much of it. Implement a thin adapter over your project's actual mechanism — see the
    /// template README — rather than depending on that mechanism directly.
    /// </summary>
    public interface IOrientationSource
    {
        UIOrientation Current { get; }
        void Subscribe(Action<UIOrientation> onChanged);
        void Unsubscribe(Action<UIOrientation> onChanged);
    }
}
