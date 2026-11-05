namespace PlayableKit
{
    /// <summary>Receives wind intensity/kick from FallWindFx (e.g. a camera shaker).</summary>
    public interface IWindFxReceiver
    {
        void SetWindIntensity(float intensity);
        void Kick(float amplitude, float duration);
    }
}
