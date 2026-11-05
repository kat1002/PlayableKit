namespace PlayableKit
{
    /// <summary>Receives drag-rotate input from UiDragRotate (e.g. an orbit camera).</summary>
    public interface IDragRotateTarget
    {
        void Hold(bool held);
        void Drag(float dxPixels, float dyPixels);
    }
}
