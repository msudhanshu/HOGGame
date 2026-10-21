using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Unity
{
    public sealed class HuntTween
    {
        internal Object Owner;
        internal bool HasOwner;
        internal float Duration;
        internal float Delay;
        internal float Elapsed;
        internal Action<float> Step;
        internal Func<float, float> Curve;
        internal Action Done;
        internal bool Dead;

        public void Kill() => Dead = true;
    }

    /// <summary>Tiny unscaled-time tween runner for UI juice. Tweens die with their owner.</summary>
    public sealed class HuntTweener : MonoBehaviour
    {
        static HuntTweener _instance;
        static readonly List<HuntTween> Active = new List<HuntTween>(64);
        static readonly List<HuntTween> Scratch = new List<HuntTween>(64);

        static void EnsureRunner()
        {
            if (_instance != null)
                return;
            var go = new GameObject("HuntTweener") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<HuntTweener>();
        }

        public static HuntTween To(Object owner, float duration, Action<float> step, Func<float, float> ease = null,
            float delay = 0f, Action done = null)
        {
            EnsureRunner();
            var tween = new HuntTween
            {
                Owner = owner,
                HasOwner = owner != null,
                Duration = Mathf.Max(0.0001f, duration),
                Delay = Mathf.Max(0f, delay),
                Step = step,
                Curve = ease ?? (Func<float, float>)Ease.OutQuad,
                Done = done
            };
            Active.Add(tween);
            return tween;
        }

        /// <summary>Calls done after a delay (unscaled).</summary>
        public static HuntTween Delay(Object owner, float seconds, Action done)
        {
            return To(owner, 0.0001f, null, Ease.Linear, seconds, done);
        }

        public static void Kill(Object owner)
        {
            if (owner == null)
                return;
            for (var i = 0; i < Active.Count; i++)
            {
                if (ReferenceEquals(Active[i].Owner, owner))
                    Active[i].Dead = true;
            }
        }

        public static HuntTween Scale(Transform t, Vector3 from, Vector3 to, float duration, Func<float, float> ease = null,
            float delay = 0f, Action done = null)
        {
            if (t == null)
                return null;
            if (delay <= 0f)
                t.localScale = from;
            return To(t, duration, k => t.localScale = Vector3.LerpUnclamped(from, to, k), ease, delay, done);
        }

        /// <summary>Overshoot "boing" around the transform's current scale.</summary>
        public static HuntTween Punch(Transform t, float amount = 0.2f, float duration = 0.45f)
        {
            if (t == null)
                return null;
            Kill(t);
            var baseScale = Vector3.one;
            return To(t, duration, k => t.localScale = baseScale * (1f + Ease.Punch(k) * amount), Ease.Linear, 0f,
                () => t.localScale = baseScale);
        }

        public static HuntTween Move(RectTransform t, Vector2 from, Vector2 to, float duration, Func<float, float> ease = null,
            float delay = 0f, Action done = null)
        {
            if (t == null)
                return null;
            if (delay <= 0f)
                t.anchoredPosition = from;
            return To(t, duration, k => t.anchoredPosition = Vector2.LerpUnclamped(from, to, k), ease, delay, done);
        }

        public static HuntTween Fade(CanvasGroup group, float from, float to, float duration, float delay = 0f, Action done = null)
        {
            if (group == null)
                return null;
            if (delay <= 0f)
                group.alpha = from;
            return To(group, duration, k => group.alpha = Mathf.LerpUnclamped(from, to, k), Ease.OutQuad, delay, done);
        }

        public static HuntTween Tint(Graphic graphic, Color from, Color to, float duration, float delay = 0f, Action done = null)
        {
            if (graphic == null)
                return null;
            return To(graphic, duration, k => graphic.color = Color.LerpUnclamped(from, to, k), Ease.OutQuad, delay, done);
        }

        void Update()
        {
            if (Active.Count == 0)
                return;
            var dt = Time.unscaledDeltaTime;
            Scratch.Clear();
            Scratch.AddRange(Active);
            for (var i = 0; i < Scratch.Count; i++)
            {
                var tw = Scratch[i];
                if (tw.Dead || (tw.HasOwner && tw.Owner == null))
                {
                    tw.Dead = true;
                    continue;
                }

                if (tw.Delay > 0f)
                {
                    tw.Delay -= dt;
                    continue;
                }

                tw.Elapsed += dt;
                var t = Mathf.Clamp01(tw.Elapsed / tw.Duration);
                try
                {
                    tw.Step?.Invoke(tw.Curve(t));
                    if (t >= 1f)
                    {
                        tw.Dead = true;
                        tw.Done?.Invoke();
                    }
                }
                catch (Exception e)
                {
                    tw.Dead = true;
                    Debug.LogException(e);
                }
            }

            Active.RemoveAll(tw => tw.Dead);
        }
    }
}
