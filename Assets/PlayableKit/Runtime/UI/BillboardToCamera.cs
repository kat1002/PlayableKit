using UnityEngine;

namespace PlayableKit
{
    public class BillboardToCamera : MonoBehaviour
    {
        private Camera _cam;

        void Awake()
        {
            _cam = Camera.main;
        }

        void LateUpdate()
        {
            if (_cam == null)
                return;

            transform.forward = _cam.transform.forward;
        }
    }
}
