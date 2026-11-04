namespace PlayableUIKit
{
    /// <summary>
    /// A 1-axis steering input source (a drag-to-rotate wheel, tilt-steer, ...). Kept separate from
    /// <see cref="IMoveInput"/> — see that interface's doc comment for why.
    /// </summary>
    public interface ISteerInput
    {
        float SteerValue { get; }
        bool IsDragging { get; }
    }
}
