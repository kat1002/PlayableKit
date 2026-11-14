using UnityEngine;
using UnityEngine.UI;

namespace PlayableKit.Effects
{
    /// <summary>
    /// Progress-bar fill revealed by the UI_ProgressFill shader: the fill keeps its full size and Progress (0..1) masks
    /// it from the left, so the sprite never squashes. Also feeds the fill's pixel size and sprite UV rect to the shader
    /// (animated stripes, gloss, leading-edge glow).
    /// Runs in edit mode so the Progress slider previews the fill in the Inspector. In play mode the material is cloned
    /// in Awake so the shared asset is untouched. Update-driven, no tweens (Luna-safe).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    public sealed class PAUIProgressFillFX : MonoBehaviour
    {
        private static readonly int FillSizeId = Shader.PropertyToID("_FillSize");
        private static readonly int UVRectId = Shader.PropertyToID("_UVRect");
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");

        [SerializeField, Range(0f, 1f)] private float progress = 1f;

        private Image _image;
        private Material _mat;
        private RectTransform _rt;
        private Vector2 _lastSize = new Vector2(-1f, -1f);

        public float Progress
        {
            get { return progress; }
            set
            {
                progress = Mathf.Clamp01(value);
                ApplyProgress();
            }
        }

        private void Awake()
        {
            _image = GetComponent<Image>();
            _rt = (RectTransform)transform;

            if (_image.material == null || !_image.material.HasProperty(FillSizeId))
            {
                PALog.Warn("PAUIProgressFillFX", "Material has no _FillSize property, shader FX off.", this);
                return;
            }

            // edit mode previews on the shared material; play mode gets its own copy
            _mat = Application.isPlaying ? new Material(_image.material) : _image.material;
            if (Application.isPlaying)
            {
                _mat.name += " (Instance)";
                _image.material = _mat;
            }

            // Shader samples the sprite inside an atlas, so it needs the sprite's UV rect.
            // Computed from textureRect rather than Sprites.DataUtility, which Luna may not implement.
            var sprite = _image.sprite;
            if (sprite != null && sprite.texture != null)
            {
                var r = sprite.textureRect;
                float w = sprite.texture.width, h = sprite.texture.height;
                _mat.SetVector(UVRectId, new Vector4(r.x / w, r.y / h, r.width / w, r.height / h));
            }
            else
            {
                _mat.SetVector(UVRectId, new Vector4(0f, 0f, 1f, 1f));
            }
            ApplyProgress();
        }

        private void OnValidate()
        {
            ApplyProgress();
        }

        private void ApplyProgress()
        {
            // OnValidate can run before Awake in edit mode: preview on the shared material
            if (_mat == null && !Application.isPlaying)
            {
                var img = GetComponent<Image>();
                if (img != null && img.material != null && img.material.HasProperty(ProgressId)) _mat = img.material;
                _image = img;
            }
            if (_mat == null) return;
            _mat.SetFloat(ProgressId, progress);
            _image.SetMaterialDirty();
        }

        private void LateUpdate()
        {
            if (_mat == null)
            {
                return;
            }

            // Awake doesn't rerun after a domain reload, while OnValidate may have picked up the material
            if (_rt == null) _rt = (RectTransform)transform;
            Vector2 size = _rt.rect.size;
            if (size == _lastSize)
            {
                return;
            }

            _lastSize = size;
            _mat.SetVector(FillSizeId, new Vector4(Mathf.Max(size.x, 1f), Mathf.Max(size.y, 1f), 0f, 0f));
            _image.SetMaterialDirty();
        }

        private void OnDestroy()
        {
            if (_mat != null && Application.isPlaying)
            {
                Destroy(_mat);
            }
        }
    }
}
