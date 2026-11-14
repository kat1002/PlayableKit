using UnityEngine;

namespace PlayableKit
{
    /// <summary>
    /// Button OnClick target for "go to store": forwards to <see cref="PALunaManager.GoToStore"/>
    /// so a Button can call it without referencing <see cref="PALunaManager"/> directly. Falls back to
    /// <see cref="PALunaManager.instance"/> when no manager is assigned in the Inspector, matching
    /// the fallback pattern already used project-wide.
    /// </summary>
    public sealed class PALunaStoreGateway : MonoBehaviour
    {
        [SerializeField] private PALunaManager lunaManager;

        private PALunaManager Manager => lunaManager != null ? lunaManager : PALunaManager.instance;

        public void GoToStore()
        {
            // Unity == null, not ?. (fake-null objects slip past ?.).
            var manager = Manager;
            if (manager != null)
            {
                manager.GoToStore();
            }
        }
    }
}
