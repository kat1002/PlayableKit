using PlayableKit.UI;
using UnityEngine;

namespace PlayableKit
{
    /// <summary>
    /// Project-specific adapter: wraps this repo's <see cref="PALunaManager"/> (Luna/PlayWorks SDK)
    /// as a <see cref="PlayableKit.UI.IStoreGateway"/>, so <see cref="PlayableKit.UI.PABaseUIController"/>
    /// subclasses never reference <see cref="PALunaManager"/> directly. Falls back to
    /// <see cref="PALunaManager.instance"/> when no manager is assigned in the Inspector, matching
    /// the fallback pattern already used project-wide.
    /// </summary>
    public sealed class PALunaStoreGateway : MonoBehaviour, IStoreGateway
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
