using UnityEngine;

namespace PlayableKit
{
    // Store-return rule: once the store has been opened, the playable stays interactive and the
    // next single player action opens the store again. Place one instance in the playable scene.
    public sealed class PAStoreReturn : MonoBehaviour
    {
        private bool m_IsPressed;

        private void Update()
        {
            PALunaManager manager = PALunaManager.Instance;
            if (manager == null || !manager.HasOpenedStore) return;

            // Count only a press that began after the store was opened, and act on its release
            // so a button under the pointer does not open the store a second time.
            if (Input.GetMouseButtonDown(0)) m_IsPressed = true;
            if (!m_IsPressed || !Input.GetMouseButtonUp(0)) return;
            m_IsPressed = false;
            manager.GoToStore();
        }
    }
}
