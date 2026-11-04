using UnityEngine.Video;

public static class VideoPlayerLoopUtility
{
    public static void ApplyConfiguredLoop(VideoPlayer player, bool shouldLoop)
    {
        if (player == null) return;
        player.isLooping = shouldLoop;
    }

    public static void RestartIfLooping(VideoPlayer player, bool shouldLoop)
    {
        if (player == null || !shouldLoop) return;

        if (!player.isPrepared && player.clip == null && string.IsNullOrEmpty(player.url)) return;

        try
        {
            player.time = 0d;
        }
        catch
        {
            // Some runtimes can reject direct seeks until the player fully settles.
        }

        player.Play();
    }
}
