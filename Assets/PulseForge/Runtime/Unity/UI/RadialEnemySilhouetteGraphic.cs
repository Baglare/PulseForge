using PulseForge.Domain.Rhythm;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class RadialEnemySilhouetteGraphic : MaskableGraphic
    {
        [SerializeField] private EnemyArchetype archetype;
        [SerializeField] private Color accent = Color.white;

        public void Configure(EnemyArchetype value, Color accentColor)
        {
            archetype = value;
            accent = accentColor;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect bounds = rectTransform.rect;
            Color metal = Color.Lerp(
                PulseForgeUITheme.SurfaceRaised,
                PulseForgeUITheme.PrimaryText,
                0.12f);
            Color darkMetal = Color.Lerp(
                PulseForgeUITheme.BackgroundSecondary,
                PulseForgeUITheme.SurfaceRaised,
                0.52f);
            Color edge = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.SecondaryText, 0.84f);
            Color energy = Color.Lerp(PulseForgeUITheme.Strike, accent, 0.18f);

            switch (archetype)
            {
                case EnemyArchetype.Duelist:
                    DrawDuelist(vertexHelper, bounds, metal, edge, energy);
                    break;
                case EnemyArchetype.Brute:
                    DrawBrute(vertexHelper, bounds, metal, darkMetal, energy);
                    break;
                case EnemyArchetype.Raider:
                    DrawRaider(vertexHelper, bounds, metal, edge, energy);
                    break;
                case EnemyArchetype.Armored:
                    DrawArmored(vertexHelper, bounds, metal, darkMetal, energy);
                    break;
                case EnemyArchetype.ArcherGunner:
                    DrawRanged(vertexHelper, bounds, metal, edge, energy);
                    break;
                case EnemyArchetype.Swarm:
                    DrawSwarm(vertexHelper, bounds, metal, energy);
                    break;
                case EnemyArchetype.GiantBreaker:
                    DrawGiant(vertexHelper, bounds, metal, darkMetal, energy);
                    break;
                case EnemyArchetype.Saboteur:
                    DrawSaboteur(vertexHelper, bounds, metal, darkMetal, energy);
                    break;
            }
        }

        private static void DrawDuelist(
            VertexHelper vh,
            Rect rect,
            Color metal,
            Color edge,
            Color energy)
        {
            AddPolygon(vh, rect, metal,
                new Vector2(-0.12f, -0.36f), new Vector2(-0.20f, 0.18f),
                new Vector2(-0.05f, 0.35f), new Vector2(0.12f, 0.16f),
                new Vector2(0.16f, -0.34f));
            AddCircle(vh, rect, new Vector2(-0.04f, 0.36f), 0.10f, edge, 14);
            AddLine(vh, rect, new Vector2(-0.14f, -0.25f), new Vector2(-0.18f, -0.48f), 0.055f, edge);
            AddLine(vh, rect, new Vector2(0.08f, -0.24f), new Vector2(0.18f, -0.48f), 0.055f, edge);
            AddLine(vh, rect, new Vector2(0.06f, 0.08f), new Vector2(0.42f, 0.49f), 0.045f, edge);
            AddLine(vh, rect, new Vector2(0.35f, 0.42f), new Vector2(0.46f, 0.50f), 0.022f, energy);
        }

        private static void DrawBrute(
            VertexHelper vh,
            Rect rect,
            Color metal,
            Color darkMetal,
            Color energy)
        {
            AddPolygon(vh, rect, darkMetal,
                new Vector2(-0.42f, 0.20f), new Vector2(-0.28f, 0.36f),
                new Vector2(0.27f, 0.36f), new Vector2(0.43f, 0.18f),
                new Vector2(0.31f, -0.40f), new Vector2(-0.31f, -0.40f));
            AddQuad(vh, rect, new Vector2(-0.40f, 0.10f), new Vector2(0.40f, 0.30f), metal);
            AddCircle(vh, rect, new Vector2(0f, 0.37f), 0.13f, metal, 14);
            AddLine(vh, rect, new Vector2(0.24f, 0.15f), new Vector2(0.44f, -0.42f), 0.07f, metal);
            AddQuad(vh, rect, new Vector2(0.25f, -0.48f), new Vector2(0.50f, -0.30f), darkMetal);
            AddQuad(vh, rect, new Vector2(-0.19f, 0.10f), new Vector2(0.19f, 0.16f), energy);
        }

        private static void DrawRaider(
            VertexHelper vh,
            Rect rect,
            Color metal,
            Color edge,
            Color energy)
        {
            AddPolygon(vh, rect, metal,
                new Vector2(-0.34f, -0.18f), new Vector2(-0.10f, 0.31f),
                new Vector2(0.30f, 0.13f), new Vector2(0.40f, -0.28f),
                new Vector2(-0.02f, -0.39f));
            AddCircle(vh, rect, new Vector2(0.05f, 0.32f), 0.10f, edge, 12);
            AddLine(vh, rect, new Vector2(-0.16f, 0.03f), new Vector2(-0.45f, 0.36f), 0.045f, edge);
            AddLine(vh, rect, new Vector2(0.24f, 0.02f), new Vector2(0.48f, 0.29f), 0.045f, edge);
            AddPolygon(vh, rect, energy,
                new Vector2(0.01f, 0.36f), new Vector2(0.13f, 0.36f),
                new Vector2(0.08f, 0.30f));
        }

        private static void DrawArmored(
            VertexHelper vh,
            Rect rect,
            Color metal,
            Color darkMetal,
            Color energy)
        {
            AddPolygon(vh, rect, darkMetal,
                new Vector2(-0.38f, -0.37f), new Vector2(-0.45f, 0.20f),
                new Vector2(-0.25f, 0.43f), new Vector2(0.25f, 0.43f),
                new Vector2(0.45f, 0.20f), new Vector2(0.38f, -0.37f));
            AddQuad(vh, rect, new Vector2(-0.48f, 0.12f), new Vector2(-0.25f, 0.34f), metal);
            AddQuad(vh, rect, new Vector2(0.25f, 0.12f), new Vector2(0.48f, 0.34f), metal);
            AddQuad(vh, rect, new Vector2(-0.27f, -0.29f), new Vector2(0.27f, 0.22f), metal);
            AddQuad(vh, rect, new Vector2(-0.23f, 0.30f), new Vector2(0.23f, 0.39f), energy);
            AddLine(vh, rect, new Vector2(0f, -0.30f), new Vector2(0f, 0.20f), 0.028f, darkMetal);
        }

        private static void DrawRanged(
            VertexHelper vh,
            Rect rect,
            Color metal,
            Color edge,
            Color energy)
        {
            AddPolygon(vh, rect, metal,
                new Vector2(-0.19f, -0.39f), new Vector2(-0.16f, 0.25f),
                new Vector2(0.08f, 0.34f), new Vector2(0.20f, -0.34f));
            AddCircle(vh, rect, new Vector2(-0.04f, 0.38f), 0.09f, edge, 12);
            AddLine(vh, rect, new Vector2(-0.08f, 0.11f), new Vector2(0.45f, 0.13f), 0.07f, edge);
            AddQuad(vh, rect, new Vector2(0.18f, 0.07f), new Vector2(0.47f, 0.18f), metal);
            AddQuad(vh, rect, new Vector2(-0.35f, -0.08f), new Vector2(-0.17f, 0.25f), edge);
            AddCircle(vh, rect, new Vector2(0.48f, 0.13f), 0.045f, energy, 10);
        }

        private static void DrawSwarm(
            VertexHelper vh,
            Rect rect,
            Color metal,
            Color energy)
        {
            DrawSwarmUnit(vh, rect, new Vector2(-0.25f, 0.12f), 0.18f, metal, energy);
            DrawSwarmUnit(vh, rect, new Vector2(0.24f, 0.18f), 0.16f, metal, energy);
            DrawSwarmUnit(vh, rect, new Vector2(0f, -0.25f), 0.20f, metal, energy);
        }

        private static void DrawSwarmUnit(
            VertexHelper vh,
            Rect rect,
            Vector2 center,
            float size,
            Color metal,
            Color energy)
        {
            AddPolygon(vh, rect, metal,
                center + new Vector2(0f, size),
                center + new Vector2(size, 0f),
                center + new Vector2(0f, -size),
                center + new Vector2(-size, 0f));
            AddCircle(vh, rect, center + new Vector2(0f, size * 0.08f), size * 0.22f, energy, 8);
        }

        private static void DrawGiant(
            VertexHelper vh,
            Rect rect,
            Color metal,
            Color darkMetal,
            Color energy)
        {
            AddPolygon(vh, rect, darkMetal,
                new Vector2(-0.48f, -0.45f), new Vector2(-0.44f, 0.29f),
                new Vector2(-0.24f, 0.48f), new Vector2(0.24f, 0.48f),
                new Vector2(0.44f, 0.29f), new Vector2(0.48f, -0.45f));
            AddQuad(vh, rect, new Vector2(-0.50f, 0.12f), new Vector2(-0.27f, 0.39f), metal);
            AddQuad(vh, rect, new Vector2(0.27f, 0.12f), new Vector2(0.50f, 0.39f), metal);
            AddQuad(vh, rect, new Vector2(-0.33f, -0.34f), new Vector2(0.33f, 0.24f), metal);
            AddQuad(vh, rect, new Vector2(-0.28f, 0.32f), new Vector2(0.28f, 0.44f), energy);
            AddQuad(vh, rect, new Vector2(-0.49f, -0.46f), new Vector2(-0.28f, -0.30f), metal);
            AddQuad(vh, rect, new Vector2(0.28f, -0.46f), new Vector2(0.49f, -0.30f), metal);
        }

        private static void DrawSaboteur(
            VertexHelper vh,
            Rect rect,
            Color metal,
            Color darkMetal,
            Color energy)
        {
            AddPolygon(vh, rect, metal,
                new Vector2(-0.31f, -0.40f), new Vector2(-0.35f, 0.16f),
                new Vector2(-0.08f, 0.41f), new Vector2(0.18f, 0.23f),
                new Vector2(0.24f, -0.36f));
            AddCircle(vh, rect, new Vector2(-0.12f, 0.40f), 0.10f, darkMetal, 12);
            AddQuad(vh, rect, new Vector2(0.15f, -0.18f), new Vector2(0.46f, 0.18f), darkMetal);
            AddCircle(vh, rect, new Vector2(0.33f, 0.04f), 0.17f, darkMetal, 16);
            AddCircle(vh, rect, new Vector2(0.33f, 0.04f), 0.07f, energy, 10);
            AddLine(vh, rect, new Vector2(0.34f, 0.21f), new Vector2(0.20f, 0.45f), 0.035f, energy);
            AddCircle(vh, rect, new Vector2(0.18f, 0.47f), 0.045f, PulseForgeUITheme.Perfect, 8);
            AddPolygon(vh, rect, darkMetal,
                new Vector2(-0.35f, 0.13f), new Vector2(-0.49f, -0.08f),
                new Vector2(-0.32f, -0.19f));
        }

        private static void AddQuad(
            VertexHelper vh,
            Rect rect,
            Vector2 min,
            Vector2 max,
            Color color)
        {
            AddPolygon(vh, rect, color,
                new Vector2(min.x, min.y), new Vector2(min.x, max.y),
                new Vector2(max.x, max.y), new Vector2(max.x, min.y));
        }

        private static void AddLine(
            VertexHelper vh,
            Rect rect,
            Vector2 from,
            Vector2 to,
            float width,
            Color color)
        {
            Vector2 direction = (to - from).normalized;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x) * (width * 0.5f);
            AddPolygon(vh, rect, color,
                from - perpendicular, from + perpendicular,
                to + perpendicular, to - perpendicular);
        }

        private static void AddCircle(
            VertexHelper vh,
            Rect rect,
            Vector2 normalizedCenter,
            float normalizedRadius,
            Color color,
            int segments)
        {
            Vector2 center = ToLocal(rect, normalizedCenter);
            float radius = normalizedRadius * Mathf.Min(rect.width, rect.height);
            int start = vh.currentVertCount;
            vh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                vh.AddVert(
                    center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    color,
                    Vector2.zero);
            }

            for (int i = 0; i < segments; i++)
            {
                vh.AddTriangle(start, start + i + 1, start + i + 2);
            }
        }

        private static void AddPolygon(
            VertexHelper vh,
            Rect rect,
            Color color,
            params Vector2[] normalizedPoints)
        {
            if (normalizedPoints == null || normalizedPoints.Length < 3)
            {
                return;
            }

            int start = vh.currentVertCount;
            for (int i = 0; i < normalizedPoints.Length; i++)
            {
                vh.AddVert(ToLocal(rect, normalizedPoints[i]), color, Vector2.zero);
            }
            for (int i = 1; i < normalizedPoints.Length - 1; i++)
            {
                vh.AddTriangle(start, start + i, start + i + 1);
            }
        }

        private static Vector2 ToLocal(Rect rect, Vector2 normalized)
        {
            return rect.center + new Vector2(
                normalized.x * rect.width,
                normalized.y * rect.height);
        }
    }
}
