using PulseForge.Domain.Rhythm;
using UnityEngine;

namespace PulseForge.Runtime.Unity.Prototype
{
    public sealed class DebugCombatFeedbackRenderer
    {
        public const float PanelHeight = 118f;

        private const double FeedbackDurationSeconds = 0.35d;

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
            try
            {
                GUI.color = new Color(0.12f, 0.12f, 0.12f, 1f);
                GUI.Box(area, GUIContent.none);

                Rect playerRect = new Rect(area.x + 24f, area.y + 44f, 150f, 48f);
                Rect enemyRect = new Rect(area.xMax - 174f, area.y + 44f, 150f, 48f);
                Rect feedbackRect = new Rect(area.center.x - 150f, area.y + 34f, 300f, 42f);

                GUI.color = new Color(0.28f, 0.48f, 0.95f, 1f);
                GUI.Box(playerRect, "PLAYER");

                GUI.color = new Color(0.95f, 0.32f, 0.28f, 1f);
                GUI.Box(enemyRect, "ENEMY");

                if (IsFeedbackActive(nowSeconds))
                {
                    Color feedbackColor = GetFeedbackColor();
                    feedbackColor.a = GetFeedbackAlpha(nowSeconds);
                    GUI.color = feedbackColor;
                    GUI.Box(feedbackRect, feedbackText);
                }
                else
                {
                    GUI.color = new Color(0.35f, 0.35f, 0.35f, 1f);
                    GUI.Box(feedbackRect, string.Empty);
                }
            }
            finally
            {
                GUI.color = previousColor;
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
                return new Color(1f, 0.35f, 0.25f, 1f);
            }

            if (lastAction == RhythmAction.Guard)
            {
                return new Color(0.25f, 0.9f, 1f, 1f);
            }

            if (lastAction == RhythmAction.Strike)
            {
                return new Color(1f, 0.86f, 0.25f, 1f);
            }

            return Color.white;
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
