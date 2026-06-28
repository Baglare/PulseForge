using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

#pragma warning disable 0649

namespace PulseForge.Editor.AudioPipeline
{
    public sealed class PipelineReportSummaryDrawer
    {
        private const int MaxDroppedEventsToShow = 8;

        public void Draw(
            string analysisReportPath,
            string postprocessReportPath,
            string compareReportPath,
            bool compareEnabled)
        {
            DrawAnalysisReport(analysisReportPath);
            EditorGUILayout.Space();
            DrawPostprocessReport(postprocessReportPath);
            EditorGUILayout.Space();
            DrawCompareReport(compareReportPath, compareEnabled);
        }

        private static void DrawAnalysisReport(string reportPath)
        {
            EditorGUILayout.LabelField("Analysis Report", EditorStyles.miniBoldLabel);
            if (!TryReadReport(reportPath, out string json, out string errorMessage))
            {
                EditorGUILayout.HelpBox(errorMessage, MessageType.Info);
                return;
            }

            try
            {
                AnalysisReport report = JsonUtility.FromJson<AnalysisReport>(json);
                if (report == null)
                {
                    EditorGUILayout.HelpBox("Analysis report could not be parsed.", MessageType.Warning);
                    return;
                }

                EditorGUILayout.LabelField("detectedEventCount", FormatInt(report.detectedEventCount));
                EditorGUILayout.LabelField("durationSeconds", FormatSeconds(report.durationSeconds));
                EditorGUILayout.LabelField("sampleRate", FormatInt(report.sampleRate));
                EditorGUILayout.LabelField("detectionMode", FormatString(report.detectionMode));
                EditorGUILayout.LabelField("thresholdRatio", FormatFloat(report.thresholdRatio));
                EditorGUILayout.LabelField("minGapSeconds", FormatSeconds(report.minGapSeconds));
                EditorGUILayout.LabelField("maxFrameAmplitude", FormatFloat(report.maxFrameAmplitude));

                if (ContainsJsonField(json, "detectionCurveMax"))
                {
                    EditorGUILayout.LabelField("detectionCurveMax", FormatFloat(report.detectionCurveMax));
                }
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox("Analysis report parse error: " + exception.Message, MessageType.Warning);
            }
        }

        private static void DrawPostprocessReport(string reportPath)
        {
            EditorGUILayout.LabelField("Postprocess Report", EditorStyles.miniBoldLabel);
            if (!TryReadReport(reportPath, out string json, out string errorMessage))
            {
                EditorGUILayout.HelpBox(errorMessage, MessageType.Info);
                return;
            }

            try
            {
                PostprocessReport report = JsonUtility.FromJson<PostprocessReport>(json);
                if (report == null)
                {
                    EditorGUILayout.HelpBox("Postprocess report could not be parsed.", MessageType.Warning);
                    return;
                }

                EditorGUILayout.LabelField("inputEventCount", FormatInt(report.inputEventCount));
                EditorGUILayout.LabelField("outputEventCount", FormatInt(report.outputEventCount));
                EditorGUILayout.LabelField("droppedEventCount", FormatInt(report.droppedEventCount));
                EditorGUILayout.LabelField("difficulty", FormatString(report.difficulty));
                EditorGUILayout.LabelField("minGapSeconds", FormatSeconds(report.minGapSeconds));
                EditorGUILayout.LabelField("actionMode", FormatString(report.actionMode));
                EditorGUILayout.LabelField("maxEvents", ContainsJsonNull(json, "maxEvents") ? "none" : FormatInt(report.maxEvents));
                DrawDroppedEvents(report.droppedEvents);
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox("Postprocess report parse error: " + exception.Message, MessageType.Warning);
            }
        }

        private static void DrawCompareReport(string reportPath, bool compareEnabled)
        {
            EditorGUILayout.LabelField("Compare Report", EditorStyles.miniBoldLabel);
            if (!compareEnabled)
            {
                EditorGUILayout.HelpBox("Compare not enabled for the last pipeline run.", MessageType.Info);
                return;
            }

            if (!TryReadReport(reportPath, out string json, out string errorMessage))
            {
                EditorGUILayout.HelpBox(errorMessage, MessageType.Info);
                return;
            }

            try
            {
                CompareReport report = JsonUtility.FromJson<CompareReport>(json);
                if (report == null || report.summary == null)
                {
                    EditorGUILayout.HelpBox("Compare report could not be parsed.", MessageType.Warning);
                    return;
                }

                CompareSummary summary = report.summary;
                EditorGUILayout.LabelField("expectedEventCount", FormatInt(summary.expectedEventCount));
                EditorGUILayout.LabelField("actualEventCount", FormatInt(summary.actualEventCount));
                EditorGUILayout.LabelField("comparedEventCount", FormatInt(summary.comparedEventCount));
                EditorGUILayout.LabelField("meanSignedErrorMs", FormatMilliseconds(summary.meanSignedErrorMs));
                EditorGUILayout.LabelField("meanAbsoluteErrorMs", FormatMilliseconds(summary.meanAbsoluteErrorMs));
                EditorGUILayout.LabelField("maxAbsoluteErrorMs", FormatMilliseconds(summary.maxAbsoluteErrorMs));
                EditorGUILayout.LabelField("suggestedGlobalOffsetSeconds", FormatSeconds(summary.suggestedGlobalOffsetSeconds));
                EditorGUILayout.LabelField("outsideToleranceCount", FormatInt(summary.outsideToleranceCount));
                EditorGUILayout.LabelField("actionMismatchCount", FormatInt(summary.actionMismatchCount));
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox("Compare report parse error: " + exception.Message, MessageType.Warning);
            }
        }

        private static void DrawDroppedEvents(DroppedEvent[] droppedEvents)
        {
            if (droppedEvents == null || droppedEvents.Length == 0)
            {
                EditorGUILayout.LabelField("Dropped events", "none");
                return;
            }

            EditorGUILayout.LabelField("Dropped events", EditorStyles.miniBoldLabel);
            int shownCount = Math.Min(MaxDroppedEventsToShow, droppedEvents.Length);
            for (int i = 0; i < shownCount; i++)
            {
                DroppedEvent droppedEvent = droppedEvents[i];
                EditorGUILayout.LabelField(
                    FormatSeconds(droppedEvent.targetTimeSeconds),
                    FormatString(droppedEvent.reason));
            }

            int remainingCount = droppedEvents.Length - shownCount;
            if (remainingCount > 0)
            {
                EditorGUILayout.LabelField("...and " + remainingCount.ToString(CultureInfo.InvariantCulture) + " more");
            }
        }

        private static bool TryReadReport(string reportPath, out string json, out string errorMessage)
        {
            json = string.Empty;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(reportPath))
            {
                errorMessage = "Report path is not available.";
                return false;
            }

            string fullPath = ToFullPath(reportPath);
            if (!File.Exists(fullPath))
            {
                errorMessage = "Report not found: " + reportPath;
                return false;
            }

            try
            {
                json = File.ReadAllText(fullPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    errorMessage = "Report is empty: " + reportPath;
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                errorMessage = "Could not read report " + reportPath + ": " + exception.Message;
                return false;
            }
        }

        private static string ToFullPath(string reportPath)
        {
            if (Path.IsPathRooted(reportPath))
            {
                return reportPath;
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, reportPath);
        }

        private static bool ContainsJsonField(string json, string fieldName)
        {
            return json.IndexOf("\"" + fieldName + "\"", StringComparison.Ordinal) >= 0;
        }

        private static bool ContainsJsonNull(string json, string fieldName)
        {
            return json.IndexOf("\"" + fieldName + "\": null", StringComparison.Ordinal) >= 0;
        }

        private static string FormatString(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "n/a" : value;
        }

        private static string FormatInt(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string FormatSeconds(float value)
        {
            return FormatFloat(value) + "s";
        }

        private static string FormatMilliseconds(float value)
        {
            return FormatFloat(value) + "ms";
        }

        [Serializable]
        private sealed class AnalysisReport
        {
            public int detectedEventCount;
            public float durationSeconds;
            public int sampleRate;
            public string detectionMode;
            public float thresholdRatio;
            public float minGapSeconds;
            public float maxFrameAmplitude;
            public float detectionCurveMax;
        }

        [Serializable]
        private sealed class PostprocessReport
        {
            public int inputEventCount;
            public int outputEventCount;
            public int droppedEventCount;
            public string difficulty;
            public float minGapSeconds;
            public string actionMode;
            public int maxEvents;
            public DroppedEvent[] droppedEvents;
        }

        [Serializable]
        private sealed class DroppedEvent
        {
            public float targetTimeSeconds;
            public string reason;
        }

        [Serializable]
        private sealed class CompareReport
        {
            public CompareSummary summary;
        }

        [Serializable]
        private sealed class CompareSummary
        {
            public int expectedEventCount;
            public int actualEventCount;
            public int comparedEventCount;
            public float meanSignedErrorMs;
            public float meanAbsoluteErrorMs;
            public float maxAbsoluteErrorMs;
            public float suggestedGlobalOffsetSeconds;
            public int outsideToleranceCount;
            public int actionMismatchCount;
        }
    }
}

#pragma warning restore 0649
