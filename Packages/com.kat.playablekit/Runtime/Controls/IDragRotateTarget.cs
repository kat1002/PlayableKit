namespace PlayableKit.Controls
{
    /// <summary>Receives drag-rotate input from PAUiDragRotate (e.g. an orbit camera).</summary>
    public interface IDragRotateTarget
    {
        void Hold(bool held);
        void Drag(float dxPixels, float dyPixels);
    }
}
