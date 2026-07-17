using System;
using System.Collections.Generic;
using PulseForge.Domain.Rhythm;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public enum RadialCombatVfxKind
    {
        GuardArc,
        CoreBurst,
        DodgeAfterimage,
        LightSlash,
        HeavyImpact,
        MissImpact,
        ArmorBreak,
        BreakSegment,
        BreakFinal,
        ProjectileDeflect
    }

    public readonly struct RadialCombatVfxKey : IEquatable<RadialCombatVfxKey>
    {
        public RadialCombatVfxKey(
            RadialPresentationKey presentationKey,
            RadialCombatVfxKind kind,
            int ordinal = 0)
        {
            PresentationKey = presentationKey;
            Kind = kind;
            Ordinal = ordinal;
        }

        public RadialPresentationKey PresentationKey { get; }
        public RadialCombatVfxKind Kind { get; }
        public int Ordinal { get; }

        public bool Equals(RadialCombatVfxKey other)
        {
            return PresentationKey.Equals(other.PresentationKey)
                && Kind == other.Kind
                && Ordinal == other.Ordinal;
        }

        public override bool Equals(object obj)
        {
            return obj is RadialCombatVfxKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = PresentationKey.GetHashCode();
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ Ordinal;
                return hash;
            }
        }
    }

    public readonly struct RadialCombatVfxCue
    {
        public RadialCombatVfxCue(
            RadialCombatVfxKey key,
            Vector2 position,
            float rotationDegrees,
            double startTimeSeconds,
            float durationSeconds,
            float scale,
            float intensity)
        {
            Key = key;
            Position = position;
            RotationDegrees = rotationDegrees;
            StartTimeSeconds = startTimeSeconds;
            DurationSeconds = Mathf.Max(0.01f, durationSeconds);
            Scale = Mathf.Max(0.01f, scale);
            Intensity = Mathf.Clamp01(intensity);
        }

        public RadialCombatVfxKey Key { get; }
        public Vector2 Position { get; }
        public float RotationDegrees { get; }
        public double StartTimeSeconds { get; }
        public float DurationSeconds { get; }
        public float Scale { get; }
        public float Intensity { get; }

        public bool IsVisible(double songTimeSeconds)
        {
            return songTimeSeconds >= StartTimeSeconds
                && songTimeSeconds <= StartTimeSeconds + DurationSeconds;
        }

        public float Progress(double songTimeSeconds)
        {
            return Mathf.Clamp01((float)(
                (songTimeSeconds - StartTimeSeconds) / DurationSeconds));
        }
    }

    public sealed class RadialCombatVfxLayer : MonoBehaviour
    {
        [SerializeField] private RectTransform poolRoot;
        [SerializeField] private RadialCombatVfxView[] pool = Array.Empty<RadialCombatVfxView>();

        private readonly Dictionary<RadialCombatVfxKey, RadialCombatVfxView> active =
            new Dictionary<RadialCombatVfxKey, RadialCombatVfxView>(RadialVfxTokens.PoolCapacity);
        private readonly HashSet<RadialCombatVfxKey> desired =
            new HashSet<RadialCombatVfxKey>();
        private readonly List<RadialCombatVfxKey> releaseScratch =
            new List<RadialCombatVfxKey>(RadialVfxTokens.PoolCapacity);
        private bool poolWarningLogged;

        public RectTransform PoolRoot => poolRoot;
        public IReadOnlyList<RadialCombatVfxView> Pool => pool;

        public void Configure(RectTransform root, RadialCombatVfxView[] views)
        {
            poolRoot = root;
            pool = views ?? Array.Empty<RadialCombatVfxView>();
            ResetView();
        }

        public void BeginFrame()
        {
            desired.Clear();
        }

        public void RenderCue(RadialCombatVfxCue cue, double songTimeSeconds)
        {
            if (!cue.IsVisible(songTimeSeconds))
            {
                return;
            }

            desired.Add(cue.Key);
            if (!active.TryGetValue(cue.Key, out RadialCombatVfxView view))
            {
                view = FindFreeView();
                if (view == null)
                {
                    if (!poolWarningLogged)
                    {
                        poolWarningLogged = true;
                        Debug.LogWarning("Radial combat VFX pool capacity was exhausted.", this);
                    }
                    return;
                }
                view.Activate(cue.Key);
                active.Add(cue.Key, view);
            }

            view.Render(cue, songTimeSeconds);
        }

        public void EndFrame()
        {
            releaseScratch.Clear();
            foreach (KeyValuePair<RadialCombatVfxKey, RadialCombatVfxView> pair in active)
            {
                if (!desired.Contains(pair.Key))
                {
                    releaseScratch.Add(pair.Key);
                }
            }

            for (int i = 0; i < releaseScratch.Count; i++)
            {
                RadialCombatVfxKey key = releaseScratch[i];
                if (active.TryGetValue(key, out RadialCombatVfxView view))
                {
                    view.ResetView();
                    active.Remove(key);
                }
            }
        }

        public void ResetView()
        {
            desired.Clear();
            active.Clear();
            releaseScratch.Clear();
            poolWarningLogged = false;
            if (pool == null)
            {
                return;
            }
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i]?.ResetView();
            }
        }

        private RadialCombatVfxView FindFreeView()
        {
            if (pool == null)
            {
                return null;
            }
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] != null && !pool[i].IsInUse)
                {
                    return pool[i];
                }
            }
            return null;
        }
    }

    public sealed class RadialCombatVfxView : MonoBehaviour
    {
        [SerializeField] private RectTransform viewRoot;
        [SerializeField] private RadialCombatVfxGraphic graphic;

        public RadialCombatVfxKey Key { get; private set; }
        public bool IsInUse { get; private set; }

        public void Configure(RectTransform root, RadialCombatVfxGraphic value)
        {
            viewRoot = root;
            graphic = value;
        }

        public void Activate(RadialCombatVfxKey key)
        {
            Key = key;
            IsInUse = true;
            gameObject.SetActive(true);
        }

        public void Render(RadialCombatVfxCue cue, double songTimeSeconds)
        {
            float progress = cue.Progress(songTimeSeconds);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            float animationScale = cue.Key.Kind == RadialCombatVfxKind.HeavyImpact
                || cue.Key.Kind == RadialCombatVfxKind.BreakFinal
                    ? Mathf.Lerp(0.68f, 1.12f, eased)
                    : Mathf.Lerp(0.84f, 1.04f, eased);
            viewRoot.anchoredPosition = cue.Position;
            viewRoot.localRotation = Quaternion.Euler(0f, 0f, cue.RotationDegrees);
            viewRoot.localScale = Vector3.one * cue.Scale * animationScale;
            graphic.SetVisual(cue.Key.Kind, progress, cue.Intensity);
        }

        public void ResetView()
        {
            Key = default(RadialCombatVfxKey);
            IsInUse = false;
            if (viewRoot != null)
            {
                viewRoot.anchoredPosition = Vector2.zero;
                viewRoot.localRotation = Quaternion.identity;
                viewRoot.localScale = Vector3.one;
            }
            graphic?.SetVisual(RadialCombatVfxKind.CoreBurst, 1f, 0f);
            gameObject.SetActive(false);
        }
    }

    public sealed class RadialCombatVfxGraphic : MaskableGraphic
    {
        private RadialCombatVfxKind kind;
        private float progress = 1f;
        private float intensity;

        public void SetVisual(
            RadialCombatVfxKind value,
            float normalizedProgress,
            float normalizedIntensity)
        {
            kind = value;
            progress = Mathf.Clamp01(normalizedProgress);
            intensity = Mathf.Clamp01(normalizedIntensity);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (intensity <= 0.001f)
            {
                return;
            }

            float fade = Mathf.SmoothStep(1f, 0f, progress);
            float alpha = fade * intensity;
            Vector2 center = rectTransform.rect.center;
            switch (kind)
            {
                case RadialCombatVfxKind.GuardArc:
                    DrawGuard(vertexHelper, center, alpha);
                    break;
                case RadialCombatVfxKind.CoreBurst:
                    DrawCoreBurst(vertexHelper, center, alpha);
                    break;
                case RadialCombatVfxKind.DodgeAfterimage:
                    DrawDodge(vertexHelper, center, alpha);
                    break;
                case RadialCombatVfxKind.LightSlash:
                    DrawLightSlash(vertexHelper, center, alpha);
                    break;
                case RadialCombatVfxKind.HeavyImpact:
                    DrawHeavyImpact(vertexHelper, center, alpha);
                    break;
                case RadialCombatVfxKind.MissImpact:
                    DrawMiss(vertexHelper, center, alpha);
                    break;
                case RadialCombatVfxKind.ArmorBreak:
                    DrawArmorBreak(vertexHelper, center, alpha);
                    break;
                case RadialCombatVfxKind.BreakSegment:
                    DrawBreak(vertexHelper, center, alpha, false);
                    break;
                case RadialCombatVfxKind.BreakFinal:
                    DrawBreak(vertexHelper, center, alpha, true);
                    break;
                case RadialCombatVfxKind.ProjectileDeflect:
                    DrawDeflect(vertexHelper, center, alpha);
                    break;
            }
        }

        private void DrawGuard(VertexHelper vh, Vector2 center, float alpha)
        {
            Color cyan = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Guard, alpha * 0.92f);
            AddArc(vh, center, 66f + progress * 18f, 9f, -118f, 118f, cyan, 24);
            AddArc(
                vh,
                center,
                53f + progress * 12f,
                3f,
                -108f,
                108f,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.PrimaryText, alpha * 0.50f),
                20);
        }

        private void DrawCoreBurst(VertexHelper vh, Vector2 center, float alpha)
        {
            float radius = 18f + progress * 52f;
            AddRing(vh, center, radius + 5f, radius, PulseForgeUITheme.WithAlpha(
                PulseForgeUITheme.Guard,
                alpha * 0.76f), 32);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                AddQuad(vh, center + direction * (20f + progress * 20f), direction, 18f, 3f,
                    PulseForgeUITheme.WithAlpha(PulseForgeUITheme.PrimaryText, alpha * 0.42f));
            }
        }

        private void DrawDodge(VertexHelper vh, Vector2 center, float alpha)
        {
            for (int i = 0; i < 3; i++)
            {
                float offset = -34f + i * 28f + progress * 32f;
                Color color = PulseForgeUITheme.WithAlpha(
                    Color.Lerp(PulseForgeUITheme.Primary, PulseForgeUITheme.Guard, 0.38f),
                    alpha * (0.24f + i * 0.16f));
                AddQuad(vh, center + new Vector2(offset, 0f), Vector2.up, 68f, 14f, color);
                AddDiamond(vh, center + new Vector2(offset, 18f), 13f, 18f, color);
            }
        }

        private void DrawLightSlash(VertexHelper vh, Vector2 center, float alpha)
        {
            Color hot = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Strike, alpha * 0.96f);
            Vector2 direction = new Vector2(0.82f, 0.57f).normalized;
            AddQuad(vh, center, direction, 116f, 7f, hot);
            AddQuad(vh, center - direction * 5f, direction, 82f, 2.5f,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Perfect, alpha * 0.84f));
        }

        private void DrawHeavyImpact(VertexHelper vh, Vector2 center, float alpha)
        {
            float radius = 38f + progress * 46f;
            Color ember = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Strike, alpha * 0.88f);
            AddRing(vh, center, radius + 10f, radius, ember, 40);
            AddRing(vh, center, radius * 0.58f + 5f, radius * 0.58f,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Perfect, alpha * 0.64f), 32);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                AddQuad(vh, center + direction * radius * 0.88f, direction, 28f, 6f, ember);
            }
        }

        private void DrawMiss(VertexHelper vh, Vector2 center, float alpha)
        {
            Color red = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Miss, alpha * 0.88f);
            AddRing(vh, center, 32f + progress * 34f, 26f + progress * 34f, red, 32);
            AddQuad(vh, center, new Vector2(0.707f, 0.707f), 68f, 7f, red);
            AddQuad(vh, center, new Vector2(0.707f, -0.707f), 68f, 7f, red);
        }

        private void DrawArmorBreak(VertexHelper vh, Vector2 center, float alpha)
        {
            Color metal = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.PrimaryText, alpha * 0.62f);
            Color edge = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Strike, alpha * 0.92f);
            AddTriangle(vh, center + new Vector2(-24f - progress * 20f, 10f),
                new Vector2(-14f, 26f), new Vector2(16f, 10f), new Vector2(-8f, -20f), metal);
            AddTriangle(vh, center + new Vector2(24f + progress * 20f, -8f),
                new Vector2(-16f, 18f), new Vector2(18f, 24f), new Vector2(10f, -24f), metal);
            AddQuad(vh, center, new Vector2(0.45f, 0.89f), 78f, 5f, edge);
        }

        private void DrawBreak(VertexHelper vh, Vector2 center, float alpha, bool final)
        {
            int count = final ? 10 : 5;
            float reach = final ? 76f : 48f;
            Color shard = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Strike, alpha * 0.90f);
            for (int i = 0; i < count; i++)
            {
                float angle = (i * (360f / count) + 12f) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 origin = center + direction * Mathf.Lerp(18f, reach, progress);
                AddTriangle(vh, origin, -direction * 8f + Vector2.up * 4f,
                    direction * (final ? 14f : 10f), -direction * 8f - Vector2.up * 4f, shard);
            }
            if (final)
            {
                AddRing(vh, center, 42f + progress * 46f, 36f + progress * 46f,
                    PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Perfect, alpha * 0.62f), 36);
            }
        }

        private void DrawDeflect(VertexHelper vh, Vector2 center, float alpha)
        {
            Color cyan = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Guard, alpha);
            AddRing(vh, center, 24f + progress * 18f, 18f + progress * 18f, cyan, 24);
            AddQuad(vh, center + new Vector2(30f, 18f) * progress,
                new Vector2(0.80f, 0.60f), 72f, 7f, cyan);
        }

        private static void AddArc(
            VertexHelper vh,
            Vector2 center,
            float radius,
            float thickness,
            float startDegrees,
            float endDegrees,
            Color color,
            int segments)
        {
            int start = vh.currentVertCount;
            float inner = Mathf.Max(0f, radius - thickness);
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(startDegrees, endDegrees, i / (float)segments)
                    * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vh.AddVert(center + direction * radius, color, Vector2.zero);
                vh.AddVert(center + direction * inner, color, Vector2.zero);
            }
            AddStripTriangles(vh, start, segments);
        }

        private static void AddRing(
            VertexHelper vh,
            Vector2 center,
            float outer,
            float inner,
            Color color,
            int segments)
        {
            int start = vh.currentVertCount;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vh.AddVert(center + direction * outer, color, Vector2.zero);
                vh.AddVert(center + direction * inner, color, Vector2.zero);
            }
            AddStripTriangles(vh, start, segments);
        }

        private static void AddStripTriangles(VertexHelper vh, int start, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                int index = start + i * 2;
                vh.AddTriangle(index, index + 2, index + 3);
                vh.AddTriangle(index, index + 3, index + 1);
            }
        }

        private static void AddQuad(
            VertexHelper vh,
            Vector2 center,
            Vector2 direction,
            float length,
            float width,
            Color color)
        {
            Vector2 along = direction.normalized * length * 0.5f;
            Vector2 across = new Vector2(-direction.y, direction.x).normalized * width * 0.5f;
            int start = vh.currentVertCount;
            vh.AddVert(center - along - across, color, Vector2.zero);
            vh.AddVert(center - along + across, color, Vector2.zero);
            vh.AddVert(center + along + across, color, Vector2.zero);
            vh.AddVert(center + along - across, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddDiamond(
            VertexHelper vh,
            Vector2 center,
            float halfWidth,
            float halfHeight,
            Color color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(center + Vector2.up * halfHeight, color, Vector2.zero);
            vh.AddVert(center + Vector2.right * halfWidth, color, Vector2.zero);
            vh.AddVert(center + Vector2.down * halfHeight, color, Vector2.zero);
            vh.AddVert(center + Vector2.left * halfWidth, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddTriangle(
            VertexHelper vh,
            Vector2 center,
            Vector2 a,
            Vector2 b,
            Vector2 c,
            Color color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(center + a, color, Vector2.zero);
            vh.AddVert(center + b, color, Vector2.zero);
            vh.AddVert(center + c, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
        }
    }
}
