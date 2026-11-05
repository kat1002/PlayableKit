using UnityEngine;

namespace PlayableKit.UI
{
    /// <summary>
    /// A 2D movement input source (a virtual joystick, tilt, WASD, ...). Kept separate from
    /// <see cref="ISteerInput"/> rather than forcing one shape — a joystick's 2D value and a
    /// steering wheel's 1-axis value are genuinely different controls, not two implementations of
    /// the same thing.
    /// </summary>
    public interface IMoveInput
    {
        Vector2 Value { get; }
        bool IsPressed { get; }
    }
}
