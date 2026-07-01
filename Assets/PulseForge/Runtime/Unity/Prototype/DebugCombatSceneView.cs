using PulseForge.Domain.Rhythm;
using UnityEngine;

namespace PulseForge.Runtime.Unity.Prototype
{
    public sealed class DebugCombatSceneView : MonoBehaviour
    {
        private const float DefaultFeedbackDurationSeconds = 0.35f;
        private const float PerfectFeedbackDurationSeconds = 0.45f;
        private const int CharacterSortingOrder = 10;
        private const int EffectSortingOrder = 12;
        private const int TextSortingOrder = 14;

        private static readonly Color PlayerBaseColor = new Color(0.20f, 0.42f, 0.95f, 1f);
        private static readonly Color EnemyBaseColor = new Color(0.92f, 0.25f, 0.22f, 1f);
        private static readonly Color ParryColor = new Color(0.20f, 0.92f, 1f, 1f);
        private static readonly Color SlashColor = new Color(1f, 0.82f, 0.20f, 1f);
        private static readonly Color MissColor = new Color(1f, 0.32f, 0.18f, 1f);

        private SpriteRenderer playerRenderer;
        private SpriteRenderer enemyRenderer;
        private SpriteRenderer parryFlashRenderer;
        private SpriteRenderer slashLineRenderer;
        private TextMesh feedbackText;
        private Sprite generatedSprite;
        private Texture2D generatedTexture;
        private FeedbackKind feedbackKind;
        private HitGrade feedbackGrade;
        private float feedbackDurationSeconds;
        private float feedbackRemainingSeconds;

        private enum FeedbackKind
        {
            None,
            Parry,
            Slash,
            Miss
        }

        public void ResetView()
        {
            EnsureViewObjects();
            feedbackKind = FeedbackKind.None;
            feedbackGrade = HitGrade.Miss;
            feedbackDurationSeconds = 0f;
            feedbackRemainingSeconds = 0f;
            ApplyHiddenFeedbackState();
        }

        public void ShowHit(RhythmAction action, HitGrade grade)
        {
            if (grade == HitGrade.Miss)
            {
                ShowMiss();
                return;
            }

            EnsureViewObjects();
            feedbackGrade = grade;
            feedbackDurationSeconds = grade == HitGrade.Perfect
                ? PerfectFeedbackDurationSeconds
                : DefaultFeedbackDurationSeconds;
            feedbackRemainingSeconds = feedbackDurationSeconds;

            if (action == RhythmAction.Guard)
            {
                feedbackKind = FeedbackKind.Parry;
                feedbackText.text = grade == HitGrade.Perfect ? "PERFECT PARRY" : "GOOD PARRY";
            }
            else if (action == RhythmAction.Strike)
            {
                feedbackKind = FeedbackKind.Slash;
                feedbackText.text = grade == HitGrade.Perfect ? "PERFECT SLASH" : "GOOD SLASH";
            }
            else
            {
                feedbackKind = FeedbackKind.None;
                feedbackText.text = grade.ToString().ToUpperInvariant();
            }

            UpdateFeedbackVisuals();
        }

        public void ShowMiss()
        {
            EnsureViewObjects();
            feedbackKind = FeedbackKind.Miss;
            feedbackGrade = HitGrade.Miss;
            feedbackDurationSeconds = DefaultFeedbackDurationSeconds;
            feedbackRemainingSeconds = feedbackDurationSeconds;
            feedbackText.text = "MISS / HIT TAKEN";
            UpdateFeedbackVisuals();
        }

        private void Awake()
        {
            EnsureViewObjects();
            ResetView();
        }

        private void Update()
        {
            if (feedbackKind == FeedbackKind.None)
            {
                return;
            }

            feedbackRemainingSeconds -= Time.deltaTime;
            if (feedbackRemainingSeconds <= 0f)
            {
                ResetView();
                return;
            }

            UpdateFeedbackVisuals();
        }

        private void OnDestroy()
        {
            if (generatedSprite != null)
            {
                Destroy(generatedSprite);
            }

            if (generatedTexture != null)
            {
                Destroy(generatedTexture);
            }
        }

        private void EnsureViewObjects()
        {
            Sprite sprite = GetOrCreateSprite();

            playerRenderer = GetOrCreateSpriteRenderer("Player", transform);
            playerRenderer.sprite = sprite;
            playerRenderer.sortingOrder = CharacterSortingOrder;
            playerRenderer.transform.localPosition = new Vector3(-2.25f, 0f, 0f);
            playerRenderer.transform.localRotation = Quaternion.identity;
            playerRenderer.transform.localScale = new Vector3(0.8f, 1.4f, 1f);

            enemyRenderer = GetOrCreateSpriteRenderer("Enemy", transform);
            enemyRenderer.sprite = sprite;
            enemyRenderer.sortingOrder = CharacterSortingOrder;
            enemyRenderer.transform.localPosition = new Vector3(2.25f, 0f, 0f);
            enemyRenderer.transform.localRotation = Quaternion.identity;
            enemyRenderer.transform.localScale = new Vector3(0.8f, 1.4f, 1f);

            Transform effectRoot = GetOrCreateChild("EffectRoot", transform);
            effectRoot.localPosition = Vector3.zero;
            effectRoot.localRotation = Quaternion.identity;
            effectRoot.localScale = Vector3.one;

            parryFlashRenderer = GetOrCreateSpriteRenderer("ParryFlash", effectRoot);
            parryFlashRenderer.sprite = sprite;
            parryFlashRenderer.sortingOrder = EffectSortingOrder;
            parryFlashRenderer.transform.localPosition = new Vector3(-1.65f, 0.35f, -0.05f);
            parryFlashRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            slashLineRenderer = GetOrCreateSpriteRenderer("SlashLine", effectRoot);
            slashLineRenderer.sprite = sprite;
            slashLineRenderer.sortingOrder = EffectSortingOrder;
            slashLineRenderer.transform.localPosition = new Vector3(2.25f, 0.12f, -0.05f);
            slashLineRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, -28f);

            feedbackText = GetOrCreateTextMesh("FeedbackText", effectRoot);
            feedbackText.transform.localPosition = new Vector3(0f, 1.65f, -0.1f);
            feedbackText.transform.localRotation = Quaternion.identity;
            feedbackText.transform.localScale = Vector3.one;
            feedbackText.anchor = TextAnchor.MiddleCenter;
            feedbackText.alignment = TextAlignment.Center;
            feedbackText.characterSize = 0.18f;
            feedbackText.fontSize = 48;

            MeshRenderer textRenderer = feedbackText.GetComponent<MeshRenderer>();
            if (textRenderer != null)
            {
                textRenderer.sortingOrder = TextSortingOrder;
            }
        }

        private Sprite GetOrCreateSprite()
        {
            if (generatedSprite != null)
            {
                return generatedSprite;
            }

            generatedTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            generatedTexture.SetPixel(0, 0, Color.white);
            generatedTexture.Apply();
            generatedTexture.wrapMode = TextureWrapMode.Clamp;
            generatedTexture.filterMode = FilterMode.Point;
            generatedSprite = Sprite.Create(
                generatedTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            return generatedSprite;
        }

        private static SpriteRenderer GetOrCreateSpriteRenderer(string childName, Transform parent)
        {
            Transform child = GetOrCreateChild(childName, parent);
            SpriteRenderer spriteRenderer = child.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = child.gameObject.AddComponent<SpriteRenderer>();
            }

            return spriteRenderer;
        }

        private static TextMesh GetOrCreateTextMesh(string childName, Transform parent)
        {
            Transform child = GetOrCreateChild(childName, parent);
            TextMesh textMesh = child.GetComponent<TextMesh>();
            if (textMesh == null)
            {
                textMesh = child.gameObject.AddComponent<TextMesh>();
            }

            return textMesh;
        }

        private static Transform GetOrCreateChild(string childName, Transform parent)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            GameObject childObject = new GameObject(childName);
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private void UpdateFeedbackVisuals()
        {
            float effectRatio = feedbackDurationSeconds <= 0f
                ? 0f
                : Mathf.Clamp01(feedbackRemainingSeconds / feedbackDurationSeconds);
            float alpha = Mathf.Clamp01(effectRatio);

            playerRenderer.color = PlayerBaseColor;
            enemyRenderer.color = EnemyBaseColor;
            parryFlashRenderer.gameObject.SetActive(feedbackKind == FeedbackKind.Parry);
            slashLineRenderer.gameObject.SetActive(feedbackKind == FeedbackKind.Slash);
            feedbackText.gameObject.SetActive(feedbackKind != FeedbackKind.None);
            feedbackText.color = ColorWithAlpha(GetFeedbackColor(), alpha);

            if (feedbackKind == FeedbackKind.Parry)
            {
                float scale = feedbackGrade == HitGrade.Perfect ? 0.72f : 0.5f;
                parryFlashRenderer.transform.localScale = new Vector3(scale, scale, 1f);
                parryFlashRenderer.color = ColorWithAlpha(ParryColor, alpha);
            }
            else if (feedbackKind == FeedbackKind.Slash)
            {
                float width = feedbackGrade == HitGrade.Perfect ? 1.55f : 1.15f;
                float height = feedbackGrade == HitGrade.Perfect ? 0.12f : 0.08f;
                slashLineRenderer.transform.localScale = new Vector3(width, height, 1f);
                slashLineRenderer.color = ColorWithAlpha(SlashColor, alpha);
                enemyRenderer.color = Color.Lerp(EnemyBaseColor, SlashColor, alpha);
            }
            else if (feedbackKind == FeedbackKind.Miss)
            {
                playerRenderer.color = Color.Lerp(PlayerBaseColor, MissColor, alpha);
            }
        }

        private void ApplyHiddenFeedbackState()
        {
            playerRenderer.color = PlayerBaseColor;
            enemyRenderer.color = EnemyBaseColor;
            parryFlashRenderer.gameObject.SetActive(false);
            slashLineRenderer.gameObject.SetActive(false);
            feedbackText.text = string.Empty;
            feedbackText.gameObject.SetActive(false);
        }

        private Color GetFeedbackColor()
        {
            if (feedbackKind == FeedbackKind.Parry)
            {
                return ParryColor;
            }

            if (feedbackKind == FeedbackKind.Slash)
            {
                return SlashColor;
            }

            if (feedbackKind == FeedbackKind.Miss)
            {
                return MissColor;
            }

            return Color.white;
        }

        private static Color ColorWithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
