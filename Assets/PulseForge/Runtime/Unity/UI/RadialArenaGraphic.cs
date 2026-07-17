using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class RadialArenaGraphic : MaskableGraphic
    {
        private const int DirectionCount = 8;
        private const int CircleSegments = 96;

        [SerializeField] private float outerRadius = 360f;
        [SerializeField] private float judgementRadius = 155f;

        private readonly float[] directionEmphasis = new float[DirectionCount];

        public void Configure(float stageOuterRadius, float stageJudgementRadius)
        {
            outerRadius = Mathf.Max(220f, stageOuterRadius);
            judgementRadius = Mathf.Clamp(stageJudgementRadius, 80f, outerRadius - 80f);
            raycastTarget = false;
            SetVerticesDirty();
        }

        public void SetDirectionEmphasis(float[] values)
        {
            bool changed = false;
            for (int i = 0; i < DirectionCount; i++)
            {
                float value = values == null || i >= values.Length
                    ? 0f
                    : Mathf.Clamp01(values[i]);
                if (Mathf.Abs(directionEmphasis[i] - value) < 0.01f)
                {
                    continue;
                }

                directionEmphasis[i] = value;
                changed = true;
            }

            if (changed)
            {
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Vector2 center = rectTransform.rect.center;
            float platformRadius = outerRadius + 42f;

            AddCircle(
                vertexHelper,
                center,
                platformRadius + 24f,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Backdrop, 0.70f),
                CircleSegments);
            AddRing(
                vertexHelper,
                center,
                platformRadius + 18f,
                platformRadius + 8f,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Strike, 0.10f),
                CircleSegments);
            AddCircle(
                vertexHelper,
                center,
                platformRadius,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Surface, 0.96f),
                CircleSegments);

            float panelInner = judgementRadius + 34f;
            for (int i = 0; i < DirectionCount; i++)
            {
                float angle = DirectionAngle(i);
                Color panelColor = i % 2 == 0
                    ? PulseForgeUITheme.WithAlpha(PulseForgeUITheme.SurfaceRaised, 0.58f)
                    : PulseForgeUITheme.WithAlpha(PulseForgeUITheme.SurfaceSoft, 0.44f);
                AddWedge(
                    vertexHelper,
                    center,
                    panelInner,
                    platformRadius - 14f,
                    angle - 19f * Mathf.Deg2Rad,
                    angle + 19f * Mathf.Deg2Rad,
                    panelColor,
                    8);
            }

            AddRing(
                vertexHelper,
                center,
                platformRadius,
                platformRadius - 7f,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Border, 0.92f),
                CircleSegments);
            AddRing(
                vertexHelper,
                center,
                outerRadius + 16f,
                outerRadius + 11f,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Strike, 0.34f),
                CircleSegments);
            AddRing(
                vertexHelper,
                center,
                panelInner,
                panelInner - 5f,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Divider, 0.80f),
                CircleSegments);
            AddCircle(
                vertexHelper,
                center,
                panelInner - 7f,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.BackgroundSecondary, 0.88f),
                CircleSegments);

            float laneStart = judgementRadius + 16f;
            for (int i = 0; i < DirectionCount; i++)
            {
                Vector2 direction = DirectionVector(i);
                float emphasis = directionEmphasis[i];
                Color channelColor = Color.Lerp(
                    PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Divider, 0.36f),
                    PulseForgeUITheme.WithAlpha(PulseForgeUITheme.AccentSoft, 0.68f),
                    emphasis);
                Color energyColor = Color.Lerp(
                    PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Primary, 0.12f),
                    PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Primary, 0.72f),
                    emphasis);

                AddRadialQuad(
                    vertexHelper,
                    center,
                    direction,
                    laneStart,
                    outerRadius + 8f,
                    30f,
                    PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Backdrop, 0.72f));
                AddRadialQuad(
                    vertexHelper,
                    center,
                    direction,
                    laneStart + 2f,
                    outerRadius + 5f,
                    21f,
                    channelColor);
                AddRadialQuad(
                    vertexHelper,
                    center,
                    direction,
                    laneStart + 5f,
                    outerRadius,
                    emphasis > 0.01f ? 3.5f : 2f,
                    energyColor);

                AddCircle(
                    vertexHelper,
                    center + direction * (outerRadius + 23f),
                    emphasis > 0.01f ? 5f : 3.5f,
                    Color.Lerp(
                        PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Strike, 0.40f),
                        PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Primary, 0.90f),
                        emphasis),
                    12);
            }

            AddRing(
                vertexHelper,
                center,
                judgementRadius + 11f,
                judgementRadius + 4f,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Border, 0.88f),
                CircleSegments);
            AddRing(
                vertexHelper,
                center,
                judgementRadius - 8f,
                judgementRadius - 12f,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Primary, 0.12f),
                CircleSegments);
        }

        private static float DirectionAngle(int index)
        {
            return (90f - index * 45f) * Mathf.Deg2Rad;
        }

        private static Vector2 DirectionVector(int index)
        {
            float angle = DirectionAngle(index);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private static void AddCircle(
            VertexHelper vertexHelper,
            Vector2 center,
            float radius,
            Color color,
            int segments)
        {
            int start = vertexHelper.currentVertCount;
            vertexHelper.AddVert(center, color, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                vertexHelper.AddVert(
                    center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    color,
                    Vector2.zero);
            }

            for (int i = 0; i < segments; i++)
            {
                vertexHelper.AddTriangle(start, start + i + 1, start + i + 2);
            }
        }

        private static void AddRing(
            VertexHelper vertexHelper,
            Vector2 center,
            float outerRadius,
            float innerRadius,
            Color color,
            int segments)
        {
            int start = vertexHelper.currentVertCount;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertexHelper.AddVert(center + direction * outerRadius, color, Vector2.zero);
                vertexHelper.AddVert(center + direction * innerRadius, color, Vector2.zero);
            }

            for (int i = 0; i < segments; i++)
            {
                int index = start + i * 2;
                vertexHelper.AddTriangle(index, index + 2, index + 3);
                vertexHelper.AddTriangle(index, index + 3, index + 1);
            }
        }

        private static void AddWedge(
            VertexHelper vertexHelper,
            Vector2 center,
            float innerRadius,
            float outerRadius,
            float startAngle,
            float endAngle,
            Color color,
            int segments)
        {
            int start = vertexHelper.currentVertCount;
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(startAngle, endAngle, i / (float)segments);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertexHelper.AddVert(center + direction * outerRadius, color, Vector2.zero);
                vertexHelper.AddVert(center + direction * innerRadius, color, Vector2.zero);
            }

            for (int i = 0; i < segments; i++)
            {
                int index = start + i * 2;
                vertexHelper.AddTriangle(index, index + 2, index + 3);
                vertexHelper.AddTriangle(index, index + 3, index + 1);
            }
        }

        private static void AddRadialQuad(
            VertexHelper vertexHelper,
            Vector2 center,
            Vector2 direction,
            float startDistance,
            float endDistance,
            float width,
            Color color)
        {
            Vector2 perpendicular = new Vector2(-direction.y, direction.x) * (width * 0.5f);
            Vector2 start = center + direction * startDistance;
            Vector2 end = center + direction * endDistance;
            AddQuad(
                vertexHelper,
                start - perpendicular,
                start + perpendicular,
                end + perpendicular,
                end - perpendicular,
                color);
        }

        private static void AddQuad(
            VertexHelper vertexHelper,
            Vector2 first,
            Vector2 second,
            Vector2 third,
            Vector2 fourth,
            Color color)
        {
            int start = vertexHelper.currentVertCount;
            vertexHelper.AddVert(first, color, Vector2.zero);
            vertexHelper.AddVert(second, color, Vector2.zero);
            vertexHelper.AddVert(third, color, Vector2.zero);
            vertexHelper.AddVert(fourth, color, Vector2.zero);
            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start, start + 2, start + 3);
        }
    }
}
