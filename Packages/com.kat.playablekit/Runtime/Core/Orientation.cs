using UnityEngine;

namespace PlayableKit
{
    /// <summary>
    /// Single source of truth for portrait vs. landscape. The ad webview loads at a fixed
    /// aspect and never rotates live, so a one-time Screen.width/height check at Awake is
    /// enough — there's no MRAID/resize signal to hook in this project.
    /// </summary>
    public static class Orientation
    {
        public static bool IsLandscape => Screen.width > Screen.height;
    }
}
