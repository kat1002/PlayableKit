using Playable.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableKit
{
    public enum UIPathTwinkleShape
    {
        Arc = 0,
        Bezier = 1,
        ChildPoints = 2
    }

    public enum UIPathTwinkleDirection
    {
        Forward = 0,
        Backward = 1,
        PingPong = 2
    }

    /// <summary>
    /// Sparkle wave along a curved path (arc, cubic Bezier, or the "P*" child points you drag in the Scene view).
    /// Duplicates an inactive template Image child count times in Awake, places the stars evenly along the curve
    /// (arc-length lookup table) and pops them in one after another, then pops them out and loops. Positions are in
    /// this rect's local units, origin at the rect centre (stars use centre anchors/pivot). Every star's state is derived
    /// from the cycle time each frame, so there is no per-star state machine. Update-driven, Luna-safe, no tween chains.
    /// </summary>
    public sealed class UIPathTwinkle : MonoBehaviour
    {
        [Tooltip("Inactive child Image used as the star template (sprite/material). Duplicated once in Awake.")]
        [SerializeField] private Image template;

        [Header("Path")]
        [SerializeField] private UIPathTwinkleShape shape = UIPathTwinkleShape.Arc;
        [Tooltip("Arc: ellipse centre in local units (origin = rect centre).")]
        [SerializeField] private Vector2 arcCenter = new Vector2(0f, -120f);
        [Tooltip("Arc: ellipse radius X / Y in local units.")]
        [SerializeField] private Vector2 arcRadius = new Vector2(330f, 325f);
        [Tooltip("Arc: start angle in degrees (0 = right, 90 = up).")]
        [SerializeField] private float arcStartAngle = 165f;
        [Tooltip("Arc: end angle in degrees (0 = right, 90 = up).")]
        [SerializeField] private float arcEndAngle = 15f;
        [Header("Bezier (always 4 control points, not the P* children)")]
        [Tooltip("Bezier: start point, local units (origin = rect centre).")]
        [SerializeField] private Vector2 p0 = new Vector2(-300f, 0f);
        [Tooltip("Bezier: control handle pulling the curve near the start.")]
        [SerializeField] private Vector2 p1 = new Vector2(-150f, 220f);
        [Tooltip("Bezier: control handle pulling the curve near the end.")]
        [SerializeField] private Vector2 p2 = new Vector2(150f, 220f);
        [Tooltip("Bezier: end point.")]
        [SerializeField] private Vector2 p3 = new Vector2(300f, 0f);
        [Header("Child Points (any number)")]
        [Tooltip("ChildPoints: the path, in order. Drag the point objects in here. Empty = auto: active children of Points Root named 'P*' in sibling order.")]
        [SerializeField] private RectTransform[] pathPoints = new RectTransform[0];
        [Tooltip("ChildPoints (auto mode): parent of the 'P*' points. Empty = this transform.")]
        [SerializeField] private RectTransform pointsRoot;
        [Tooltip("ChildPoints: join the last point back to the first.")]
        [SerializeField] private bool closedLoop = false;

        [Header("Stars")]
        [SerializeField, Range(1, 32)] private int count = 9;
        [SerializeField] private Vector2 sizeRange = new Vector2(36f, 64f);
        [Tooltip("Random shift along the path, as a fraction of the gap between two stars.")]
        [SerializeField, Range(0f, 0.5f)] private float alongJitter = 0.1f;
        [Tooltip("Random shift sideways from the path, in local units.")]
        [SerializeField] private float sideJitter = 8f;
        [Tooltip("Roll new sizes / jitter / tints every cycle.")]
        [SerializeField] private bool rerollEachCycle = true;
        [SerializeField] private Color colorA = Color.white;
        [SerializeField] private Color colorB = new Color(1f, 0.95f, 0.6f, 1f);

        [Header("Wave")]
        [SerializeField] private UIPathTwinkleDirection direction = UIPathTwinkleDirection.Forward;
        [Tooltip("Delay between two neighbouring stars popping in.")]
        [SerializeField, Min(0f)] private float stagger = 0.08f;
        [SerializeField, Min(0.01f)] private float popInDuration = 0.22f;
        [SerializeField, Min(0f)] private float holdDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float popOutDuration = 0.3f;
        [Tooltip("On = every star stays until the whole path is filled, then they pop out in the same order.")]
        [SerializeField] private bool trail = false;
        [Tooltip("Pause after the wave ends before the next one.")]
        [SerializeField, Min(0f)] private float loopDelay = 0.8f;
        [Tooltip("Delay before the first wave.")]
        [SerializeField, Min(0f)] private float startDelay = 0.2f;
        [SerializeField] private float spinDegrees = 90f;
        [SerializeField] private bool useUnscaledTime;

        [Header("Editor")]
        [Tooltip("Draw the path gizmo even when this object is not selected.")]
        [SerializeField] private bool alwaysShowGizmo = false;

        private const int LutSegments = 64;
        private const int MaxPoints = 32;

        private RectTransform _area;
        private RectTransform[] _stars;
        private Image[] _images;
        private float[] _u;
        private float[] _side;
        private float[] _rotZ;
        private Color[] _tint;

        private RectTransform[] _ptChildren;
        private Vector2[] _pts;
        private int _ptCount;
        private Vector2[] _lutPos;
        private float[] _lutLen;
        private float _pathLength;

        private float _time;
        private bool _reverse;

        private void Awake()
        {
            _area = (RectTransform)transform;
            AllocBuffers();

            if (template == null)
            {
                _stars = new RectTransform[0];
                _images = new Image[0];
                AllocState(0);
                return;
            }

            template.gameObject.SetActive(false);
            _stars = new RectTransform[count];
            _images = new Image[count];
            AllocState(count);

            for (var i = 0; i < count; i++)
            {
                var img = Instantiate(template, _area, false);
                img.name = "Star_" + i;
                img.raycastTarget = false;

                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.localScale = Vector3.zero;
                img.gameObject.SetActive(true);

                _stars[i] = rt;
                _images[i] = img;
            }
        }

        private void AllocBuffers()
        {
            if (_lutPos != null)
            {
                return;
            }

            _ptChildren = new RectTransform[MaxPoints];
            _pts = new Vector2[MaxPoints];
            _lutPos = new Vector2[LutSegments + 1];
            _lutLen = new float[LutSegments + 1];
        }

        private void AllocState(int n)
        {
            _u = new float[n];
            _side = new float[n];
            _rotZ = new float[n];
            _tint = new Color[n];
        }

        private void OnEnable()
        {
            if (_stars == null)
            {
                return;
            }

            _time = -startDelay;
            _reverse = direction == UIPathTwinkleDirection.Backward;
            BeginCycle();
            HideAll();
        }

        private void OnDisable()
        {
            if (_stars == null)
            {
                return;
            }

            HideAll();
        }

        private void HideAll()
        {
            for (var i = 0; i < _stars.Length; i++)
            {
                _stars[i].localScale = Vector3.zero;
            }
        }

        // Once per cycle: refresh child points, rebuild the length table, lay the stars out along it.
        private void BeginCycle()
        {
            RefreshPoints();
            BuildLut();

            var n = _stars.Length;
            for (var i = 0; i < n; i++)
            {
                var gap = n > 1 ? 1f / (n - 1) : 0f;
                var u = n > 1 ? i * gap : 0.5f;
                if (rerollEachCycle || _tint[i].a <= 0f)
                {
                    _u[i] = Mathf.Clamp01(u + Random.Range(-alongJitter, alongJitter) * gap);
                    _side[i] = Random.Range(-sideJitter, sideJitter);
                    _rotZ[i] = Random.Range(0f, 360f);
                    _tint[i] = Color.Lerp(colorA, colorB, Random.value);

                    var size = Random.Range(sizeRange.x, sizeRange.y);
                    _stars[i].sizeDelta = new Vector2(size, size);
                }
            }
        }

        // ChildPoints: use the pathPoints list if filled, else active children whose name starts with "P" in sibling order.
        private void RefreshPoints()
        {
            _ptCount = 0;
            if (shape != UIPathTwinkleShape.ChildPoints)
            {
                return;
            }

            var rectCenter = _area.rect.center;
            if (pathPoints != null && pathPoints.Length > 0)
            {
                for (var i = 0; i < pathPoints.Length && _ptCount < MaxPoints; i++)
                {
                    var p = pathPoints[i];
                    if (p == null || !p.gameObject.activeSelf)
                    {
                        continue;
                    }

                    _ptChildren[_ptCount] = p;
                    var lp = transform.InverseTransformPoint(p.position);
                    _pts[_ptCount] = new Vector2(lp.x, lp.y) - rectCenter;
                    _ptCount++;
                }

                return;
            }

            var root = pointsRoot != null ? (Transform)pointsRoot : transform;
            var n = root.childCount;
            for (var i = 0; i < n && _ptCount < MaxPoints; i++)
            {
                var child = root.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf || !child.name.StartsWith("P"))
                {
                    continue;
                }

                _ptChildren[_ptCount] = child;
                var local = transform.InverseTransformPoint(child.position);
                _pts[_ptCount] = new Vector2(local.x, local.y) - rectCenter;
                _ptCount++;
            }
        }

        // Raw curve point, t in 0..1 (not evenly spaced).
        private Vector2 EvalRaw(float t)
        {
            t = Mathf.Clamp01(t);

            if (shape == UIPathTwinkleShape.Bezier)
            {
                var m = 1f - t;
                return m * m * m * p0 + 3f * m * m * t * p1 + 3f * m * t * t * p2 + t * t * t * p3;
            }

            if (shape == UIPathTwinkleShape.ChildPoints && _ptCount > 0)
            {
                if (_ptCount == 1)
                {
                    return _pts[0];
                }

                if (_ptCount == 2 && !closedLoop)
                {
                    return Vector2.Lerp(_pts[0], _pts[1], t);
                }

                // Uniform Catmull-Rom; endpoints clamped, or wrapped when closed.
                var segs = closedLoop ? _ptCount : _ptCount - 1;
                var f = t * segs;
                var s = Mathf.Min((int)f, segs - 1);
                f -= s;
                var a = PointAt(s - 1);
                var b = PointAt(s);
                var c = PointAt(s + 1);
                var d = PointAt(s + 2);
                var f2 = f * f;
                var f3 = f2 * f;
                return 0.5f * (2f * b + (c - a) * f + (2f * a - 5f * b + 4f * c - d) * f2 + (3f * b - a - 3f * c + d) * f3);
            }

            // Arc (also the fallback for ChildPoints with no points).
            var ang = Mathf.Lerp(arcStartAngle, arcEndAngle, t) * Mathf.Deg2Rad;
            return arcCenter + new Vector2(Mathf.Cos(ang) * arcRadius.x, Mathf.Sin(ang) * arcRadius.y);
        }

        private Vector2 PointAt(int i)
        {
            if (closedLoop)
            {
                return _pts[((i % _ptCount) + _ptCount) % _ptCount];
            }

            return _pts[Mathf.Clamp(i, 0, _ptCount - 1)];
        }

        private void BuildLut()
        {
            var prev = EvalRaw(0f);
            _lutPos[0] = prev;
            _lutLen[0] = 0f;
            for (var i = 1; i <= LutSegments; i++)
            {
                var cur = EvalRaw((float)i / LutSegments);
                _lutPos[i] = cur;
                _lutLen[i] = _lutLen[i - 1] + Vector2.Distance(prev, cur);
                prev = cur;
            }

            _pathLength = _lutLen[LutSegments];
        }

        // Evenly spaced along the curve length: u in 0..1 -> position and unit tangent.
        private Vector2 EvalUniform(float u, out Vector2 tangent)
        {
            tangent = Vector2.right;
            if (_pathLength <= 0.0001f)
            {
                return _lutPos[0];
            }

            var target = Mathf.Clamp01(u) * _pathLength;
            var lo = 0;
            var hi = LutSegments;
            while (hi - lo > 1)
            {
                var mid = (lo + hi) >> 1;
                if (_lutLen[mid] <= target)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            var seg = _lutLen[hi] - _lutLen[lo];
            var k = seg > 0.0001f ? (target - _lutLen[lo]) / seg : 0f;
            var dir = _lutPos[hi] - _lutPos[lo];
            if (dir.sqrMagnitude > 0.000001f)
            {
                tangent = dir.normalized;
            }

            return Vector2.Lerp(_lutPos[lo], _lutPos[hi], k);
        }

        private float CycleLength()
        {
            var n = _stars.Length;
            var last = Mathf.Max(0, n - 1) * stagger;
            var end = trail
                ? last + popInDuration + holdDuration + last + popOutDuration
                : last + popInDuration + holdDuration + popOutDuration;
            return end + loopDelay;
        }

        private void Update()
        {
            if (_stars == null || _stars.Length == 0)
            {
                return;
            }

            var dt = Mathf.Min(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime, 0.1f);
            _time += dt;
            if (_time < 0f)
            {
                return;
            }

            var cycle = CycleLength();
            if (_time >= cycle)
            {
                _time -= cycle;
                if (direction == UIPathTwinkleDirection.PingPong)
                {
                    _reverse = !_reverse;
                }

                BeginCycle();
            }

            var n = _stars.Length;
            var lastStart = (n - 1) * stagger;
            for (var i = 0; i < n; i++)
            {
                var star = _stars[i];
                var k = _reverse ? n - 1 - i : i;
                var start = k * stagger;
                var popOutStart = trail
                    ? lastStart + popInDuration + holdDuration + k * stagger
                    : start + popInDuration + holdDuration;
                var life = popOutStart + popOutDuration - start;
                var t = _time - start;

                if (t < 0f || t >= life)
                {
                    star.localScale = Vector3.zero;
                    continue;
                }

                var tint = _tint[i];
                float scale;
                float alpha;
                var tOut = t - (popOutStart - start);
                if (t < popInDuration)
                {
                    scale = TweenLite.ApplyEase(Ease.OutBack, Mathf.Clamp01(t / popInDuration));
                    alpha = Mathf.Clamp01(t / (popInDuration * 0.8f));
                }
                else if (tOut <= 0f)
                {
                    scale = 1f;
                    alpha = 1f;
                }
                else
                {
                    var p = Mathf.Clamp01(tOut / popOutDuration);
                    scale = 1f - TweenLite.ApplyEase(Ease.InBack, p);
                    alpha = 1f - p;
                }

                // Re-evaluate while visible so a moved child point drags the sparkle with it (LUT is per cycle).
                Vector2 tangent;
                var pos = EvalUniform(_u[i], out tangent);
                star.anchoredPosition = pos + new Vector2(-tangent.y, tangent.x) * _side[i];
                star.localScale = Vector3.one * scale;
                star.localRotation = Quaternion.Euler(0f, 0f, _rotZ[i] + spinDegrees * Mathf.Clamp01(t / life));
                _images[i].color = new Color(tint.r, tint.g, tint.b, tint.a * alpha);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _area = (RectTransform)transform;
            AllocBuffers();
            RefreshPoints();
            BuildLut();
        }

        private Vector3 GizmoWorld(Vector2 v)
        {
            return transform.TransformPoint(new Vector3(v.x + _area.rect.center.x, v.y + _area.rect.center.y, 0f));
        }

        private void OnDrawGizmos()
        {
            if (alwaysShowGizmo)
            {
                DrawPathGizmo();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!alwaysShowGizmo)
            {
                DrawPathGizmo();
            }
        }

        private void DrawPathGizmo()
        {
            // Works in edit mode where Awake has not run; allocation is fine here (editor only).
            _area = (RectTransform)transform;
            AllocBuffers();
            RefreshPoints();
            BuildLut();

            var scale = Mathf.Max(0.0001f, transform.lossyScale.x);

            Gizmos.color = Color.yellow;
            var prev = GizmoWorld(_lutPos[0]);
            for (var i = 1; i <= LutSegments; i++)
            {
                var cur = GizmoWorld(_lutPos[i]);
                Gizmos.DrawLine(prev, cur);
                prev = cur;
            }

            var n = Mathf.Max(1, count);
            for (var i = 0; i < n; i++)
            {
                Vector2 tangent;
                var u = n > 1 ? (float)i / (n - 1) : 0.5f;
                Gizmos.DrawWireSphere(GizmoWorld(EvalUniform(u, out tangent)), 0.5f * (sizeRange.x + sizeRange.y) * 0.5f * scale);
            }

            if (shape == UIPathTwinkleShape.Bezier)
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(GizmoWorld(p0), GizmoWorld(p1));
                Gizmos.DrawLine(GizmoWorld(p3), GizmoWorld(p2));
                Gizmos.DrawWireSphere(GizmoWorld(p1), 6f * scale);
                Gizmos.DrawWireSphere(GizmoWorld(p2), 6f * scale);
            }
            else if (shape == UIPathTwinkleShape.ChildPoints)
            {
                Gizmos.color = Color.cyan;
                for (var i = 0; i < _ptCount; i++)
                {
                    Gizmos.DrawSphere(GizmoWorld(_pts[i]), 8f * scale);
                }
            }
        }
#endif
    }
}
