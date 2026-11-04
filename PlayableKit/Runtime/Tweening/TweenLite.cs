using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Playable.Tweening
{
    public enum Ease
    {
        Linear,
        InSine,
        OutSine,
        InOutSine,
        OutBack,
        InBack,
        InQuad,
        OutQuad,
        InOutQuad,
        OutCubic,
        OutElastic,
        InCubic
    }

    public enum RotateMode
    {
        Fast,
        FastBeyond360,
        WorldAxisAdd,
        LocalAxisAdd
    }

    public enum LoopType
    {
        Restart,
        Yoyo,
        Incremental
    }

    public sealed class TweenRunner : MonoBehaviour { }

    public class Tween : CustomYieldInstruction
    {
        internal Func<Tween, IEnumerator> routineFactory;
        internal object target;
        internal object id;
        internal Coroutine coroutine;
        internal bool controlled;
        internal bool ignoreTimeScale;
        internal bool killed;
        internal bool playing;
        internal bool started;
        internal bool completed;
        internal bool paused;
        internal bool relative;
        internal bool from;
        internal Ease ease = Ease.Linear;
        internal int loops = 1;
        internal LoopType loopType = LoopType.Restart;
        internal float delay;
        internal Action<bool> completionAction;
        internal Action completeAction;
        internal Action updateAction;
        public float timeScale = 1f;

        public override bool keepWaiting => !killed && !completed && (!started || playing);

        internal Tween(Func<Tween, IEnumerator> routineFactory, object target = null, bool autoPlay = true)
        {
            this.routineFactory = routineFactory;
            this.target = target;
            if (autoPlay)
            {
                var runner = TweenLite.Runner;
                if (runner != null)
                {
                    runner.StartCoroutine(AutoPlay());
                }
            }
        }

        private IEnumerator AutoPlay()
        {
            yield return null;
            if (!controlled && !playing && !killed && !completed)
            {
                Play();
            }
        }

        public virtual Tween Play()
        {
            if (playing && paused)
            {
                paused = false;
                return this;
            }
            if (killed || completed || playing) return this;
            started = true;
            TweenLite.Register(this);
            var runner = TweenLite.Runner;
            if (runner == null) return this;
            coroutine = runner.StartCoroutine(TweenLite.Drive(Run()));
            return this;
        }

        internal IEnumerator RunForSequence(bool parentIgnoreTimeScale)
        {
            controlled = true;
            started = true;
            killed = false;
            completed = false;
            paused = false;
            ignoreTimeScale = ignoreTimeScale || parentIgnoreTimeScale;
            TweenLite.Register(this);
            yield return Run();
        }

        internal virtual IEnumerator Run()
        {
            playing = true;
            if (delay > 0f)
            {
                yield return TweenLite.Wait(delay, this);
            }
            if (routineFactory != null)
            {
                yield return routineFactory(this);
            }
            playing = false;
            TweenLite.Unregister(this);
            if (!killed)
            {
                completed = true;
                if((completeAction!=null)){completeAction.Invoke();}
            }
        }

        public virtual void Kill()
        {
            Kill(false);
        }

        public virtual void Kill(bool complete)
        {
            if (complete)
            {
                Complete();
                return;
            }
            if (killed || completed) return;
            killed = true;
            started = true;
            playing = false;
            if (coroutine != null && TweenLite.HasRunner)
            {
                TweenLite.Runner.StopCoroutine(coroutine);
            }
            TweenLite.Unregister(this);
        }

        public virtual void Complete()
        {
            Complete(true);
        }

        public virtual void Complete(bool withCallbacks)
        {
            if (killed || completed) return;
            started = true;
            playing = false;
            if (coroutine != null && TweenLite.HasRunner)
            {
                TweenLite.Runner.StopCoroutine(coroutine);
            }
            completed = true;
            ApplyCompletion(withCallbacks);
            killed = true;
            TweenLite.Unregister(this);
            if (withCallbacks)
            {
                if((completeAction!=null)){completeAction.Invoke();}
            }
        }

        internal virtual void ApplyCompletion(bool withCallbacks)
        {
            if((completionAction!=null)){completionAction.Invoke(withCallbacks);}
        }

        public IEnumerator WaitForCompletion()
        {
            while (!killed && !completed && (!started || playing))
            {
                yield return null;
            }
        }

        public bool IsPlaying()
        {
            return playing && !paused && !killed;
        }

        public bool IsActive()
        {
            return !killed && !completed;
        }

        public bool IsComplete()
        {
            return completed;
        }

        public Tween Pause()
        {
            paused = true;
            return this;
        }

        public Tween SetEase(Ease ease)
        {
            this.ease = ease;
            return this;
        }

        public Tween SetEase(Ease ease, float overshoot)
        {
            return SetEase(ease);
        }

        public Tween SetUpdate(bool ignoreTimeScale)
        {
            this.ignoreTimeScale = ignoreTimeScale;
            return this;
        }

        public Tween SetId(object id)
        {
            this.id = id;
            return this;
        }

        public Tween SetDelay(float delay)
        {
            this.delay = Mathf.Max(0f, delay);
            return this;
        }

        public Tween SetLoops(int loops, LoopType loopType = LoopType.Restart)
        {
            this.loops = loops;
            this.loopType = loopType;
            return this;
        }

        public Tween SetRelative(bool isRelative = true)
        {
            relative = isRelative;
            return this;
        }

        public Tween OnComplete(Action action)
        {
            completeAction += action;
            return this;
        }

        public Tween OnUpdate(Action action)
        {
            updateAction += action;
            return this;
        }

        public virtual Tween From()
        {
            from = true;
            return this;
        }

        public Tween Goto(float to, bool andPlay = false)
        {
            if (andPlay && !playing)
            {
                Play();
            }
            return this;
        }

        public Tween Restart()
        {
            if (playing)
            {
                Kill();
            }
            killed = false;
            completed = false;
            started = false;
            paused = false;
            return Play();
        }
    }

    public class Tweener : Tween
    {
        internal Tweener(Func<Tween, IEnumerator> routineFactory, object target = null, bool autoPlay = true)
            : base(routineFactory, target, autoPlay)
        {
        }

        public new Tweener Play()
        {
            base.Play();
            return this;
        }

        public new Tweener Pause()
        {
            base.Pause();
            return this;
        }

        public new Tweener SetEase(Ease ease)
        {
            base.SetEase(ease);
            return this;
        }

        public new Tweener SetEase(Ease ease, float overshoot)
        {
            base.SetEase(ease, overshoot);
            return this;
        }

        public new Tweener SetUpdate(bool ignoreTimeScale)
        {
            base.SetUpdate(ignoreTimeScale);
            return this;
        }

        public new Tweener SetId(object id)
        {
            base.SetId(id);
            return this;
        }

        public new Tweener SetDelay(float delay)
        {
            base.SetDelay(delay);
            return this;
        }

        public new Tweener SetLoops(int loops, LoopType loopType = LoopType.Restart)
        {
            base.SetLoops(loops, loopType);
            return this;
        }

        public new Tweener SetRelative(bool isRelative = true)
        {
            base.SetRelative(isRelative);
            return this;
        }

        public new Tweener OnComplete(Action action)
        {
            base.OnComplete(action);
            return this;
        }

        public new Tweener OnUpdate(Action action)
        {
            base.OnUpdate(action);
            return this;
        }

        public new Tweener From()
        {
            base.From();
            return this;
        }
    }

    public class Sequence : Tween
    {
        private readonly List<object> steps = new List<object>();
        private Tween currentTween;
        private readonly List<Tween> currentTweens = new List<Tween>();
        private int currentStepIndex;

        internal Sequence() : base(null)
        {
            routineFactory = RunSequence;
        }

        public Sequence Append(Tween tween)
        {
            if (tween == null) return this;
            tween.controlled = true;
            steps.Add(tween);
            return this;
        }

        public Sequence AppendInterval(float seconds)
        {
            steps.Add(Mathf.Max(0f, seconds));
            return this;
        }

        public Sequence AppendCallback(Action action)
        {
            steps.Add(action);
            return this;
        }

        public Sequence Join(Tween tween)
        {
            if (tween == null) return this;
            tween.controlled = true;
            if (steps.Count == 0)
            {
                steps.Add(tween);
                return this;
            }

            var last = steps[steps.Count - 1];
            if (last is List<Tween> parallel)
            {
                parallel.Add(tween);
            }
            else if (last is Tween existing)
            {
                steps[steps.Count - 1] = new List<Tween> { existing, tween };
            }
            else
            {
                steps.Add(tween);
            }
            return this;
        }

        private IEnumerator RunSequence(Tween tween)
        {
            bool infinite = loops < 0;
            int remain = infinite ? 1 : Mathf.Max(1, loops);

            do
            {
                for (currentStepIndex = 0; currentStepIndex < steps.Count; currentStepIndex++)
                {
                    var step = steps[currentStepIndex];
                    if (killed) yield break;
                    if (step is Tween child)
                    {
                        currentTween = child;
                        yield return child.RunForSequence(ignoreTimeScale);
                        currentTween = null;
                    }
                    else if (step is List<Tween> parallel)
                    {
                        int running = parallel.Count;
                        currentTweens.Clear();
                    foreach (var parallelChild in parallel)
                    {
                        currentTweens.Add(parallelChild);
                        TweenLite.Runner.StartCoroutine(TweenLite.Drive(RunParallelChild(parallelChild, () => running--)));
                        }
                        while (!killed && running > 0)
                        {
                            yield return null;
                        }
                        currentTweens.Clear();
                    }
                    else if (step is float wait)
                    {
                        yield return TweenLite.Wait(wait, this);
                    }
                    else if (step is Action action)
                    {
                        action.Invoke();
                    }
                }
                if (!infinite)
                {
                    remain--;
                }
            }
            while (!killed && (infinite || remain > 0));

            currentStepIndex = steps.Count;
        }

        private IEnumerator RunParallelChild(Tween child, Action onDone)
        {
            yield return child.RunForSequence(ignoreTimeScale);
            if((onDone!=null)){onDone.Invoke();}
        }

        internal override void ApplyCompletion(bool withCallbacks)
        {
            for (int i = currentStepIndex; i < steps.Count; i++)
            {
                if (steps[i] is Tween child)
                {
                    child.Complete(withCallbacks);
                }
                else if (steps[i] is List<Tween> parallel)
                {
                    foreach (var parallelChild in parallel)
                    {
                        parallelChild.Complete(withCallbacks);
                    }
                }
                else if (steps[i] is Action action && withCallbacks)
                {
                    action.Invoke();
                }
            }
            currentTween = null;
            currentStepIndex = steps.Count;
        }

        public override void Kill()
        {
            if((currentTween!=null)){currentTween.Kill();}
            foreach (var tween in currentTweens)
            {
                tween.Kill();
            }
            currentTweens.Clear();
            base.Kill();
        }

        public new Sequence SetEase(Ease ease)
        {
            base.SetEase(ease);
            return this;
        }

        public new Sequence SetUpdate(bool ignoreTimeScale)
        {
            base.SetUpdate(ignoreTimeScale);
            return this;
        }

        public new Sequence SetId(object id)
        {
            base.SetId(id);
            return this;
        }

        public new Sequence SetDelay(float delay)
        {
            base.SetDelay(delay);
            return this;
        }

        public new Sequence SetLoops(int loops, LoopType loopType = LoopType.Restart)
        {
            base.SetLoops(loops, loopType);
            return this;
        }

        public new Sequence OnComplete(Action action)
        {
            base.OnComplete(action);
            return this;
        }
    }

    internal sealed class ValueTween<T> : Tweener
    {
        private readonly Func<T> getter;
        private readonly Action<T> setter;
        private readonly Func<T, T, float, T> lerp;
        private readonly Func<T, T, T> add;
        private readonly T endValue;
        private readonly float duration;
        private T completionValue;
        private bool hasCompletionValue;

        internal ValueTween(Func<T> getter, Action<T> setter, T endValue, float duration, Func<T, T, float, T> lerp, object target, Func<T, T, T> add = null)
            : base(null, target)
        {
            this.getter = getter;
            this.setter = setter;
            this.endValue = endValue;
            this.duration = Mathf.Max(0f, duration);
            this.lerp = lerp;
            this.add = add;
            routineFactory = RunValue;
        }

        private IEnumerator RunValue(Tween tween)
        {
            T start = getter();
            T end = relative && add != null ? add(start, endValue) : endValue;
            if (from)
            {
                end = start;
                start = endValue;
                setter(start);
            }
            completionValue = end;
            hasCompletionValue = true;

            yield return TweenLite.TweenValue(tween, duration, v => setter(lerp(start, end, v)));
        }

        internal override void ApplyCompletion(bool withCallbacks)
        {
            if (!hasCompletionValue)
            {
                var current = getter();
                completionValue = from ? current : (relative && add != null ? add(current, endValue) : endValue);
                hasCompletionValue = true;
            }
            setter(completionValue);
            if (withCallbacks)
            {
                if((updateAction!=null)){updateAction.Invoke();}
            }
        }
    }

    internal sealed class JumpTween : Tweener
    {
        private readonly Transform transform;
        private readonly Vector3 endValue;
        private readonly float jumpPower;
        private readonly int jumps;
        private readonly float duration;

        internal JumpTween(Transform transform, Vector3 endValue, float jumpPower, int jumps, float duration)
            : base(null, transform)
        {
            this.transform = transform;
            this.endValue = endValue;
            this.jumpPower = jumpPower;
            this.jumps = Mathf.Max(1, jumps);
            this.duration = Mathf.Max(0f, duration);
            routineFactory = RunJump;
        }

        private IEnumerator RunJump(Tween tween)
        {
            Vector3 start = transform.position;
            yield return TweenLite.TweenValue(tween, duration, v =>
            {
                Vector3 pos = Vector3.LerpUnclamped(start, endValue, v);
                pos.y += Mathf.Abs(Mathf.Sin(v * Mathf.PI * jumps)) * jumpPower;
                transform.position = pos;
            });
        }

        internal override void ApplyCompletion(bool withCallbacks)
        {
            transform.position = endValue;
            if (withCallbacks)
            {
                if((updateAction!=null)){updateAction.Invoke();}
            }
        }
    }

    internal sealed class ShakePositionTween : Tweener
    {
        private readonly Transform targetTransform;
        private readonly float duration;
        private readonly Vector3 strength;
        private readonly int vibrato;
        private Vector3 start;
        private bool hasStart;

        internal ShakePositionTween(Transform targetTransform, float duration, Vector3 strength, int vibrato)
            : base(null, targetTransform)
        {
            this.targetTransform = targetTransform;
            this.duration = Mathf.Max(0f, duration);
            this.strength = strength;
            this.vibrato = Mathf.Max(1, vibrato);
            routineFactory = RunShake;
        }

        private IEnumerator RunShake(Tween tween)
        {
            start = targetTransform.position;
            hasStart = true;
            yield return TweenLite.TweenValue(tween, duration, v =>
            {
                float damper = 1f - v;
                float seed = Mathf.Floor(v * vibrato);
                float x = Mathf.Sin(seed * 12.9898f) * strength.x * damper;
                float y = Mathf.Sin(seed * 78.233f) * strength.y * damper;
                float z = Mathf.Sin(seed * 37.719f) * strength.z * damper;
                targetTransform.position = start + new Vector3(x, y, z);
            });
        }

        internal override void ApplyCompletion(bool withCallbacks)
        {
            if (!hasStart)
            {
                start = targetTransform.position;
                hasStart = true;
            }
            targetTransform.position = start;
            if (withCallbacks)
            {
                if((updateAction!=null)){updateAction.Invoke();}
            }
        }
    }

    public static class TweenLite
    {
        private static MonoBehaviour runner;
        private static readonly List<Tween> tweens = new List<Tween>();
        public static float timeScale = 1f;

        internal static bool HasRunner => runner != null;

        internal static MonoBehaviour Runner
        {
            get
            {
                if (runner == null)
                {
                    runner = UnityEngine.Object.FindObjectOfType<TweenRunner>();
                    if (runner == null)
                    {
                        var runnerObject = new GameObject(nameof(TweenRunner));
                        runner = runnerObject.AddComponent<TweenRunner>();
                    }
                }
                return runner;
            }
        }

        public static Sequence Sequence()
        {
            return new Sequence();
        }

        public static void Kill(object target)
        {
            if (target is Tween tween)
            {
                tween.Kill();
                return;
            }

            for (int i = tweens.Count - 1; i >= 0; i--)
            {
                if (MatchesTarget(tweens[i], target))
                {
                    tweens[i].Kill();
                }
            }
        }

        public static Tweener To(Func<float> getter, Action<float> setter, float endValue, float duration)
        {
            return new ValueTween<float>(getter, setter, endValue, duration, Mathf.LerpUnclamped, setter, (a, b) => a + b);
        }

        public static Tweener To(Func<int> getter, Action<int> setter, int endValue, float duration)
        {
            return new ValueTween<int>(getter, setter, endValue, duration, (a, b, t) => Mathf.RoundToInt(Mathf.LerpUnclamped(a, b, t)), setter, (a, b) => a + b);
        }

        internal static void Register(Tween tween)
        {
            if (!tweens.Contains(tween))
            {
                tweens.Add(tween);
            }
        }

        internal static void Unregister(Tween tween)
        {
            tweens.Remove(tween);
        }

        // Luna never resumes a coroutine after a nested IEnumerator it yielded finishes, so tweens that should
        // end (sequences, OnComplete) hang there. Step nested enumerators here and hand Unity only plain yields.
        internal static IEnumerator Drive(IEnumerator root)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext())
                {
                    stack.Pop();
                    continue;
                }
                var nested = top.Current as IEnumerator;
                if (nested != null)
                {
                    stack.Push(nested);
                    continue;
                }
                yield return top.Current;
            }
        }

        internal static IEnumerator Wait(float seconds, Tween tween)
        {
            float elapsed = 0f;
            while (!tween.killed && elapsed < seconds)
            {
                elapsed += Delta(tween);
                yield return null;
            }
        }

        internal static IEnumerator TweenValue(Tween tween, float duration, Action<float> setter)
        {
            bool infinite = tween.loops < 0;
            int remain = infinite ? 1 : Mathf.Max(1, tween.loops);
            bool reverse = false;

            do
            {
                float elapsed = 0f;
                while (!tween.killed && elapsed < duration)
                {
                    elapsed += Delta(tween);
                    float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                    float eased = ApplyEase(tween.ease, reverse ? 1f - t : t);
                    setter(eased);
                    if((tween.updateAction!=null)){tween.updateAction.Invoke();}
                    yield return null;
                }

                if (tween.killed) yield break;
                setter(reverse ? 0f : 1f);
                if((tween.updateAction!=null)){tween.updateAction.Invoke();}

                if (tween.loopType == LoopType.Yoyo)
                {
                    reverse = !reverse;
                }
                if (!infinite)
                {
                    remain--;
                }
            }
            while (!tween.killed && (infinite || remain > 0));
        }

        internal static float Delta(Tween tween)
        {
            if (tween.paused) return 0f;
            float delta = tween.ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            return delta * Mathf.Max(0f, tween.timeScale) * Mathf.Max(0f, timeScale);
        }

        public static float ApplyEase(Ease ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case Ease.InSine:
                    return 1f - Mathf.Cos(t * Mathf.PI * 0.5f);
                case Ease.OutSine:
                    return Mathf.Sin(t * Mathf.PI * 0.5f);
                case Ease.InOutSine:
                    return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
                case Ease.OutBack:
                    {
                        const float c1 = 1.70158f;
                        const float c3 = c1 + 1f;
                        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
                    }
                case Ease.InBack:
                    {
                        const float c1 = 1.70158f;
                        const float c3 = c1 + 1f;
                        return c3 * t * t * t - c1 * t * t;
                    }
                case Ease.InQuad:
                    return t * t;
                case Ease.OutQuad:
                    return 1f - (1f - t) * (1f - t);
                case Ease.InOutQuad:
                    return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
                case Ease.OutCubic:
                    return 1f - Mathf.Pow(1f - t, 3f);
                case Ease.InCubic:
                    return t * t * t;
                case Ease.OutElastic:
                    return t == 0f || t == 1f ? t : Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;
                default:
                    return t;
            }
        }

        private static bool MatchesTarget(Tween tween, object target)
        {
            if (tween == null || target == null) return false;
            if (ReferenceEquals(tween.target, target)) return true;
            if (ReferenceEquals(tween.id, target) || (tween.id != null && tween.id.Equals(target))) return true;

            var targetGameObject = target as GameObject;
            var component = tween.target as Component;
            if (targetGameObject != null && component != null)
            {
                return component.gameObject == targetGameObject;
            }

            var targetComponent = target as Component;
            if (targetComponent != null && component != null)
            {
                return targetComponent == component || targetComponent.gameObject == component.gameObject;
            }

            return false;
        }
    }

    public static class DOVirtual
    {
        public static Tween DelayedCall(float delay, Action callback)
        {
            var tween = new Tweener(t => Run(delay, callback, t));
            tween.completionAction = withCallbacks =>
            {
                if (withCallbacks)
                {
                    if((callback!=null)){callback.Invoke();}
                }
            };
            return tween;
        }

        public static Tween Float(float from, float to, float duration, Action<float> onValue)
        {
            var tween = new Tweener(t => TweenLite.TweenValue(t, duration, v =>
            {
                if((onValue!=null)){onValue.Invoke(Mathf.LerpUnclamped(from, to, v));}
            }));
            tween.completionAction = _ => {if((onValue!=null)){onValue.Invoke(to);}};
            return tween;
        }

        private static IEnumerator Run(float delay, Action callback, Tween tween)
        {
            yield return TweenLite.Wait(delay, tween);
            if (!tween.killed)
            {
                if((callback!=null)){callback.Invoke();}
            }
        }
    }

    public static class TweenExtensions
    {
        public static void DOKill(this Transform target)
        {
            TweenLite.Kill(target);
        }

        public static void DOKill(this Component target)
        {
            TweenLite.Kill(target);
        }

        public static void DOKill(this GameObject target)
        {
            TweenLite.Kill(target);
        }

        public static void DOKill(this ScrollRect target)
        {
            TweenLite.Kill(target);
        }

        public static Tweener DOScale(this Transform target, float endValue, float duration)
        {
            return DOScale(target, Vector3.one * endValue, duration);
        }

        public static Tweener DOScale(this Transform target, Vector2 endValue, float duration)
        {
            return DOScale(target, new Vector3(endValue.x, endValue.y, target.localScale.z), duration);
        }

        public static Tweener DOScale(this Transform target, Vector3 endValue, float duration)
        {
            return new ValueTween<Vector3>(() => target.localScale, v => target.localScale = v, endValue, duration, Vector3.LerpUnclamped, target, (a, b) => a + b);
        }

        public static Tweener DOMove(this Transform target, Vector3 endValue, float duration)
        {
            return new ValueTween<Vector3>(() => target.position, v => target.position = v, endValue, duration, Vector3.LerpUnclamped, target, (a, b) => a + b);
        }

        public static Tweener DOMove(this Transform target, Vector3 endValue, float duration, bool snapping)
        {
            return DOMove(target, endValue, duration);
        }

        public static Tweener DOMoveX(this Transform target, float endValue, float duration)
        {
            return new ValueTween<float>(() => target.position.x, v =>
            {
                var p = target.position;
                p.x = v;
                target.position = p;
            }, endValue, duration, Mathf.LerpUnclamped, target, (a, b) => a + b);
        }

        public static Tweener DOMoveY(this Transform target, float endValue, float duration)
        {
            return new ValueTween<float>(() => target.position.y, v =>
            {
                var p = target.position;
                p.y = v;
                target.position = p;
            }, endValue, duration, Mathf.LerpUnclamped, target, (a, b) => a + b);
        }

        public static Tweener DOMoveZ(this Transform target, float endValue, float duration)
        {
            return new ValueTween<float>(() => target.position.z, v =>
            {
                var p = target.position;
                p.z = v;
                target.position = p;
            }, endValue, duration, Mathf.LerpUnclamped, target, (a, b) => a + b);
        }

        public static Tweener DOLocalMove(this Transform target, Vector3 endValue, float duration)
        {
            return new ValueTween<Vector3>(() => target.localPosition, v => target.localPosition = v, endValue, duration, Vector3.LerpUnclamped, target, (a, b) => a + b);
        }

        public static Tweener DOLocalMoveX(this Transform target, float endValue, float duration)
        {
            return new ValueTween<float>(() => target.localPosition.x, v =>
            {
                var p = target.localPosition;
                p.x = v;
                target.localPosition = p;
            }, endValue, duration, Mathf.LerpUnclamped, target, (a, b) => a + b);
        }

        public static Tweener DOLocalMoveY(this Transform target, float endValue, float duration)
        {
            return new ValueTween<float>(() => target.localPosition.y, v =>
            {
                var p = target.localPosition;
                p.y = v;
                target.localPosition = p;
            }, endValue, duration, Mathf.LerpUnclamped, target, (a, b) => a + b);
        }

        public static Tweener DOLocalMoveZ(this Transform target, float endValue, float duration)
        {
            return new ValueTween<float>(() => target.localPosition.z, v =>
            {
                var p = target.localPosition;
                p.z = v;
                target.localPosition = p;
            }, endValue, duration, Mathf.LerpUnclamped, target, (a, b) => a + b);
        }

        private static Vector3 LerpAngleVec3(Vector3 a, Vector3 b, float t)
        {
            return new Vector3(Mathf.LerpAngle(a.x, b.x, t), Mathf.LerpAngle(a.y, b.y, t), Mathf.LerpAngle(a.z, b.z, t));
        }

        public static Tweener DORotate(this Transform target, Vector3 endValue, float duration)
        {
            return DORotate(target, endValue, duration, RotateMode.Fast);
        }

        public static Tweener DOLocalRotate(this Transform target, Vector3 endValue, float duration)
        {
            return DOLocalRotate(target, endValue, duration, RotateMode.Fast);
        }

        public static Tweener DORotate(this Transform target, Vector3 endValue, float duration, RotateMode mode)
        {
            var lerp = mode == RotateMode.FastBeyond360 ? (Func<Vector3, Vector3, float, Vector3>)Vector3.LerpUnclamped : LerpAngleVec3;
            return new ValueTween<Vector3>(() => target.eulerAngles, v => target.rotation = Quaternion.Euler(v), endValue, duration, lerp, target, (a, b) => a + b);
        }

        public static Tweener DOLocalRotate(this Transform target, Vector3 endValue, float duration, RotateMode mode)
        {
            var lerp = mode == RotateMode.FastBeyond360 ? (Func<Vector3, Vector3, float, Vector3>)Vector3.LerpUnclamped : LerpAngleVec3;
            return new ValueTween<Vector3>(() => target.localEulerAngles, v => target.localRotation = Quaternion.Euler(v), endValue, duration, lerp, target, (a, b) => a + b);
        }

        public static Tweener DOBlendableLocalMoveBy(this Transform target, Vector2 byValue, float duration)
        {
            return new ValueTween<Vector3>(() => target.localPosition, v => target.localPosition = v,
                new Vector3(byValue.x, byValue.y, 0f), duration, Vector3.LerpUnclamped, target, (a, b) => a + b).SetRelative(true);
        }

        public static Tweener DOJump(this Transform target, Vector3 endValue, float jumpPower, int numJumps, float duration)
        {
            return new JumpTween(target, endValue, jumpPower, numJumps, duration);
        }

        public static Tweener DOAnchorPos(this RectTransform target, Vector2 endValue, float duration)
        {
            return new ValueTween<Vector2>(() => target.anchoredPosition, v => target.anchoredPosition = v, endValue, duration, Vector2.LerpUnclamped, target, (a, b) => a + b);
        }

        public static Tweener DOAnchorPosX(this RectTransform target, float endValue, float duration)
        {
            return new ValueTween<float>(() => target.anchoredPosition.x, v =>
            {
                var p = target.anchoredPosition;
                p.x = v;
                target.anchoredPosition = p;
            }, endValue, duration, Mathf.LerpUnclamped, target, (a, b) => a + b);
        }

        public static Tweener DOAnchorPosY(this RectTransform target, float endValue, float duration)
        {
            return new ValueTween<float>(() => target.anchoredPosition.y, v =>
            {
                var p = target.anchoredPosition;
                p.y = v;
                target.anchoredPosition = p;
            }, endValue, duration, Mathf.LerpUnclamped, target, (a, b) => a + b);
        }

        public static Tweener DOHorizontalNormalizedPos(this ScrollRect target, float endValue, float duration)
        {
            return new ValueTween<float>(() => target.horizontalNormalizedPosition, v => target.horizontalNormalizedPosition = v, endValue, duration, Mathf.LerpUnclamped, target);
        }

        public static Tweener DOFade(this CanvasGroup target, float endValue, float duration)
        {
            return new ValueTween<float>(() => target.alpha, v => target.alpha = v, endValue, duration, Mathf.LerpUnclamped, target);
        }

        public static Tweener DOFade(this Graphic target, float endValue, float duration)
        {
            return new ValueTween<Color>(() => target.color, v => target.color = v,
                new Color(target.color.r, target.color.g, target.color.b, endValue), duration, Color.LerpUnclamped, target);
        }

        public static Tweener DOColor(this Graphic target, Color endValue, float duration)
        {
            return new ValueTween<Color>(() => target.color, v => target.color = v, endValue, duration, Color.LerpUnclamped, target);
        }

        public static Tweener DOFade(this SpriteRenderer target, float endValue, float duration)
        {
            return new ValueTween<Color>(() => target.color, v => target.color = v,
                new Color(target.color.r, target.color.g, target.color.b, endValue), duration, Color.LerpUnclamped, target);
        }

        public static Tweener DOColor(this SpriteRenderer target, Color endValue, float duration)
        {
            return new ValueTween<Color>(() => target.color, v => target.color = v, endValue, duration, Color.LerpUnclamped, target);
        }

        public static Tweener DOFillAmount(this Image target, float endValue, float duration)
        {
            return new ValueTween<float>(() => target.fillAmount, v => target.fillAmount = v, endValue, duration, Mathf.LerpUnclamped, target);
        }

        public static Tweener DOFlexibleSize(this LayoutElement target, Vector2 endValue, float duration)
        {
            return new ValueTween<Vector2>(() => new Vector2(target.flexibleWidth, target.flexibleHeight), v =>
            {
                target.flexibleWidth = v.x;
                target.flexibleHeight = v.y;
            }, endValue, duration, Vector2.LerpUnclamped, target, (a, b) => a + b);
        }

        public static Tweener DOShakePosition(this Camera target, float duration, Vector3 strength, int vibrato, float randomness, bool snapping)
        {
            return new ShakePositionTween(target.transform, duration, strength, vibrato);
        }
    }
}
