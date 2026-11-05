using PlayableUIKit;
using UnityEngine;

namespace Shared.UI.Adapters
{
    /// <summary>
    /// Project-specific adapter: wraps this repo's <see cref="LunaManager"/> (Luna/PlayWorks SDK)
    /// as a <see cref="PlayableUIKit.IStoreGateway"/>, so <see cref="PlayableUIKit.BaseUIController"/>
    /// subclasses never reference <see cref="LunaManager"/> directly. Falls back to
    /// <see cref="LunaManager.instance"/> when no manager is assigned in the Inspector, matching
    /// the fallback pattern already used project-wide.
    /// </summary>
    public sealed class LunaStoreGateway : MonoBehaviour, IStoreGateway
    {
        [SerializeField] private LunaManager lunaManager;

        private LunaManager Manager => lunaManager != null ? lunaManager : LunaManager.instance;

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
