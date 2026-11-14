using UnityEngine;

namespace PlayableKit.Controls
{
    /// <summary>
    /// A 2D movement input source (a virtual joystick, tilt, WASD, ...). Kept separate from a
    /// 1-axis steering input rather than forcing one shape — a joystick's 2D value and a
    /// steering wheel's 1-axis value are genuinely different controls, not two implementations of
    /// the same thing.
    /// </summary>
    public interface IMoveInput
    {
        Vector2 Value { get; }
        bool IsPressed { get; }
    }
}
