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

        if (player.canSetTime)
        {
            player.time = 0d;
        }

        player.Play();
    }
}
