using UnityEngine;
using UnityEngine.UI;

namespace PlayableKit.UI
{
    /// <summary>
    /// Outline for a <see cref="Text"/> made of authored copies, because Luna leaves the
    /// <c>Outline</c> and <c>Shadow</c> mesh effects empty. The copies are children of the source
    /// text: offset copies in the outline color, then one copy in the text color on top. The source
    /// text stays the owner of the string and is not drawn itself.
    /// </summary>
    public class PATextOutline : MonoBehaviour
    {
        [SerializeField] private Text m_Source;
        [SerializeField] private Color m_OutlineColor = Color.black;
        [Tooltip("Outline thickness in the text's local UI units.")]
        [SerializeField, Min(0f)] private float m_Distance = 2f;
        [SerializeField] private Text[] m_Copies = new Text[0];

        private string m_ShownText;

        private void OnEnable()
        {
            SyncText();
        }

        private void LateUpdate()
        {
            if (m_Source != null && m_Source.text != m_ShownText) SyncText();
        }

        private void SyncText()
        {
            if (m_Source == null) return;
            m_ShownText = m_Source.text;
            foreach (Text copy in m_Copies)
                if (copy != null) copy.text = m_ShownText;
        }

    #if UNITY_EDITOR
        private static readonly Vector2[] Directions =
        {
            new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
            new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f),
            new Vector2(0.7071f, -0.7071f), new Vector2(-0.7071f, -0.7071f)
        };

        /// <summary>Recreates the copies from the source text. Run it after changing the text style.</summary>
        [ContextMenu("Rebuild Copies")]
        public void RebuildCopies()
        {
            if (m_Source == null) m_Source = GetComponent<Text>();
            if (m_Source == null) return;
            foreach (Text copy in m_Copies)
                if (copy != null) DestroyImmediate(copy.gameObject);

            m_Copies = new Text[Directions.Length + 1];
            for (int i = 0; i < Directions.Length; i++)
                m_Copies[i] = CreateCopy("Outline" + i, m_OutlineColor, Directions[i] * m_Distance);
            m_Copies[Directions.Length] = CreateCopy("Face", m_Source.color, Vector2.zero);
            m_Source.enabled = false;
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.EditorUtility.SetDirty(m_Source);
        }

        private Text CreateCopy(string copyName, Color color, Vector2 offset)
        {
            GameObject copyObject = new GameObject(copyName, typeof(RectTransform));
            copyObject.layer = gameObject.layer;
            RectTransform rect = (RectTransform)copyObject.transform;
            rect.SetParent(m_Source.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = offset;

            Text copy = copyObject.AddComponent<Text>();
            copy.font = m_Source.font;
            copy.fontSize = m_Source.fontSize;
            copy.fontStyle = m_Source.fontStyle;
            copy.alignment = m_Source.alignment;
            copy.alignByGeometry = m_Source.alignByGeometry;
            copy.supportRichText = m_Source.supportRichText;
            copy.horizontalOverflow = m_Source.horizontalOverflow;
            copy.verticalOverflow = m_Source.verticalOverflow;
            copy.lineSpacing = m_Source.lineSpacing;
            copy.resizeTextForBestFit = m_Source.resizeTextForBestFit;
            copy.resizeTextMinSize = m_Source.resizeTextMinSize;
            copy.resizeTextMaxSize = m_Source.resizeTextMaxSize;
            copy.color = color;
            copy.raycastTarget = false;
            copy.text = m_Source.text;
            return copy;
        }
    #endif
    }
}
