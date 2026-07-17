using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class PulseForgeRingGraphic : MaskableGraphic
    {
        [SerializeField, Range(0.1f, 0.95f)] private float innerRadius = 0.72f;
        [SerializeField, Range(12, 96)] private int segments = 64;

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect bounds = rectTransform.rect;
            float outer = Mathf.Min(bounds.width, bounds.height) * 0.5f;
            float inner = outer * innerRadius;
            Vector2 center = bounds.center;
            for (int i = 0; i <= segments; i++)
            {
                float radians = (i / (float)segments) * Mathf.PI * 2f;
                Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                vertexHelper.AddVert(center + (direction * outer), color, Vector2.zero);
                vertexHelper.AddVert(center + (direction * inner), color, Vector2.zero);
            }
            for (int i = 0; i < segments; i++)
            {
                int outerStart = i * 2;
                int innerStart = outerStart + 1;
                int outerEnd = outerStart + 2;
                int innerEnd = outerStart + 3;
                vertexHelper.AddTriangle(outerStart, outerEnd, innerEnd);
                vertexHelper.AddTriangle(outerStart, innerEnd, innerStart);
            }
        }
    }
}
