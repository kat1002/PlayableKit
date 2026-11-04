using System.Collections;
using UnityEngine;

// Playable perf helper. Some Spine props (e.g. the chair "wire") are driven by a
// SkeletonUtility that spawns dozens of SkeletonUtilityBone transforms, each
// syncing a bone -> Transform every frame for a looping idle animation nobody
// looks at during a 30s ad. This lets the rig play for a few frames so it settles
// into pose, then disables the listed Behaviours so it costs nothing for the rest
// of the session. The mesh / sprites keep their last pose.
//
// Wire: put this on the Spine rig root, drag its SkeletonUtility and
// SkeletonAnimation (and any SkeletonUtilityBone roots, if they update
// independently) into `toDisable`.
[DisallowMultipleComponent]
public class FreezeSpineRigAfterWarmup : MonoBehaviour
{
    [Tooltip("Frames to let the rig animate before freezing it.")]
    [SerializeField] int warmupFrames = 3;

    [Tooltip("Components disabled after the warmup (SkeletonUtility, SkeletonAnimation, ...).")]
    [SerializeField] Behaviour[] toDisable;

    IEnumerator Start()
    {
        for (int i = 0; i < warmupFrames; i++)
            yield return null;

        if (toDisable != null)
        {
            for (int i = 0; i < toDisable.Length; i++)
                if (toDisable[i] != null)
                    toDisable[i].enabled = false;
        }

        enabled = false;
    }
}
