using PulseForge.Domain.Rhythm;
using UnityEngine;

namespace PulseForge.Runtime.Unity.Prototype
{
    public sealed class DebugCombatSceneView : MonoBehaviour
    {
        private const int CharacterSortingOrder = 10;
        private const int EffectSortingOrder = 12;
        private const int TextSortingOrder = 14;

        [SerializeField] private Vector2 playerPosition = new Vector2(-2.2f, 0f);
        [SerializeField] private Vector2 enemyPosition = new Vector2(2.2f, 0f);
        [SerializeField] private Vector2 characterSize = new Vector2(0.8f, 1.4f);
        [SerializeField] private float feedbackDurationSeconds = 0.35f;
        [SerializeField] private float perfectEffectScale = 1.25f;
        [SerializeField] private float goodEffectScale = 1.0f;
        [SerializeField] private float minIntensityEffectScale = 0.75f;
        [SerializeField] private float maxIntensityEffectScale = 1.35f;
        [SerializeField] private float minShakeDistance = 0.08f;
        [SerializeField] private float maxShakeDistance = 0.22f;
        [SerializeField] private Color playerBaseColor = new Color(0.20f, 0.42f, 0.95f, 1f);
        [SerializeField] private Color enemyBaseColor = new Color(0.92f, 0.25f, 0.22f, 1f);
        [SerializeField] private Color parryColor = new Color(0.20f, 0.92f, 1f, 1f);
        [SerializeField] private Color slashColor = new Color(1f, 0.82f, 0.20f, 1f);
        [SerializeField] private Color missColor = new Color(1f, 0.32f, 0.18f, 1f);
        [SerializeField] private Color perfectColor = new Color(1f, 0.95f, 0.35f, 1f);
        [SerializeField] private Color goodColor = new Color(0.75f, 0.95f, 1f, 1f);

        private SpriteRenderer playerRenderer;
        private SpriteRenderer enemyRenderer;
        private SpriteRenderer[] parrySparkRenderers;
        private SpriteRenderer slashGlowRenderer;
        private SpriteRenderer slashCoreRenderer;
        private Transform visualRoot;
        private Transform parryEffectRoot;
        private Transform slashEffectRoot;
        private TextMesh playerLabel;
        private TextMesh enemyLabel;
        private TextMesh feedbackText;
        private Sprite generatedSprite;
        private Texture2D generatedTexture;
        private FeedbackKind feedbackKind;
        private HitGrade feedbackGrade;
        private float activeIntensity = 1f;
        private float activeFeedbackDurationSeconds;
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
            activeIntensity = 1f;
            activeFeedbackDurationSeconds = 0f;
            feedbackRemainingSeconds = 0f;
            ApplyHiddenFeedbackState();
        }

        public void SetVisible(bool isVisible)
        {
            EnsureViewObjects();
            if (visualRoot.gameObject.activeSelf != isVisible)
            {
                visualRoot.gameObject.SetActive(isVisible);
            }
        }

        public void ShowHit(RhythmAction action, HitGrade grade)
        {
            ShowHit(action, grade, 1f);
        }

        public void ShowHit(RhythmAction action, HitGrade grade, float intensity)
        {
            if (grade == HitGrade.Miss)
            {
                ShowMiss(intensity);
                return;
            }

            EnsureViewObjects();
            feedbackGrade = grade;
            activeIntensity = ClampIntensity(intensity);
            activeFeedbackDurationSeconds = GetConfiguredFeedbackDurationSeconds();
            feedbackRemainingSeconds = activeFeedbackDurationSeconds;

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
            ShowMiss(0.5f);
        }

        public void ShowMiss(float intensity)
        {
            EnsureViewObjects();
            feedbackKind = FeedbackKind.Miss;
            feedbackGrade = HitGrade.Miss;
            activeIntensity = ClampIntensity(intensity);
            activeFeedbackDurationSeconds = GetConfiguredFeedbackDurationSeconds();
            feedbackRemainingSeconds = activeFeedbackDurationSeconds;
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
            visualRoot = GetOrCreateChild("Combat Visual Root", transform);

            playerRenderer = GetOrCreateSpriteRenderer("Player", visualRoot);
            playerRenderer.sprite = sprite;
            playerRenderer.sortingOrder = CharacterSortingOrder;

            enemyRenderer = GetOrCreateSpriteRenderer("Enemy", visualRoot);
            enemyRenderer.sprite = sprite;
            enemyRenderer.sortingOrder = CharacterSortingOrder;

            playerLabel = GetOrCreateTextMesh("PlayerLabel", visualRoot);
            ConfigureLabel(playerLabel, "PLAYER");

            enemyLabel = GetOrCreateTextMesh("EnemyLabel", visualRoot);
            ConfigureLabel(enemyLabel, "ENEMY");

            parryEffectRoot = GetOrCreateChild("ParryEffect", visualRoot);
            parryEffectRoot.localRotation = Quaternion.identity;
            parrySparkRenderers = new[]
            {
                ConfigureEffectSprite("SparkCenter", parryEffectRoot, sprite, Vector2.one * 0.18f, 0f),
                ConfigureEffectSprite("SparkHorizontal", parryEffectRoot, sprite, new Vector2(0.72f, 0.06f), 0f),
                ConfigureEffectSprite("SparkVertical", parryEffectRoot, sprite, new Vector2(0.06f, 0.72f), 0f),
                ConfigureEffectSprite("SparkSlashA", parryEffectRoot, sprite, new Vector2(0.62f, 0.05f), 45f),
                ConfigureEffectSprite("SparkSlashB", parryEffectRoot, sprite, new Vector2(0.62f, 0.05f), -45f)
            };

            slashEffectRoot = GetOrCreateChild("SlashEffect", visualRoot);
            slashEffectRoot.localRotation = Quaternion.Euler(0f, 0f, -28f);
            slashGlowRenderer = ConfigureEffectSprite("SlashGlow", slashEffectRoot, sprite, new Vector2(1.55f, 0.18f), 0f);
            slashCoreRenderer = ConfigureEffectSprite("SlashCore", slashEffectRoot, sprite, new Vector2(1.32f, 0.07f), 0f);

            feedbackText = GetOrCreateTextMesh("FeedbackText", visualRoot);
            ConfigureFeedbackText();
            ApplyCharacterBaseState();
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

        private static SpriteRenderer ConfigureEffectSprite(
            string childName,
            Transform parent,
            Sprite sprite,
            Vector2 localScale,
            float zRotationDegrees)
        {
            SpriteRenderer spriteRenderer = GetOrCreateSpriteRenderer(childName, parent);
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = EffectSortingOrder;
            spriteRenderer.transform.localPosition = Vector3.zero;
            spriteRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, zRotationDegrees);
            spriteRenderer.transform.localScale = new Vector3(localScale.x, localScale.y, 1f);
            return spriteRenderer;
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

        private void ConfigureLabel(TextMesh label, string text)
        {
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.13f;
            label.fontSize = 36;
            label.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            SetTextSortingOrder(label, TextSortingOrder);
        }

        private void ConfigureFeedbackText()
        {
            feedbackText.transform.localRotation = Quaternion.identity;
            feedbackText.transform.localScale = Vector3.one;
            feedbackText.anchor = TextAnchor.MiddleCenter;
            feedbackText.alignment = TextAlignment.Center;
            feedbackText.characterSize = 0.18f;
            feedbackText.fontSize = 48;
            SetTextSortingOrder(feedbackText, TextSortingOrder);
        }

        private static void SetTextSortingOrder(TextMesh textMesh, int sortingOrder)
        {
            MeshRenderer textRenderer = textMesh.GetComponent<MeshRenderer>();
            if (textRenderer != null)
            {
                textRenderer.sortingOrder = sortingOrder;
            }
        }

        private void UpdateFeedbackVisuals()
        {
            float effectRatio = activeFeedbackDurationSeconds <= 0f
                ? 0f
                : Mathf.Clamp01(feedbackRemainingSeconds / activeFeedbackDurationSeconds);
            float alpha = Mathf.Clamp01(effectRatio);
            float intensityAlpha = GetIntensityAlphaMultiplier();
            float gradeScale = GetGradeEffectScale() * GetIntensityEffectScale();

            ApplyCharacterBaseState();
            parryEffectRoot.gameObject.SetActive(feedbackKind == FeedbackKind.Parry);
            slashEffectRoot.gameObject.SetActive(feedbackKind == FeedbackKind.Slash);
            feedbackText.gameObject.SetActive(feedbackKind != FeedbackKind.None);
            feedbackText.color = ColorWithAlpha(GetFeedbackColor(), alpha * intensityAlpha);

            if (feedbackKind == FeedbackKind.Parry)
            {
                parryEffectRoot.localScale = Vector3.one * gradeScale;
                SetSpriteRenderersColor(parrySparkRenderers, ColorWithAlpha(GetHitEffectColor(parryColor), alpha * intensityAlpha));
            }
            else if (feedbackKind == FeedbackKind.Slash)
            {
                slashEffectRoot.localScale = Vector3.one * gradeScale;
                slashGlowRenderer.color = ColorWithAlpha(GetHitEffectColor(slashColor), alpha * intensityAlpha * 0.45f);
                slashCoreRenderer.color = ColorWithAlpha(GetHitEffectColor(slashColor), alpha * intensityAlpha);
                enemyRenderer.color = Color.Lerp(enemyBaseColor, slashColor, Mathf.Clamp01(alpha * GetIntensityFlashStrength()));
            }
            else if (feedbackKind == FeedbackKind.Miss)
            {
                float shakeDistance = GetShakeDistance();
                Vector3 playerOffset = new Vector3(
                    -shakeDistance * alpha + Mathf.Sin(Time.time * 70f) * shakeDistance * 0.22f * alpha,
                    0f,
                    0f);
                playerRenderer.transform.localPosition = GetPlayerBasePosition() + playerOffset;
                playerLabel.transform.localPosition = GetPlayerLabelPosition(playerRenderer.transform.localPosition);
                playerRenderer.color = Color.Lerp(playerBaseColor, missColor, Mathf.Clamp01(alpha * GetIntensityFlashStrength()));
            }
        }

        private void ApplyHiddenFeedbackState()
        {
            ApplyCharacterBaseState();
            parryEffectRoot.gameObject.SetActive(false);
            slashEffectRoot.gameObject.SetActive(false);
            feedbackText.text = string.Empty;
            feedbackText.gameObject.SetActive(false);
        }

        private void ApplyCharacterBaseState()
        {
            Vector3 playerBasePosition = GetPlayerBasePosition();
            Vector3 enemyBasePosition = GetEnemyBasePosition();

            playerRenderer.transform.localPosition = playerBasePosition;
            playerRenderer.transform.localRotation = Quaternion.identity;
            playerRenderer.transform.localScale = GetCharacterScale();
            playerRenderer.color = playerBaseColor;

            enemyRenderer.transform.localPosition = enemyBasePosition;
            enemyRenderer.transform.localRotation = Quaternion.identity;
            enemyRenderer.transform.localScale = GetCharacterScale();
            enemyRenderer.color = enemyBaseColor;

            playerLabel.transform.localPosition = GetPlayerLabelPosition(playerBasePosition);
            playerLabel.transform.localRotation = Quaternion.identity;
            playerLabel.transform.localScale = Vector3.one;

            enemyLabel.transform.localPosition = GetEnemyLabelPosition(enemyBasePosition);
            enemyLabel.transform.localRotation = Quaternion.identity;
            enemyLabel.transform.localScale = Vector3.one;

            parryEffectRoot.localPosition = playerBasePosition + new Vector3(characterSize.x * 0.65f, characterSize.y * 0.18f, -0.05f);
            parryEffectRoot.localRotation = Quaternion.identity;
            parryEffectRoot.localScale = Vector3.one;

            slashEffectRoot.localPosition = enemyBasePosition + new Vector3(0f, characterSize.y * 0.1f, -0.05f);
            slashEffectRoot.localRotation = Quaternion.Euler(0f, 0f, -28f);
            slashEffectRoot.localScale = Vector3.one;

            feedbackText.transform.localPosition = Vector3.Lerp(playerBasePosition, enemyBasePosition, 0.5f)
                + new Vector3(0f, characterSize.y * 1.1f, -0.1f);
        }

        private Color GetFeedbackColor()
        {
            if (feedbackKind == FeedbackKind.Parry)
            {
                return GetHitEffectColor(parryColor);
            }

            if (feedbackKind == FeedbackKind.Slash)
            {
                return GetHitEffectColor(slashColor);
            }

            if (feedbackKind == FeedbackKind.Miss)
            {
                return missColor;
            }

            return Color.white;
        }

        private Color GetHitEffectColor(Color baseColor)
        {
            Color gradeColor = feedbackGrade == HitGrade.Perfect ? perfectColor : goodColor;
            return Color.Lerp(baseColor, gradeColor, 0.35f);
        }

        private float GetGradeEffectScale()
        {
            float configuredScale = feedbackGrade == HitGrade.Perfect ? perfectEffectScale : goodEffectScale;
            return Mathf.Max(0.05f, configuredScale);
        }

        private float GetIntensityEffectScale()
        {
            float minScale = Mathf.Min(minIntensityEffectScale, maxIntensityEffectScale);
            float maxScale = Mathf.Max(minIntensityEffectScale, maxIntensityEffectScale);
            return Mathf.Lerp(minScale, maxScale, activeIntensity);
        }

        private float GetIntensityAlphaMultiplier()
        {
            return Mathf.Lerp(0.55f, 1f, activeIntensity);
        }

        private float GetIntensityFlashStrength()
        {
            return Mathf.Lerp(0.45f, 1f, activeIntensity);
        }

        private float GetShakeDistance()
        {
            float minDistance = Mathf.Min(minShakeDistance, maxShakeDistance);
            float maxDistance = Mathf.Max(minShakeDistance, maxShakeDistance);
            return Mathf.Lerp(minDistance, maxDistance, activeIntensity);
        }

        private float GetConfiguredFeedbackDurationSeconds()
        {
            return Mathf.Max(0.05f, feedbackDurationSeconds);
        }

        private Vector3 GetCharacterScale()
        {
            Vector2 safeSize = new Vector2(Mathf.Max(0.05f, characterSize.x), Mathf.Max(0.05f, characterSize.y));
            return new Vector3(safeSize.x, safeSize.y, 1f);
        }

        private Vector3 GetPlayerBasePosition()
        {
            return new Vector3(playerPosition.x, playerPosition.y, 0f);
        }

        private Vector3 GetEnemyBasePosition()
        {
            return new Vector3(enemyPosition.x, enemyPosition.y, 0f);
        }

        private Vector3 GetPlayerLabelPosition(Vector3 basePosition)
        {
            return basePosition + new Vector3(0f, -characterSize.y * 0.65f, -0.1f);
        }

        private Vector3 GetEnemyLabelPosition(Vector3 basePosition)
        {
            return basePosition + new Vector3(0f, -characterSize.y * 0.65f, -0.1f);
        }

        private static void SetSpriteRenderersColor(SpriteRenderer[] renderers, Color color)
        {
            if (renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].color = color;
                }
            }
        }

        private static float ClampIntensity(float intensity)
        {
            if (float.IsNaN(intensity))
            {
                return 0f;
            }

            return Mathf.Clamp01(intensity);
        }

        private static Color ColorWithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
}
