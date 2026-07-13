using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class PulseForgeUIMotionRunner : MonoBehaviour
    {
        private sealed class ActiveMotion
        {
            public Coroutine Coroutine;
            public Action ApplyFinal;
            public Action Completed;
        }

        private readonly Dictionary<int, ActiveMotion> activeMotions =
            new Dictionary<int, ActiveMotion>();

        public bool IsRunning(int key)
        {
            return activeMotions.ContainsKey(key);
        }

        public void Fade(
            int key,
            CanvasGroup canvasGroup,
            float fromAlpha,
            float toAlpha,
            float duration,
            AnimationCurve curve,
            float delay = 0f,
            Action completed = null)
        {
            if (canvasGroup == null)
            {
                completed?.Invoke();
                return;
            }

            Begin(
                key,
                duration,
                delay,
                curve,
                progress => canvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, progress),
                () => canvasGroup.alpha = toAlpha,
                completed);
        }

        public void Slide(
            int key,
            RectTransform target,
            Vector2 fromPosition,
            Vector2 toPosition,
            float duration,
            AnimationCurve curve,
            float delay = 0f,
            Action completed = null)
        {
            if (target == null)
            {
                completed?.Invoke();
                return;
            }

            Begin(
                key,
                duration,
                delay,
                curve,
                progress => target.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, progress),
                () => target.anchoredPosition = toPosition,
                completed);
        }

        public void Scale(
            int key,
            RectTransform target,
            Vector3 fromScale,
            Vector3 toScale,
            float duration,
            AnimationCurve curve,
            float delay = 0f,
            Action completed = null)
        {
            if (target == null)
            {
                completed?.Invoke();
                return;
            }

            Begin(
                key,
                duration,
                delay,
                curve,
                progress => target.localScale = Vector3.LerpUnclamped(fromScale, toScale, progress),
                () => target.localScale = toScale,
                completed);
        }

        public void Tint(
            int key,
            Graphic target,
            Color fromColor,
            Color toColor,
            float duration,
            AnimationCurve curve,
            float delay = 0f,
            Action completed = null)
        {
            if (target == null)
            {
                completed?.Invoke();
                return;
            }

            Begin(
                key,
                duration,
                delay,
                curve,
                progress => target.color = Color.LerpUnclamped(fromColor, toColor, progress),
                () => target.color = toColor,
                completed);
        }

        public void FadeSlide(
            int key,
            CanvasGroup canvasGroup,
            RectTransform target,
            float fromAlpha,
            float toAlpha,
            Vector2 fromPosition,
            Vector2 toPosition,
            float duration,
            AnimationCurve curve,
            float delay = 0f,
            Action completed = null)
        {
            if (canvasGroup == null || target == null)
            {
                ApplyInstant(canvasGroup, target, toAlpha, toPosition, target == null ? Vector3.one : target.localScale);
                completed?.Invoke();
                return;
            }

            Begin(
                key,
                duration,
                delay,
                curve,
                progress =>
                {
                    canvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, progress);
                    target.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, progress);
                },
                () => ApplyInstant(canvasGroup, target, toAlpha, toPosition, target.localScale),
                completed);
        }

        public void FadeScale(
            int key,
            CanvasGroup canvasGroup,
            RectTransform target,
            float fromAlpha,
            float toAlpha,
            Vector3 fromScale,
            Vector3 toScale,
            float duration,
            AnimationCurve curve,
            float delay = 0f,
            Action completed = null)
        {
            if (canvasGroup == null || target == null)
            {
                ApplyInstant(canvasGroup, target, toAlpha, target == null ? Vector2.zero : target.anchoredPosition, toScale);
                completed?.Invoke();
                return;
            }

            Begin(
                key,
                duration,
                delay,
                curve,
                progress =>
                {
                    canvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, progress);
                    target.localScale = Vector3.LerpUnclamped(fromScale, toScale, progress);
                },
                () => ApplyInstant(canvasGroup, target, toAlpha, target.anchoredPosition, toScale),
                completed);
        }

        public void ApplyInstant(
            CanvasGroup canvasGroup,
            RectTransform target,
            float alpha,
            Vector2 anchoredPosition,
            Vector3 scale)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = alpha;
            }

            if (target != null)
            {
                target.anchoredPosition = anchoredPosition;
                target.localScale = scale;
            }
        }

        public void ApplyScaleInstant(int key, RectTransform target, Vector3 scale)
        {
            Cancel(key, false);
            if (target != null)
            {
                target.localScale = scale;
            }
        }

        public void Cancel(int key, bool applyFinalState = true)
        {
            if (!activeMotions.TryGetValue(key, out ActiveMotion motion))
            {
                return;
            }

            activeMotions.Remove(key);
            if (motion.Coroutine != null)
            {
                StopCoroutine(motion.Coroutine);
            }

            if (applyFinalState)
            {
                motion.ApplyFinal?.Invoke();
                motion.Completed?.Invoke();
            }
        }

        public void CompleteAll()
        {
            if (activeMotions.Count == 0)
            {
                return;
            }

            int[] keys = new int[activeMotions.Count];
            activeMotions.Keys.CopyTo(keys, 0);
            for (int i = 0; i < keys.Length; i++)
            {
                Cancel(keys[i], true);
            }
        }

        private void Begin(
            int key,
            float duration,
            float delay,
            AnimationCurve curve,
            Action<float> applyProgress,
            Action applyFinal,
            Action completed)
        {
            Cancel(key, true);
            if (!Application.isPlaying || !isActiveAndEnabled || duration <= 0f)
            {
                applyFinal?.Invoke();
                completed?.Invoke();
                return;
            }

            ActiveMotion motion = new ActiveMotion
            {
                ApplyFinal = applyFinal,
                Completed = completed
            };
            activeMotions[key] = motion;
            motion.Coroutine = StartCoroutine(RunMotion(
                key,
                motion,
                Mathf.Max(0f, duration),
                Mathf.Max(0f, delay),
                curve ?? PulseForgeUIMotionTokens.EaseOut,
                applyProgress));
        }

        private IEnumerator RunMotion(
            int key,
            ActiveMotion motion,
            float duration,
            float delay,
            AnimationCurve curve,
            Action<float> applyProgress)
        {
            applyProgress(0f);
            float delayElapsed = 0f;
            while (delayElapsed < delay)
            {
                delayElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                applyProgress(curve.Evaluate(normalized));
                yield return null;
            }

            if (!activeMotions.TryGetValue(key, out ActiveMotion current) || current != motion)
            {
                yield break;
            }

            activeMotions.Remove(key);
            motion.ApplyFinal?.Invoke();
            motion.Completed?.Invoke();
        }

        private void OnDisable()
        {
            CompleteAll();
        }
    }
}
