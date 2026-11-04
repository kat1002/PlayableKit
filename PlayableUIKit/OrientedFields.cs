using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableUIKit
{
    // Non-generic serialized Hor/Ver pairs. Luna 7.2 cannot deserialize generic serializable
    // types — every [SerializeField] Oriented<T> field is silently dropped from the build (null at
    // runtime). Serialize one of these instead; each converts implicitly to the runtime-only
    // Oriented<T> that CtaPresenter/CountdownTimer/BaseUIController take. Field names stay
    // "hor"/"ver" so scenes authored against Oriented<T> keep their references.

    [System.Serializable]
    public sealed class OrientedGameObject
    {
        [SerializeField] private GameObject hor;
        [SerializeField] private GameObject ver;

        public GameObject Hor => hor;
        public GameObject Ver => ver;

        public GameObject ForOrientation(UIOrientation orientation) => orientation == UIOrientation.Landscape ? hor : ver;

        /// <summary>Sets both Hor and Ver active/inactive together.</summary>
        public void SetActive(bool active)
        {
            // Unity == null, not ?. - unassigned serialized fields are "fake null" in the Editor.
            if (hor != null)
            {
                hor.SetActive(active);
            }
            if (ver != null)
            {
                ver.SetActive(active);
            }
        }

        public static implicit operator Oriented<GameObject>(OrientedGameObject o)
        {
            return o == null ? default : new Oriented<GameObject> { Hor = o.hor, Ver = o.ver };
        }
    }

    [System.Serializable]
    public sealed class OrientedButton
    {
        [SerializeField] private Button hor;
        [SerializeField] private Button ver;

        public Button Hor => hor;
        public Button Ver => ver;

        public Button ForOrientation(UIOrientation orientation) => orientation == UIOrientation.Landscape ? hor : ver;

        public Button GetActiveInHierarchy() => OrientedFieldUtil.ActiveInHierarchy(hor, ver);

        public static implicit operator Oriented<Button>(OrientedButton o)
        {
            return o == null ? default : new Oriented<Button> { Hor = o.hor, Ver = o.ver };
        }
    }

    [System.Serializable]
    public sealed class OrientedText
    {
        [SerializeField] private TMP_Text hor;
        [SerializeField] private TMP_Text ver;

        public TMP_Text Hor => hor;
        public TMP_Text Ver => ver;

        public TMP_Text ForOrientation(UIOrientation orientation) => orientation == UIOrientation.Landscape ? hor : ver;

        public TMP_Text GetActiveInHierarchy() => OrientedFieldUtil.ActiveInHierarchy(hor, ver);

        /// <summary>Writes the same text to both Hor and Ver.</summary>
        public void SetText(string text)
        {
            ((Oriented<TMP_Text>)this).SetText(text);
        }

        public static implicit operator Oriented<TMP_Text>(OrientedText o)
        {
            return o == null ? default : new Oriented<TMP_Text> { Hor = o.hor, Ver = o.ver };
        }
    }

    public static class OrientedFieldUtil
    {
        /// <summary>Whichever of Hor/Ver is currently active in the hierarchy, or null.</summary>
        public static T ActiveInHierarchy<T>(T hor, T ver) where T : Component
        {
            if (hor != null && hor.gameObject.activeInHierarchy)
            {
                return hor;
            }

            if (ver != null && ver.gameObject.activeInHierarchy)
            {
                return ver;
            }

            return null;
        }
    }
}
