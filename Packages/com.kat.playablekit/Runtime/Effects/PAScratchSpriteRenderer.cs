using PlayableKit.Tweening;
using UnityEngine;
using UnityEngine.Events;

namespace PlayableKit.Effects
{
    public enum ScratchMode
    {
        Scratch,
        Reveal
    }

    public class PAScratchSpriteRenderer : MonoBehaviour
    {
        [Header("Mode")]
        public ScratchMode mode;

        [Header("Target")]
        public SpriteRenderer targetRenderer;
        public Transform target;

        [Header("Brush")]
        public Texture2D brushTexture;
        public float brushWorldSize = 0.3f;

        [Header("Auto Complete Fade")]
        public float fadeDuration = 0.25f;

        [Header("Progress")]
        [Range(0f, 1f)]
        public float completeThreshold = 0.7f;
        public UnityEvent onCompleted;

        // --- Variables tối ưu (Dùng Color32 thay vì Color để nhẹ gấp 4 lần) ---
        Texture2D runtimeTex;
        Color32[] originalPixels; // Màu gốc (Color32)
        Color32[] runtimePixels;  // Buffer (Color32)
        bool[] pixelStatus;

        // Brush Cache
        Color32[] brushPixels; // Cache brush dạng Color32
        int brushWidth;
        int brushHeight;

        // Performance Counters
        int totalValidPixels = 0;
        int currentDonePixels = 0;
        public bool isCompleted;

        Vector2Int lastPixelPos;
        bool hasLastPos;

        // --- Optimization Flags ---
        float applyCooldown = 0.033f; // Giới hạn update texture ~30 lần/giây (0.016f cho 60fps)
        float applyTimer;
        bool isTextureDirty = false;  // Đánh dấu có thay đổi để update

        public bool Active = false;

        void Start()
        {
            InitBrush();
            InitTexture();
        }
        public void Completed()
        {
            isCompleted = false;
        }

        // ================= INIT =================

        void InitBrush()
        {
            if (brushTexture == null) return;
            brushWidth = brushTexture.width;
            brushHeight = brushTexture.height;
            // Dùng GetPixels32 để lấy dữ liệu dạng byte
            brushPixels = brushTexture.GetPixels32();
        }

        void InitTexture()
        {
            Texture2D source = targetRenderer.sprite.texture;

            // Cache dữ liệu gốc dạng Color32
            originalPixels = source.GetPixels32();

            // Buffer runtime
            runtimePixels = new Color32[originalPixels.Length];
            pixelStatus = new bool[originalPixels.Length];

            totalValidPixels = 0;
            currentDonePixels = 0;

            Color32 transparent = new Color32(0, 0, 0, 0);

            for (int i = 0; i < originalPixels.Length; i++)
            {
                Color32 c = originalPixels[i];

                // Check alpha > 0 (trong Color32 alpha max là 255)
                if (c.a > 0)
                {
                    totalValidPixels++;
                }
                else
                {
                    pixelStatus[i] = true;
                }

                if (mode == ScratchMode.Scratch)
                {
                    runtimePixels[i] = c;
                }
                else
                {
                    // Mode Hiện: Bắt đầu bằng trong suốt
                    runtimePixels[i] = transparent;
                }
            }

            // Tạo Texture runtime
            runtimeTex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            runtimeTex.SetPixels32(runtimePixels);
            runtimeTex.Apply(false); // False = không tạo mipmap (quan trọng để mượt)

            targetRenderer.sprite = Sprite.Create(
                runtimeTex,
                new Rect(0, 0, runtimeTex.width, runtimeTex.height),
                new Vector2(0.5f, 0.5f),
                targetRenderer.sprite.pixelsPerUnit
            );
        }

        // ================= UPDATE =================

        void Update()
        {
            if (!Active) return;
            if (isCompleted) return;

            // 1. Xử lý Input và vẽ lên RAM (rất nhanh)
            HandleInput();

            // 2. Chỉ đẩy dữ liệu lên GPU theo nhịp (Throttle)
            UpdateTextureToGPU();
        }

        void HandleInput()
        {
            if (!Input.GetMouseButton(0))
            {
                hasLastPos = false; // Reset nét vẽ khi nhả chuột
                return;
            }

            Vector3 worldPos = target.position;
            //Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            worldPos.z = 0;

            if (!targetRenderer.bounds.Contains(worldPos)) return;

            Vector2Int currentPixel = WorldToPixel(worldPos);
            bool drawn = false;

            if (!hasLastPos)
            {
                drawn = DrawBrush(currentPixel);
                lastPixelPos = currentPixel;
                hasLastPos = true;
            }
            else
            {
                drawn = DrawLine(lastPixelPos, currentPixel);
                lastPixelPos = currentPixel;
            }

            if (drawn)
            {
                isTextureDirty = true; // Đánh dấu là cần update
            }
        }

        void UpdateTextureToGPU()
        {
            if (!isTextureDirty) return;

            applyTimer -= Time.deltaTime;
            if (applyTimer <= 0)
            {
                // SetPixels32 nhanh hơn SetPixels thường
                runtimeTex.SetPixels32(runtimePixels);

                // Apply(false) -> KHÔNG tính toán lại Mipmaps (Siêu quan trọng để giảm lag)
                runtimeTex.Apply(false);

                CheckProgress();

                applyTimer = applyCooldown;
                isTextureDirty = false;
            }
        }

        bool DrawLine(Vector2Int from, Vector2Int to)
        {
            bool changed = false;
            int distance = Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y));

            if (distance == 0) return DrawBrush(from);

            for (int i = 0; i <= distance; i++)
            {
                float t = (float)i / distance;
                int x = Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t));

                if (DrawBrush(new Vector2Int(x, y)))
                    changed = true;
            }
            return changed;
        }

        // ================= CORE LOGIC =================

        Vector2Int WorldToPixel(Vector3 worldPos)
        {
            Vector3 local = targetRenderer.transform.InverseTransformPoint(worldPos);
            Bounds b = targetRenderer.sprite.bounds;

            float u = (local.x - b.min.x) / b.size.x;
            float v = (local.y - b.min.y) / b.size.y;

            int x = Mathf.RoundToInt(u * runtimeTex.width);
            int y = Mathf.RoundToInt(v * runtimeTex.height);

            return new Vector2Int(x, y);
        }

        bool DrawBrush(Vector2Int center)
        {
            bool changed = false;
            int brushPixelSize = GetBrushPixelSize();
            int texW = runtimeTex.width;
            int texH = runtimeTex.height;

            // Define vùng ảnh hưởng để tránh loop thừa
            // Thay vì loop qua toàn bộ brush rồi check if, ta tính toán bounds trước

            // Loop qua vùng brush
            for (int by = 0; by < brushHeight; by++)
            {
                for (int bx = 0; bx < brushWidth; bx++)
                {
                    // Check alpha của brush (Color32 dùng byte 0-255, 25 ~ 0.1f)
                    byte alpha = brushPixels[by * brushWidth + bx].a;
                    if (alpha < 25) continue;

                    float u = (float)bx / brushWidth - 0.5f;
                    float v = (float)by / brushHeight - 0.5f;

                    int px = center.x + Mathf.RoundToInt(u * brushPixelSize);
                    int py = center.y + Mathf.RoundToInt(v * brushPixelSize);

                    // Check bounds nhanh
                    if (px < 0 || px >= texW || py < 0 || py >= texH) continue;

                    int index = py * texW + px;

                    if (pixelStatus[index]) continue;

                    if (mode == ScratchMode.Scratch)
                    {
                        runtimePixels[index] = new Color32(0, 0, 0, 0);
                    }
                    else
                    {
                        runtimePixels[index] = originalPixels[index];
                    }

                    pixelStatus[index] = true;
                    currentDonePixels++;
                    changed = true;
                }
            }
            return changed;
        }

        int GetBrushPixelSize()
        {
            float ppu = targetRenderer.sprite.pixelsPerUnit;
            float scale = targetRenderer.transform.lossyScale.x;
            return Mathf.Max(1, Mathf.RoundToInt(brushWorldSize * ppu * scale));
        }

        // ================= PROGRESS =================

        void CheckProgress()
        {
            if (isCompleted || totalValidPixels == 0) return;

            float progress = (float)currentDonePixels / totalValidPixels;

            if (progress >= completeThreshold)
            {
                isCompleted = true;
                AutoComplete();
            }
        }

        public float GetProgress()
        {
            return (float)currentDonePixels / totalValidPixels;
        }

        // ================= AUTO COMPLETE & FX =================

        void AutoComplete()
        {
            targetRenderer.DOKill();

            if (mode == ScratchMode.Scratch)
            {
                targetRenderer.DOFade(0f, fadeDuration)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        ApplyFinalTexture();
                        onCompleted?.Invoke();
                       /// SetProgress(1);

                    });
            }
            else
            {
                StartCoroutine(RunRevealEffect());
            }
        }
        public float duration;
        public void SetUpTime()
        {
            targetRenderer.DOFade(0f, duration)
                   .SetEase(Ease.OutQuad);
        }

        System.Collections.IEnumerator RunRevealEffect()
        {
            // 1. Tạo Overlay Object
            GameObject overlayObj = new GameObject("RevealOverlay");
            overlayObj.transform.SetParent(targetRenderer.transform);
            overlayObj.transform.localPosition = Vector3.zero;
            overlayObj.transform.localRotation = Quaternion.identity;
            overlayObj.transform.localScale = Vector3.one;

            SpriteRenderer overlaySR = overlayObj.AddComponent<SpriteRenderer>();

            // [FIX 1] Copy Material của thằng gốc sang để màu sắc đồng nhất 100%
            overlaySR.sharedMaterial = targetRenderer.sharedMaterial;

            // Setup Layer cao hơn để đè lên
            overlaySR.sortingLayerID = targetRenderer.sortingLayerID;
            overlaySR.sortingOrder = targetRenderer.sortingOrder + 1;

            // 2. Tạo Texture Full từ dữ liệu gốc
            Texture2D fullTex = new Texture2D(runtimeTex.width, runtimeTex.height, TextureFormat.RGBA32, false);
            fullTex.SetPixels32(originalPixels);
            fullTex.Apply(false);

            overlaySR.sprite = Sprite.Create(
                fullTex,
                new Rect(0, 0, fullTex.width, fullTex.height),
                new Vector2(0.5f, 0.5f),
                targetRenderer.sprite.pixelsPerUnit
            );

            // 3. Fade In Overlay (Từ từ hiện cái ảnh full lên đè cái cũ)
            overlaySR.color = new Color(1, 1, 1, 0);
            Tween t = overlaySR.DOFade(1f, fadeDuration).SetEase(Ease.OutQuad);

            // Chờ Fade xong
            yield return t.WaitForCompletion();

            // 4. Lúc này Overlay đang che kín mít -> Ta âm thầm sửa cái ảnh gốc bên dưới cho full
            ApplyFinalTexture();

            // [FIX 2] Chờ hết frame này để đảm bảo Texture dưới GPU đã cập nhật xong
            // Nếu không có dòng này, xóa overlay ngay có thể bị nháy 1 frame
            yield return null;

            // 5. Sự kiện hoàn thành
            onCompleted?.Invoke();
      //      SetProgress(1); // (Nếu bạn có dùng hàm này ở class cha)


            // 6. Dọn dẹp (Lúc này xóa Overlay đi thì người chơi cũng không biết vì ảnh dưới đã y hệt rồi)
            Destroy(overlayObj);
            Destroy(fullTex);
        }

        void ApplyFinalTexture()
        {
            Color32 transparent = new Color32(0, 0, 0, 0);
            for (int i = 0; i < runtimePixels.Length; i++)
            {
                if (originalPixels[i].a == 0) continue;

                if (mode == ScratchMode.Reveal)
                    runtimePixels[i] = originalPixels[i];
                else
                    runtimePixels[i] = transparent;
            }

            runtimeTex.SetPixels32(runtimePixels);
            runtimeTex.Apply(false);
        }
    }
}
