using UnityEngine;
using UnityEngine.UI;

namespace PlayableKit.UI
{
    /// <summary>
    /// Repeating pattern background built from plain <c>Simple</c> Images. <c>Image.Type.Tiled</c>,
    /// <c>SpriteDrawMode.Tiled</c> and Repeat wrap do not repeat in a Luna build, so the pattern is a grid of cloned
    /// Images that covers this RectTransform.
    /// <para>
    /// Luna only clones scene objects, so the tile template must be an inactive Image child of this object
    /// (Simple type, sprite assigned, RectTransform size = one tile in canvas units). The grid is rebuilt whenever the
    /// rect size changes (polled in <c>Update</c>: no reliance on rect-change callbacks). Each tile overlaps its
    /// right / bottom neighbour by the overlap value to hide seams.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class PATiledImageBackground : MonoBehaviour
    {
        // Field names are serialized; keep them stable (scenes reference them).
        [SerializeField, Tooltip("Inactive Simple Image child; its RectTransform size is the tile size.")]
        private Image m_TileTemplate;

        [SerializeField, Tooltip("Extra size (canvas units) added to each tile to hide seams.")]
        private float m_Overlap = 1f;

        private RectTransform rect;
        private Image[] tiles = new Image[0];
        private int used;
        private Vector2 builtSize = new Vector2(-1f, -1f);

        /// <summary>Number of tiles needed to cover <paramref name="area"/> with tiles of size <paramref name="tile"/>.</summary>
        public static int TileCount(float area, float tile)
        {
            if (area <= 0f || tile <= 0f) return 0;
            return Mathf.CeilToInt(area / tile);
        }

        private void Awake()
        {
            rect = (RectTransform)transform;
            if (m_TileTemplate != null) m_TileTemplate.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (m_TileTemplate == null) return;
            Vector2 size = rect.rect.size;
            if (size == builtSize) return;
            builtSize = size;
            Rebuild(size);
        }

        private void Rebuild(Vector2 size)
        {
            Vector2 tile = m_TileTemplate.rectTransform.rect.size;
            int cols = TileCount(size.x, tile.x);
            int rows = TileCount(size.y, tile.y);
            int needed = cols * rows;

            if (needed > tiles.Length)
            {
                Image[] grown = new Image[needed];
                for (int i = 0; i < tiles.Length; i++) grown[i] = tiles[i];
                for (int i = tiles.Length; i < needed; i++) grown[i] = CreateTile();
                tiles = grown;
            }

            // Child anchors sit on this rect's centre, so the layout ignores this rect's pivot.
            float left = -size.x * 0.5f;
            float top = size.y * 0.5f;
            Vector2 tileSize = new Vector2(tile.x + m_Overlap, tile.y + m_Overlap);
            int n = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    Image img = tiles[n++];
                    img.gameObject.SetActive(true);
                    RectTransform rt = img.rectTransform;
                    rt.sizeDelta = tileSize;
                    rt.anchoredPosition = new Vector2(left + c * tile.x, top - r * tile.y);
                }
            }
            for (int i = n; i < used; i++) tiles[i].gameObject.SetActive(false);
            used = n;
        }

        private Image CreateTile()
        {
            GameObject go = Instantiate(m_TileTemplate.gameObject, transform);
            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;
            RectTransform rt = img.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.localScale = Vector3.one;
            return img;
        }
    }
}
