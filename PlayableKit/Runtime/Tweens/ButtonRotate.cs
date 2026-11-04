using UnityEngine;
using Playable.Tweening;

namespace PlayableKit
{
    public class ButtonRotate : MonoBehaviour
    {
        void Start()
        {
            transform.DORotate(new Vector3(0, 0, 360), 1f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart);
        }
    }
}
