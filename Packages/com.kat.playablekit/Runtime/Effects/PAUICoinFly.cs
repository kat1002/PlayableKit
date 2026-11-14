using System;
using PlayableKit.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PlayableKit.Effects
{
    /// <summary>
    /// Coins (or any icon) burst out of a source, scatter, hold briefly, then fly along a curve to a target
    /// such as a currency counter. Put it on a container RectTransform on the canvas; the template
    /// Image is an inactive child that gets cloned into a small pool. Update-driven, no tweens or closures.
    /// </summary>
    public sealed class PAUICoinFly : MonoBehaviour
    {
        [Tooltip("Inactive child Image cloned for every flying item.")]
        [SerializeField] private Image m_Template;
        [Tooltip("Where the items fly to when Play() / Play(from) is used.")]
        [SerializeField] private RectTransform m_Target;
        [SerializeField, Min(1)] private int m_Count = 8;

        [Header("Timing")]
        [SerializeField] private float m_ScatterRadius = 100f;
        [SerializeField, Min(0.01f)] private float m_ScatterTime = 0.3f;
        [SerializeField, Min(0f)] private float m_Hold = 0.25f;
        [SerializeField, Min(0.01f)] private float m_FlyTime = 0.5f;
        [SerializeField, Min(0f)] private float m_Stagger = 0.04f;
        [SerializeField] private bool m_UseUnscaledTime = true;

        [Header("Events")]
        [SerializeField] private UnityEvent m_OnItemArrived;
        [SerializeField] private UnityEvent m_OnAllArrived;

        private RectTransform _container;
        private Image[] _items = new Image[0];
        private Vector2[] _start = new Vector2[0];
        private Vector2[] _scatter = new Vector2[0];
        private Vector2[] _control = new Vector2[0];
        private Vector2[] _target = new Vector2[0];
        private float[] _startTime = new float[0];
        private bool[] _active = new bool[0];
        private int _remaining;

        private void Awake()
        {
            _container = (RectTransform)transform;
        }

        private void OnDisable()
        {
            for (int i = 0; i < _items.Length; i++)
            {
                _active[i] = false;
                if (_items[i] != null) _items[i].gameObject.SetActive(false);
            }
            _remaining = 0;
        }

        /// <summary>Flies from this container to the assigned target.</summary>
        public void Play()
        {
            Play(_container, m_Target);
        }

        /// <summary>Flies from the given RectTransform to the assigned target.</summary>
        public void Play(RectTransform from)
        {
            Play(from, m_Target);
        }

        public void Play(RectTransform from, RectTransform to)
        {
            if (m_Template == null || to == null) return;
            if (from == null) from = _container;

            EnsurePool();

            Vector2 start = ToLocal(from);
            Vector2 target = ToLocal(to);
            float now = Now();

            for (int i = 0; i < m_Count; i++)
            {
                Vector2 scatter = start + UnityEngine.Random.insideUnitCircle * m_ScatterRadius;
                Vector2 toTarget = target - scatter;
                Vector2 side = new Vector2(-toTarget.y, toTarget.x) * ((i & 1) == 0 ? 0.3f : -0.3f);

                _start[i] = start;
                _scatter[i] = scatter;
                _target[i] = target;
                _control[i] = (scatter + target) * 0.5f + side;
                _startTime[i] = now + i * m_Stagger;
                _active[i] = true;
                _items[i].gameObject.SetActive(false);
            }
            _remaining = m_Count;
        }

        private void Update()
        {
            if (_remaining <= 0) return;

            float now = Now();
            for (int i = 0; i < _active.Length; i++)
            {
                if (!_active[i]) continue;

                float elapsed = now - _startTime[i];
                if (elapsed < 0f) continue;

                Image item = _items[i];
                if (!item.gameObject.activeSelf) item.gameObject.SetActive(true);

                float flyElapsed = elapsed - m_ScatterTime - m_Hold;
                Vector2 pos;
                if (flyElapsed < 0f)
                {
                    float t = TweenLite.ApplyEase(Ease.OutQuad, elapsed / m_ScatterTime);
                    pos = Vector2.LerpUnclamped(_start[i], _scatter[i], t);
                }
                else if (flyElapsed < m_FlyTime)
                {
                    float t = TweenLite.ApplyEase(Ease.InOutQuad, flyElapsed / m_FlyTime);
                    pos = Bezier(_scatter[i], _control[i], _target[i], t);
                }
                else
                {
                    Arrive(i);
                    continue;
                }
                item.rectTransform.localPosition = pos;
            }
        }

        private void Arrive(int i)
        {
            _active[i] = false;
            _items[i].gameObject.SetActive(false);
            m_OnItemArrived.Invoke();

            _remaining--;
            if (_remaining == 0) m_OnAllArrived.Invoke();
        }

        /// <summary>Grows the pool and per-item arrays up to m_Count.</summary>
        private void EnsurePool()
        {
            int old = _items.Length;
            if (old >= m_Count) return;

            Array.Resize(ref _items, m_Count);
            Array.Resize(ref _start, m_Count);
            Array.Resize(ref _scatter, m_Count);
            Array.Resize(ref _control, m_Count);
            Array.Resize(ref _target, m_Count);
            Array.Resize(ref _startTime, m_Count);
            Array.Resize(ref _active, m_Count);

            for (int i = old; i < m_Count; i++)
            {
                Image item = Instantiate(m_Template, _container, false);
                item.name = "CoinItem_" + i;
                item.raycastTarget = false;
                RectTransform rect = item.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                item.gameObject.SetActive(false);
                _items[i] = item;
            }
        }

        /// <summary>World position of a RectTransform converted to this container's local space.</summary>
        private Vector2 ToLocal(RectTransform rect)
        {
            Canvas canvas = _container.GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = canvas.rootCanvas.worldCamera;

            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, rect.position);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_container, screen, cam, out local);
            return local;
        }

        private float Now()
        {
            return m_UseUnscaledTime ? Time.unscaledTime : Time.time;
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }
    }
}
