using TMPro;
using UnityEngine;

namespace PlayableUIKit
{
    /// <summary>
    /// The same Hor/Ver helpers <see cref="BaseUIController"/> exposes to its subclasses
    /// (<c>SetActive</c>/<c>GetActiveInHierarchy</c>), as extension methods so any plain
    /// <see cref="MonoBehaviour"/> — not just <see cref="BaseUIController"/> subclasses — can use
    /// them too, e.g. <c>myPanel.SetActive(true);</c>.
    /// </summary>
    public static class OrientedExtensions
    {
        /// <summary>Sets both the Hor and Ver GameObject active/inactive together — only one is
        /// ever actually visible at a time, so this is simply "broadcast to both".</summary>
        public static void SetActive(this Oriented<GameObject> item, bool active)
        {
            // Unity == null, not ?. - unassigned serialized fields are "fake null" objects in
            // the Editor that ?. doesn't catch, so SetActive on them threw.
            if (item.Hor != null)
            {
                item.Hor.SetActive(active);
            }
            if (item.Ver != null)
            {
                item.Ver.SetActive(active);
            }
        }

        /// <summary>Writes the same text to both Hor and Ver.</summary>
        public static void SetText(this Oriented<TMP_Text> item, string text)
        {
            if (item.Hor != null)
            {
                item.Hor.text = text;
            }
            if (item.Ver != null)
            {
                item.Ver.text = text;
            }
        }

        /// <summary>Picks whichever of Hor/Ver is currently active in the hierarchy.</summary>
        public static T GetActiveInHierarchy<T>(this Oriented<T> item) where T : Component
        {
            if (item.Hor != null && item.Hor.gameObject.activeInHierarchy)
            {
                return item.Hor;
            }

            if (item.Ver != null && item.Ver.gameObject.activeInHierarchy)
            {
                return item.Ver;
            }

            return null;
        }
    }
}
