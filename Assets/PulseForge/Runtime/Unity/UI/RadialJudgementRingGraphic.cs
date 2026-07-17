using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class RadialJudgementRingGraphic : MaskableGraphic
    {
        private const int Segments = 96;

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect bounds = rectTransform.rect;
            Vector2 center = bounds.center;
            float outerRadius = Mathf.Min(bounds.width, bounds.height) * 0.5f;

            AddRing(
                vertexHelper,
                center,
                outerRadius,
                outerRadius - 6f,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Border, 0.96f));
            AddRing(
                vertexHelper,
                center,
                outerRadius - 1f,
                outerRadius - 3.5f,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Strike, 0.86f));
            AddRing(
                vertexHelper,
                center,
                outerRadius - 8f,
                outerRadius - 11f,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Good, 0.48f));
            AddRing(
                vertexHelper,
                center,
                outerRadius - 13f,
                outerRadius - 16f,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Primary, 0.78f));

            for (int i = 0; i < 8; i++)
            {
                float angle = (90f - i * 45f) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 perpendicular = new Vector2(-direction.y, direction.x) * 2.5f;
                Vector2 inner = center + direction * (outerRadius - 18f);
                Vector2 outer = center + direction * (outerRadius + 2f);
                AddQuad(
                    vertexHelper,
                    inner - perpendicular,
                    inner + perpendicular,
                    outer + perpendicular,
                    outer - perpendicular,
                    i % 2 == 0
                        ? PulseForgeUITheme.Perfect
                        : PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Primary, 0.88f));
            }
        }

        private static void AddRing(
            VertexHelper vertexHelper,
            Vector2 center,
            float outerRadius,
            float innerRadius,
            Color color)
        {
            int start = vertexHelper.currentVertCount;
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i / (float)Segments * Mathf.PI * 2f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertexHelper.AddVert(center + direction * outerRadius, color, Vector2.zero);
                vertexHelper.AddVert(center + direction * innerRadius, color, Vector2.zero);
            }

            for (int i = 0; i < Segments; i++)
            {
                int index = start + i * 2;
                vertexHelper.AddTriangle(index, index + 2, index + 3);
                vertexHelper.AddTriangle(index, index + 3, index + 1);
            }
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
