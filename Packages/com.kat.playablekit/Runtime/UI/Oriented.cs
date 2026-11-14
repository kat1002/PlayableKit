using UnityEngine;

namespace PlayableKit.UI
{
    /// <summary>
    /// Which layout root is currently live. Mirrors the "Hor"/"Ver" naming this project's scenes
    /// already use (see PAScreenOrientationChecker's Landscape/Vertical split) without depending on
    /// any project-specific type.
    /// </summary>
    public enum UIOrientation
    {
        Landscape,
        Portrait
    }

    /// <summary>
    /// A UI element authored twice — once for landscape, once for portrait — as a single typed
    /// field instead of an index-convention array (index 0 = Hor, 1 = Ver). Shows as two labeled
    /// "Hor"/"Ver" slots in the Inspector.
    ///
    /// RUNTIME USE ONLY — never declare a [SerializeField] Oriented&lt;T&gt;. Luna 7.2 cannot
    /// deserialize generic serializable types, so such a field is silently null in the built
    /// playable. Serialize the non-generic wrappers in OrientedFields.cs instead (they convert
    /// implicitly to this type).
    /// </summary>
    [System.Serializable]
    public struct Oriented<T>
    {
        [SerializeField] private T hor;
        [SerializeField] private T ver;

        public T Hor
        {
            get => hor;
            set => hor = value;
        }

        public T Ver
        {
            get => ver;
            set => ver = value;
        }

        public T ForOrientation(UIOrientation orientation) => orientation == UIOrientation.Landscape ? hor : ver;
    }
}
