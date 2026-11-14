namespace PlayableKit.Effects
{
    /// <summary>Receives wind intensity/kick from PAFallWindFx (e.g. a camera shaker).</summary>
    public interface IWindFxReceiver
    {
        void SetWindIntensity(float intensity);
        void Kick(float amplitude, float duration);
    }
}
