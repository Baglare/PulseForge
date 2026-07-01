using PulseForge.Domain.Rhythm;
using UnityEngine;

namespace PulseForge.Runtime.Unity.Prototype
{
    public sealed class DebugCombatFeedbackRenderer
    {
        public const float PanelHeight = 118f;

        private const double FeedbackDurationSeconds = 0.35d;
        private static readonly Color PanelBackgroundColor = new Color(0.06f, 0.065f, 0.08f, 1f);
        private static readonly Color PlayerColor = new Color(0.16f, 0.42f, 0.72f, 1f);
        private static readonly Color EnemyColor = new Color(0.62f, 0.18f, 0.16f, 1f);
        private static readonly Color EmptyFeedbackColor = new Color(0.22f, 0.24f, 0.28f, 1f);
        private static readonly Color GuardPerfectColor = new Color(0.28f, 0.95f, 1f, 1f);
        private static readonly Color GuardGoodColor = new Color(0.2f, 0.66f, 0.92f, 1f);
        private static readonly Color StrikePerfectColor = new Color(1f, 0.64f, 0.22f, 1f);
        private static readonly Color StrikeGoodColor = new Color(0.95f, 0.34f, 0.2f, 1f);
        private static readonly Color MissColor = new Color(1f, 0.2f, 0.18f, 1f);

        private string feedbackText = string.Empty;
        private double feedbackUntilTime;
        private RhythmAction? lastAction;
        private HitGrade? lastGrade;

        public void Clear()
        {
            feedbackText = string.Empty;
            feedbackUntilTime = 0d;
            lastAction = null;
            lastGrade = null;
        }

        public void ShowHit(RhythmAction action, HitGrade grade, double nowSeconds)
        {
            if (grade == HitGrade.Miss)
            {
                ShowMiss(nowSeconds);
                return;
            }

            lastAction = action;
            lastGrade = grade;
            feedbackText = FormatHitFeedbackText(action, grade);
            feedbackUntilTime = nowSeconds + FeedbackDurationSeconds;
        }

        public void ShowMiss(double nowSeconds)
        {
            lastAction = null;
            lastGrade = HitGrade.Miss;
            feedbackText = "MISS / HIT TAKEN";
            feedbackUntilTime = nowSeconds + FeedbackDurationSeconds;
        }

        public void Draw(Rect area, double nowSeconds, bool isSessionRunning)
        {
            _ = isSessionRunning;

            Color previousColor = GUI.color;
            Color previousContentColor = GUI.contentColor;
            try
            {
                GUI.color = PanelBackgroundColor;
                GUI.Box(area, GUIContent.none);

                Rect playerRect = new Rect(area.x + 24f, area.y + 44f, 150f, 48f);
                Rect enemyRect = new Rect(area.xMax - 174f, area.y + 44f, 150f, 48f);
                Rect feedbackRect = new Rect(area.center.x - 150f, area.y + 34f, 300f, 42f);

                GUI.contentColor = Color.white;
                GUI.color = PlayerColor;
                GUI.Box(playerRect, "PLAYER");

                GUI.color = EnemyColor;
                GUI.Box(enemyRect, "ENEMY");

                if (IsFeedbackActive(nowSeconds))
                {
                    Color feedbackColor = GetFeedbackColor();
                    feedbackColor.a = GetFeedbackAlpha(nowSeconds);
                    GUI.color = feedbackColor;
                    GUI.contentColor = GetFeedbackTextColor();
                    GUI.Box(feedbackRect, feedbackText);
                }
                else
                {
                    GUI.color = EmptyFeedbackColor;
                    GUI.contentColor = new Color(0.75f, 0.78f, 0.82f, 1f);
                    GUI.Box(feedbackRect, string.Empty);
                }
            }
            finally
            {
                GUI.color = previousColor;
                GUI.contentColor = previousContentColor;
            }
        }

        private bool IsFeedbackActive(double nowSeconds)
        {
            return !string.IsNullOrEmpty(feedbackText) && nowSeconds <= feedbackUntilTime;
        }

        private float GetFeedbackAlpha(double nowSeconds)
        {
            if (!IsFeedbackActive(nowSeconds))
            {
                return 0f;
            }

            double remainingSeconds = feedbackUntilTime - nowSeconds;
            return Mathf.Clamp01((float)(remainingSeconds / FeedbackDurationSeconds));
        }

        private Color GetFeedbackColor()
        {
            if (lastGrade == HitGrade.Miss)
            {
                return MissColor;
            }

            if (lastAction == RhythmAction.Guard)
            {
                return lastGrade == HitGrade.Perfect ? GuardPerfectColor : GuardGoodColor;
            }

            if (lastAction == RhythmAction.Strike)
            {
                return lastGrade == HitGrade.Perfect ? StrikePerfectColor : StrikeGoodColor;
            }

            return Color.white;
        }

        private Color GetFeedbackTextColor()
        {
            if (lastGrade == HitGrade.Miss || lastAction == RhythmAction.Strike)
            {
                return Color.white;
            }

            return Color.black;
        }

        private static string FormatHitFeedbackText(RhythmAction action, HitGrade grade)
        {
            if (action == RhythmAction.Guard)
            {
                return grade == HitGrade.Perfect ? "PERFECT PARRY" : "GOOD PARRY";
            }

            if (action == RhythmAction.Strike)
            {
                return grade == HitGrade.Perfect ? "PERFECT SLASH" : "GOOD SLASH";
            }

            return grade.ToString().ToUpperInvariant();
        }
    }
}
