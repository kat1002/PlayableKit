using System;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableKit.UI
{
    // Switches a uGUI layout between portrait and landscape. Each item copies the rect of an
    // authored slot onto its target; a slot is an empty RectTransform under the same parent as
    // the target. Do not list a target whose position or scale is animated.
    public sealed class PAOrientationLayout : MonoBehaviour
    {
        [Serializable]
        public sealed class Item
        {
            public RectTransform Target;
            public RectTransform Portrait;
            public RectTransform Landscape;
        }

        [SerializeField] private CanvasScaler m_Scaler;
        [SerializeField] private Vector2 m_PortraitReference = new Vector2(640f, 1136f);
        [SerializeField] private Vector2 m_LandscapeReference = new Vector2(1136f, 640f);
        [SerializeField] private Item[] m_Items = new Item[0];

        private bool m_HasApplied;
        private bool m_IsLandscape;

        private void OnEnable()
        {
            Apply(Orientation.IsLandscape);
        }

        private void Update()
        {
            bool isLandscape = Orientation.IsLandscape;
            if (!m_HasApplied || isLandscape != m_IsLandscape) Apply(isLandscape);
        }

        private void Apply(bool isLandscape)
        {
            m_HasApplied = true;
            m_IsLandscape = isLandscape;
            if (m_Scaler != null) m_Scaler.referenceResolution = isLandscape ? m_LandscapeReference : m_PortraitReference;

            for (int index = 0; index < m_Items.Length; index++)
            {
                Item item = m_Items[index];
                if (item == null || item.Target == null) continue;
                RectTransform slot = isLandscape ? item.Landscape : item.Portrait;
                if (slot == null) continue;
                item.Target.anchorMin = slot.anchorMin;
                item.Target.anchorMax = slot.anchorMax;
                item.Target.pivot = slot.pivot;
                item.Target.anchoredPosition = slot.anchoredPosition;
                item.Target.sizeDelta = slot.sizeDelta;
                item.Target.localScale = slot.localScale;
            }
        }
    }
}
