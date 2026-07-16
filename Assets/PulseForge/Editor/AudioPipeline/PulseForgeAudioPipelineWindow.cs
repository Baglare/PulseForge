using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using PulseForge.AudioAnalysis;
using PulseForge.BeatMapGeneration;
using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Audio;
using PulseForge.Runtime.Unity.BeatMaps;
using PulseForge.Runtime.Unity.Persistence;
using PulseForge.Runtime.Unity.Prototype;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PulseForge.Editor.AudioPipeline
{
    public sealed class PulseForgeAudioPipelineWindow : EditorWindow
    {
        private const string MenuPath = "Tools/PulseForge/Audio Pipeline";
        private const string PipelineScriptPath = "tools/audio_analyzer/run_debug_pipeline.py";
        private const string StyleVariantsScriptPath = "tools/audio_analyzer/generate_style_variants.py";
        private const string ReportDirectory = "tools/audio_analyzer/out";
        private const string DefaultOutputDirectory = "Assets/PulseForge/Demo/BeatMaps";
        private const string DefaultOutputName = "Debug_120BPM";
        private const string DefaultPattern = "Guard,Guard,Strike,Guard,Strike,Strike,Guard,Strike,Guard,Strike";
        private const float DefaultBurstWindowSeconds = 0.35f;
        private const int StyleVariantCount = 4;
        private static readonly string[] StyleVariantLabels = { "Balanced", "Defensive", "Aggressive", "Bursty" };
        private static readonly string[] StyleVariantCliValues = { "balanced", "defensive", "aggressive", "bursty" };

        private AudioClip inputAudioClip;
        private PipelineMode pipelineMode = PipelineMode.RadialV2;
        private TextAsset expectedBeatMapJson;
        private string outputName = DefaultOutputName;
        private string pattern = DefaultPattern;
        private DetectionMode detectionMode = DetectionMode.Amplitude;
        private Difficulty difficulty = Difficulty.Normal;
        private ActionMode actionMode = ActionMode.Pattern;
        private CombatStyle combatStyle = CombatStyle.Legacy;
        private float burstWindowSeconds = DefaultBurstWindowSeconds;
        private bool writeDebugCsv;
        private bool useExpectedCompare;
        private string pythonExecutable = "python";
        private string outputDirectory = DefaultOutputDirectory;
        private string generatedRawJsonPath = string.Empty;
        private string generatedPlayableJsonPath = string.Empty;
        private string analysisReportPath = string.Empty;
        private string postprocessReportPath = string.Empty;
        private string compareReportPath = string.Empty;
        private string radialAnalysisReportPath = string.Empty;
        private string radialPlannerReportPath = string.Empty;
        private TextAsset generatedRawJsonAsset;
        private TextAsset generatedPlayableJsonAsset;
        private readonly string[] generatedVariantJsonPaths = new string[StyleVariantCount];
        private readonly TextAsset[] generatedVariantJsonAssets = new TextAsset[StyleVariantCount];
        private readonly RadialEncounterPlanResult[] radialVariantResults =
            new RadialEncounterPlanResult[StyleVariantCount];
        private RadialAudioAnalysisResult radialAnalysisResult;
        private RadialEncounterPlanResult radialPlanResult;
        private AudioClip radialAnalysisAudioClip;
        private DetectionMode radialAnalysisDetectionMode;
        private string statusMessage = "Ready";
        private string lastStdout = string.Empty;
        private string lastStderr = string.Empty;
        private string timelinePreviewError = string.Empty;
        private string pipelineReportsError = string.Empty;
        private Vector2 windowScroll;
        private Vector2 outputScroll;
        private bool timelinePreviewFoldout = true;
        private bool pipelineReportsFoldout = true;
        private bool lastRunUsedExpectedCompare;
        private StyleVariantSelection selectedVariant = StyleVariantSelection.None;
        private readonly BeatmapTimelinePreviewDrawer timelinePreviewDrawer = new BeatmapTimelinePreviewDrawer();
        private readonly PipelineReportSummaryDrawer pipelineReportSummaryDrawer = new PipelineReportSummaryDrawer();

        [MenuItem(MenuPath)]
        public static void Open()
        {
            GetWindow<PulseForgeAudioPipelineWindow>("PulseForge Audio Pipeline");
        }

        private void OnGUI()
        {
            windowScroll = EditorGUILayout.BeginScrollView(windowScroll);
            EditorGUILayout.LabelField("PulseForge Audio Pipeline", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            pipelineMode = (PipelineMode)EditorGUILayout.EnumPopup("Pipeline", pipelineMode);
            inputAudioClip = (AudioClip)EditorGUILayout.ObjectField("Input Audio Clip", inputAudioClip, typeof(AudioClip), false);
            if (pipelineMode == PipelineMode.LegacyPythonV1)
            {
                expectedBeatMapJson = (TextAsset)EditorGUILayout.ObjectField("Expected Beat Map JSON", expectedBeatMapJson, typeof(TextAsset), false);
            }
            outputName = EditorGUILayout.TextField("Output Name", outputName);
            DrawCombatStyleControls();
            detectionMode = (DetectionMode)EditorGUILayout.EnumPopup("Detection Mode", detectionMode);
            difficulty = (Difficulty)EditorGUILayout.EnumPopup("Difficulty", difficulty);
            if (pipelineMode == PipelineMode.LegacyPythonV1)
            {
                writeDebugCsv = EditorGUILayout.Toggle("Write Debug CSV", writeDebugCsv);
                useExpectedCompare = EditorGUILayout.Toggle("Use Expected Compare", useExpectedCompare);
                pythonExecutable = EditorGUILayout.TextField("Python Executable", pythonExecutable);
            }
            outputDirectory = EditorGUILayout.TextField("Output Directory", outputDirectory);
            UpdateGeneratedJsonPathsFromInputs();
            UpdateStyleVariantPathsFromInputs();
            UpdateReportPathsFromInputs();

            EditorGUILayout.Space();
            if (GUILayout.Button(
                pipelineMode == PipelineMode.RadialV2
                    ? "Run Radial V2 Pipeline"
                    : "Run Legacy Python V1 Pipeline"))
            {
                RunPipeline();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                pipelineMode == PipelineMode.RadialV2
                    ? "Generated Radial V2 JSON"
                    : "Generated Legacy V1 Playable JSON",
                EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(generatedPlayableJsonPath) ? "(not generated yet)" : generatedPlayableJsonPath, GUILayout.Height(18f));
            DrawGeneratedJsonWorkflow();

            EditorGUILayout.Space();
            DrawStyleVariantsWorkflow();

            EditorGUILayout.Space();
            DrawBeatmapTimelinePreview();

            EditorGUILayout.Space();
            DrawPipelineReports();

            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(statusMessage, GetStatusMessageType());

            if (pipelineMode == PipelineMode.LegacyPythonV1)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Legacy Python Process Output", EditorStyles.boldLabel);
                outputScroll = EditorGUILayout.BeginScrollView(outputScroll);
                EditorGUILayout.LabelField("stdout", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(lastStdout, GUILayout.MinHeight(120f));
                EditorGUILayout.LabelField("stderr", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(lastStderr, GUILayout.MinHeight(120f));
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawCombatStyleControls()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Combat Style", EditorStyles.boldLabel);
            combatStyle = (CombatStyle)EditorGUILayout.EnumPopup("Combat Style", combatStyle);

            if (pipelineMode == PipelineMode.RadialV2)
            {
                EditorGUILayout.HelpBox(
                    "Combat Style redistributes the same input budget. It does not change analysis timing.",
                    MessageType.Info);
                return;
            }

            bool useLegacyMapping = combatStyle == CombatStyle.Legacy;
            using (new EditorGUI.DisabledScope(!useLegacyMapping))
            {
                actionMode = (ActionMode)EditorGUILayout.EnumPopup("Action Mode", actionMode);
                pattern = EditorGUILayout.TextField("Pattern", pattern);
            }

            if (!useLegacyMapping)
            {
                EditorGUILayout.HelpBox("Combat style controls action mapping. Action Mode and Pattern will not be sent to Python.", MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(useLegacyMapping))
            {
                burstWindowSeconds = EditorGUILayout.FloatField("Burst Window Seconds", burstWindowSeconds);
            }

            EditorGUILayout.HelpBox("Burst Window Seconds is sent for non-legacy combat styles and is used by the bursty preset.", MessageType.None);
        }

        private void RunPipeline()
        {
            if (pipelineMode == PipelineMode.RadialV2)
            {
                RunRadialV2Pipeline();
                return;
            }
            RunLegacyPythonV1Pipeline();
        }

        private void RunRadialV2Pipeline()
        {
            lastStdout = string.Empty;
            lastStderr = string.Empty;
            radialAnalysisResult = null;
            radialPlanResult = null;
            ClearStyleVariantAssets();
            ClearRadialVariantResults();
            if (!TryValidateRadialInputs(out string audioAssetPath, out string validationError))
            {
                statusMessage = validationError;
                EditorUtility.DisplayDialog("PulseForge Audio Pipeline", validationError, "OK");
                return;
            }

            try
            {
                radialAnalysisResult = RuntimeBeatMapAnalyzer.AnalyzeV2(
                    inputAudioClip,
                    ToRuntimeDetectionMode(detectionMode));
                radialAnalysisAudioClip = inputAudioClip;
                radialAnalysisDetectionMode = detectionMode;
                string seed = AssetDatabase.AssetPathToGUID(audioAssetPath);
                if (string.IsNullOrWhiteSpace(seed))
                {
                    seed = audioAssetPath;
                }
                radialPlanResult = new RadialEncounterPlanner().Plan(
                    radialAnalysisResult,
                    ToPlannerDifficulty(difficulty),
                    ToPlannerCombatStyle(combatStyle),
                    seed);
                radialPlanResult.beatMap.displayName = GetSafeOutputName();

                UpdateGeneratedJsonPathsFromInputs();
                UpdateReportPathsFromInputs();
                RadialBeatMapCacheData artifact = RadialBeatMapArtifactSerializer.Create(
                    seed,
                    BuildRadialPresetId(combatStyle),
                    radialPlanResult.beatMap,
                    radialAnalysisResult.qualityReport,
                    radialPlanResult.qualityReport);
                WriteProjectJson(
                    generatedPlayableJsonPath,
                    RadialBeatMapArtifactSerializer.Serialize(artifact, true));
                WriteProjectJson(
                    radialAnalysisReportPath,
                    JsonUtility.ToJson(radialAnalysisResult.qualityReport, true));
                WriteProjectJson(
                    radialPlannerReportPath,
                    JsonUtility.ToJson(radialPlanResult.qualityReport, true));

                AssetDatabase.Refresh();
                TryLoadGeneratedJsonAssets();
                statusMessage = generatedPlayableJsonAsset == null
                    ? "Radial V2 completed, but the generated JSON asset is not imported yet."
                    : "Radial V2 completed successfully.";
            }
            catch (Exception exception)
            {
                statusMessage = "Radial V2 pipeline failed: " + exception.Message;
                EditorUtility.DisplayDialog("PulseForge Audio Pipeline", statusMessage, "OK");
                Debug.LogException(exception);
            }
        }

        private void RunRadialStyleVariants()
        {
            if (radialAnalysisResult == null)
            {
                statusMessage = "Run Radial V2 once before generating style variants.";
                EditorUtility.DisplayDialog("PulseForge Audio Pipeline", statusMessage, "OK");
                return;
            }
            if (radialAnalysisAudioClip != inputAudioClip
                || radialAnalysisDetectionMode != detectionMode)
            {
                statusMessage = "Input Audio Clip or Detection Mode changed. Run Radial V2 again first.";
                EditorUtility.DisplayDialog("PulseForge Audio Pipeline", statusMessage, "OK");
                return;
            }
            if (!TryValidateRadialInputs(out string audioAssetPath, out string validationError))
            {
                statusMessage = validationError;
                EditorUtility.DisplayDialog("PulseForge Audio Pipeline", validationError, "OK");
                return;
            }

            try
            {
                ClearStyleVariantAssets();
                ClearRadialVariantResults();
                UpdateStyleVariantPathsFromInputs();
                string seed = AssetDatabase.AssetPathToGUID(audioAssetPath);
                if (string.IsNullOrWhiteSpace(seed))
                {
                    seed = audioAssetPath;
                }

                for (int i = 0; i < StyleVariantCount; i++)
                {
                    CombatStyle style = GetStyleVariantCombatStyle(i);
                    RadialEncounterPlanResult variant = new RadialEncounterPlanner().Plan(
                        radialAnalysisResult,
                        ToPlannerDifficulty(difficulty),
                        ToPlannerCombatStyle(style),
                        seed);
                    variant.beatMap.displayName = GetSafeOutputName() + " " + StyleVariantLabels[i];
                    radialVariantResults[i] = variant;
                    RadialBeatMapCacheData artifact = RadialBeatMapArtifactSerializer.Create(
                        seed,
                        BuildRadialPresetId(style),
                        variant.beatMap,
                        radialAnalysisResult.qualityReport,
                        variant.qualityReport);
                    WriteProjectJson(
                        generatedVariantJsonPaths[i],
                        RadialBeatMapArtifactSerializer.Serialize(artifact, true));
                    WriteProjectJson(
                        BuildRadialStyleVariantReportPath(
                            outputDirectory.Trim(),
                            GetSafeOutputName(),
                            StyleVariantLabels[i]),
                        JsonUtility.ToJson(variant.qualityReport, true));
                }

                AssetDatabase.Refresh();
                TryLoadStyleVariantAssets();
                statusMessage = AreAllStyleVariantAssetsLoaded()
                    ? "Radial V2 style variants generated from the existing analysis."
                    : "Radial V2 variants were written, but one or more assets are not imported yet.";
            }
            catch (Exception exception)
            {
                statusMessage = "Radial V2 style variant generation failed: " + exception.Message;
                EditorUtility.DisplayDialog("PulseForge Audio Pipeline", statusMessage, "OK");
                Debug.LogException(exception);
            }
        }

        private void RunLegacyPythonV1Pipeline()
        {
            lastStdout = string.Empty;
            lastStderr = string.Empty;
            generatedRawJsonAsset = null;
            generatedPlayableJsonAsset = null;
            UpdateGeneratedJsonPathsFromInputs();

            if (!TryBuildCommand(out string arguments, out string rawJsonPath, out string playableJsonPath, out string validationError))
            {
                statusMessage = validationError;
                EditorUtility.DisplayDialog("PulseForge Audio Pipeline", validationError, "OK");
                return;
            }

            string projectRoot = GetProjectRoot();
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                Arguments = arguments,
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        statusMessage = "Could not start Python process.";
                        EditorUtility.DisplayDialog("PulseForge Audio Pipeline", statusMessage, "OK");
                        return;
                    }

                    lastStdout = process.StandardOutput.ReadToEnd();
                    lastStderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        statusMessage = "Pipeline failed with exit code " + process.ExitCode + ".";
                        EditorUtility.DisplayDialog("PulseForge Audio Pipeline", statusMessage + "\n\n" + lastStderr, "OK");
                        Debug.LogError(statusMessage + "\n" + lastStdout + "\n" + lastStderr);
                        return;
                    }
                }

                generatedRawJsonPath = rawJsonPath;
                generatedPlayableJsonPath = playableJsonPath;
                UpdateReportPathsFromInputs();
                lastRunUsedExpectedCompare = useExpectedCompare;
                AssetDatabase.Refresh();
                TryLoadGeneratedJsonAssets();
                statusMessage = generatedRawJsonAsset == null || generatedPlayableJsonAsset == null
                    ? "Pipeline completed successfully. One or more generated JSON assets were not found yet."
                    : "Pipeline completed successfully.";
                Debug.Log(statusMessage + "\n" + lastStdout);
            }
            catch (Exception exception)
            {
                statusMessage = "Pipeline failed: " + exception.Message;
                EditorUtility.DisplayDialog("PulseForge Audio Pipeline", statusMessage, "OK");
                Debug.LogException(exception);
            }
        }

        private void RunStyleVariants()
        {
            if (pipelineMode == PipelineMode.RadialV2)
            {
                RunRadialStyleVariants();
                return;
            }
            RunLegacyPythonStyleVariants();
        }

        private void RunLegacyPythonStyleVariants()
        {
            lastStdout = string.Empty;
            lastStderr = string.Empty;
            ClearStyleVariantAssets();
            UpdateStyleVariantPathsFromInputs();

            if (!TryBuildStyleVariantsCommand(out string arguments, out string validationError))
            {
                statusMessage = validationError;
                EditorUtility.DisplayDialog("PulseForge Audio Pipeline", validationError, "OK");
                return;
            }

            string projectRoot = GetProjectRoot();
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                Arguments = arguments,
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        statusMessage = "Could not start Python process.";
                        EditorUtility.DisplayDialog("PulseForge Audio Pipeline", statusMessage, "OK");
                        return;
                    }

                    lastStdout = process.StandardOutput.ReadToEnd();
                    lastStderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        statusMessage = "Style variant generation failed with exit code " + process.ExitCode + ".";
                        EditorUtility.DisplayDialog("PulseForge Audio Pipeline", statusMessage + "\n\n" + lastStderr, "OK");
                        Debug.LogError(statusMessage + "\n" + lastStdout + "\n" + lastStderr);
                        return;
                    }
                }

                AssetDatabase.Refresh();
                TryLoadGeneratedJsonAssets();
                TryLoadStyleVariantAssets();
                statusMessage = AreAllStyleVariantAssetsLoaded()
                    ? "Style variants generated successfully."
                    : "Style variants generated. One or more variant JSON assets were not found yet.";
                Debug.Log(statusMessage + "\n" + lastStdout);
            }
            catch (Exception exception)
            {
                statusMessage = "Style variant generation failed: " + exception.Message;
                EditorUtility.DisplayDialog("PulseForge Audio Pipeline", statusMessage, "OK");
                Debug.LogException(exception);
            }
        }

        private void DrawGeneratedJsonWorkflow()
        {
            if (generatedPlayableJsonAsset == null)
            {
                TryLoadGeneratedJsonAssets();
            }

            bool hasGeneratedAsset = generatedPlayableJsonAsset != null;
            EditorGUILayout.LabelField("Generated JSON Asset Status", hasGeneratedAsset ? "Found" : "Not found yet");

            if (hasGeneratedAsset)
            {
                EditorGUILayout.ObjectField("Generated JSON Asset", generatedPlayableJsonAsset, typeof(TextAsset), false);
            }

            using (new EditorGUI.DisabledScope(!hasGeneratedAsset))
            {
                if (GUILayout.Button("Ping / Select Generated JSON"))
                {
                    EditorGUIUtility.PingObject(generatedPlayableJsonAsset);
                    Selection.activeObject = generatedPlayableJsonAsset;
                }
            }

            DebugRhythmPrototypeController selectedPrototype = GetSelectedDebugPrototypeController();
            if (!hasGeneratedAsset)
            {
                EditorGUILayout.HelpBox("Generated playable JSON asset is not available yet.", MessageType.Info);
            }
            else if (selectedPrototype == null)
            {
                EditorGUILayout.HelpBox("Select a GameObject with DebugRhythmPrototypeController to assign.", MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(!hasGeneratedAsset || selectedPrototype == null))
            {
                if (GUILayout.Button("Assign to Selected Debug Prototype"))
                {
                    AssignGeneratedJsonToSelectedPrototype(selectedPrototype);
                }
            }
        }

        private void DrawStyleVariantsWorkflow()
        {
            TryLoadStyleVariantAssets();

            EditorGUILayout.LabelField("Style Variants", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                pipelineMode == PipelineMode.RadialV2
                    ? "Reuses the current AnalyzeV2 result and reruns only the planner for Balanced, Defensive, Aggressive, and Bursty."
                    : "Legacy Python V1 generates Balanced, Defensive, Aggressive, and Bursty playable JSON files from the selected WAV.",
                MessageType.Info);
            EditorGUILayout.LabelField("Input WAV", inputAudioClip == null ? "(not assigned)" : inputAudioClip.name);
            EditorGUILayout.LabelField("Output Name", GetSafeOutputName());
            EditorGUILayout.LabelField("Difficulty", ToCliValue(difficulty));
            EditorGUILayout.LabelField("Detection Mode", ToCliValue(detectionMode));

            if (GUILayout.Button(
                pipelineMode == PipelineMode.RadialV2
                    ? "Generate Radial V2 Style Variants"
                    : "Generate Legacy Python V1 Style Variants"))
            {
                RunStyleVariants();
            }

            if (GUILayout.Button("Refresh Variant Assets"))
            {
                AssetDatabase.Refresh();
                TryLoadStyleVariantAssets();
                statusMessage = "Style variant assets refreshed.";
            }

            selectedVariant = (StyleVariantSelection)EditorGUILayout.EnumPopup("Preview Variant", selectedVariant);

            DebugRhythmPrototypeController selectedPrototype = GetSelectedDebugPrototypeController();
            if (selectedPrototype == null)
            {
                EditorGUILayout.HelpBox("Select a GameObject with DebugRhythmPrototypeController to assign a variant.", MessageType.Info);
            }

            EditorGUILayout.LabelField("Style Variant Comparison", EditorStyles.miniBoldLabel);
            DrawStyleVariantHeader();
            for (int i = 0; i < StyleVariantCount; i++)
            {
                DrawStyleVariantRow(i, selectedPrototype);
            }
        }

        private void DrawStyleVariantHeader()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Style", EditorStyles.miniBoldLabel, GUILayout.Width(76f));
            EditorGUILayout.LabelField("Status", EditorStyles.miniBoldLabel, GUILayout.Width(72f));
            if (pipelineMode == PipelineMode.RadialV2)
            {
                EditorGUILayout.LabelField("Encounters", EditorStyles.miniBoldLabel, GUILayout.Width(72f));
                EditorGUILayout.LabelField("Inputs", EditorStyles.miniBoldLabel, GUILayout.Width(52f));
                EditorGUILayout.LabelField("Result", EditorStyles.miniBoldLabel, GUILayout.Width(112f));
                EditorGUILayout.LabelField("Repairs", EditorStyles.miniBoldLabel, GUILayout.Width(58f));
                EditorGUILayout.EndHorizontal();
                return;
            }
            EditorGUILayout.LabelField("Events", EditorStyles.miniBoldLabel, GUILayout.Width(52f));
            EditorGUILayout.LabelField("Guard", EditorStyles.miniBoldLabel, GUILayout.Width(52f));
            EditorGUILayout.LabelField("Strike", EditorStyles.miniBoldLabel, GUILayout.Width(52f));
            EditorGUILayout.LabelField("First", EditorStyles.miniBoldLabel, GUILayout.Width(58f));
            EditorGUILayout.LabelField("Last", EditorStyles.miniBoldLabel, GUILayout.Width(58f));
            EditorGUILayout.LabelField("Dropped", EditorStyles.miniBoldLabel, GUILayout.Width(68f));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStyleVariantRow(int index, DebugRhythmPrototypeController selectedPrototype)
        {
            if (pipelineMode == PipelineMode.RadialV2)
            {
                DrawRadialStyleVariantRow(index, selectedPrototype);
                return;
            }

            TextAsset variantAsset = generatedVariantJsonAssets[index];
            string label = StyleVariantLabels[index];
            bool hasVariantAsset = variantAsset != null;
            StyleVariantSummary summary = BuildStyleVariantSummary(index, variantAsset);

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(76f));
            EditorGUILayout.LabelField(hasVariantAsset ? "Found" : "Not found", GUILayout.Width(72f));
            EditorGUILayout.LabelField(summary.EventCountText, GUILayout.Width(52f));
            EditorGUILayout.LabelField(summary.GuardCountText, GUILayout.Width(52f));
            EditorGUILayout.LabelField(summary.StrikeCountText, GUILayout.Width(52f));
            EditorGUILayout.LabelField(summary.FirstTimeText, GUILayout.Width(58f));
            EditorGUILayout.LabelField(summary.LastTimeText, GUILayout.Width(58f));
            EditorGUILayout.LabelField(summary.DroppedCountText, GUILayout.Width(68f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!hasVariantAsset))
            {
                if (GUILayout.Button("Ping / Select", GUILayout.Width(92f)))
                {
                    EditorGUIUtility.PingObject(variantAsset);
                    Selection.activeObject = variantAsset;
                }

                if (GUILayout.Button("Preview", GUILayout.Width(64f)))
                {
                    selectedVariant = (StyleVariantSelection)(index + 1);
                    statusMessage = "Previewing style variant: " + label + ".";
                }
            }

            using (new EditorGUI.DisabledScope(!hasVariantAsset || selectedPrototype == null))
            {
                if (GUILayout.Button("Assign", GUILayout.Width(58f)))
                {
                    AssignJsonAssetToSelectedPrototype(
                        selectedPrototype,
                        variantAsset,
                        "debugBeatMapJson",
                        "Assign PulseForge " + label + " Beat Map JSON",
                        "Assigned " + label + " variant JSON to selected DebugRhythmPrototypeController.");
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.SelectableLabel(generatedVariantJsonPaths[index], GUILayout.Height(18f));
            if (hasVariantAsset)
            {
                EditorGUILayout.ObjectField(label + " JSON", variantAsset, typeof(TextAsset), false);
            }

            if (!string.IsNullOrEmpty(summary.ParseErrorText))
            {
                EditorGUILayout.HelpBox(summary.ParseErrorText, MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRadialStyleVariantRow(
            int index,
            DebugRhythmPrototypeController selectedPrototype)
        {
            TextAsset variantAsset = generatedVariantJsonAssets[index];
            PlannerQualityReport quality = GetRadialVariantQuality(index, variantAsset);
            int encounterCount = GetRadialVariantEncounterCount(index, variantAsset);
            string label = StyleVariantLabels[index];
            bool hasVariantAsset = variantAsset != null;

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(76f));
            EditorGUILayout.LabelField(hasVariantAsset ? "Found" : "Not found", GUILayout.Width(72f));
            EditorGUILayout.LabelField(
                quality == null ? "n/a" : FormatInt(encounterCount),
                GUILayout.Width(72f));
            EditorGUILayout.LabelField(
                quality == null ? "n/a" : FormatInt(quality.totalInputCost),
                GUILayout.Width(52f));
            EditorGUILayout.LabelField(
                quality == null ? "n/a" : quality.result.ToString(),
                GUILayout.Width(112f));
            EditorGUILayout.LabelField(
                quality == null || quality.repairReasons == null
                    ? "n/a"
                    : FormatInt(quality.repairReasons.Count),
                GUILayout.Width(58f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!hasVariantAsset))
            {
                if (GUILayout.Button("Ping / Select", GUILayout.Width(92f)))
                {
                    EditorGUIUtility.PingObject(variantAsset);
                    Selection.activeObject = variantAsset;
                }
                if (GUILayout.Button("Preview", GUILayout.Width(64f)))
                {
                    selectedVariant = (StyleVariantSelection)(index + 1);
                    statusMessage = "Previewing radial variant: " + label + ".";
                }
            }
            using (new EditorGUI.DisabledScope(!hasVariantAsset || selectedPrototype == null))
            {
                if (GUILayout.Button("Assign", GUILayout.Width(58f)))
                {
                    AssignJsonAssetToSelectedPrototype(
                        selectedPrototype,
                        variantAsset,
                        "debugRadialBeatMapJson",
                        "Assign PulseForge " + label + " Radial Beat Map JSON",
                        "Assigned " + label + " radial V2 JSON to selected DebugRhythmPrototypeController.");
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.SelectableLabel(generatedVariantJsonPaths[index], GUILayout.Height(18f));
            EditorGUILayout.EndVertical();
        }

        private void DrawPipelineReports()
        {
            pipelineReportsFoldout = EditorGUILayout.Foldout(pipelineReportsFoldout, "Pipeline Reports", true);
            if (!pipelineReportsFoldout)
            {
                return;
            }

            try
            {
                UpdateReportPathsFromInputs();
                if (pipelineMode == PipelineMode.RadialV2)
                {
                    DrawRadialV2Summary();
                    return;
                }
                EditorGUILayout.SelectableLabel(analysisReportPath, GUILayout.Height(18f));
                EditorGUILayout.SelectableLabel(postprocessReportPath, GUILayout.Height(18f));
                EditorGUILayout.SelectableLabel(compareReportPath, GUILayout.Height(18f));

                if (GUILayout.Button("Refresh Reports"))
                {
                    UpdateReportPathsFromInputs();
                    pipelineReportsError = string.Empty;
                    statusMessage = "Pipeline reports refreshed.";
                }

                if (!string.IsNullOrEmpty(pipelineReportsError))
                {
                    EditorGUILayout.HelpBox(pipelineReportsError, MessageType.Warning);
                }

                pipelineReportSummaryDrawer.Draw(
                    analysisReportPath,
                    postprocessReportPath,
                    compareReportPath,
                    lastRunUsedExpectedCompare || useExpectedCompare);
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception exception)
            {
                pipelineReportsError = "Pipeline reports failed: " + exception.Message;
                EditorGUILayout.HelpBox(pipelineReportsError, MessageType.Warning);
            }
        }

        private void DrawRadialV2Summary()
        {
            EditorGUILayout.LabelField("Radial V2 Quality Summary", EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel(radialAnalysisReportPath, GUILayout.Height(18f));
            EditorGUILayout.SelectableLabel(radialPlannerReportPath, GUILayout.Height(18f));
            if (radialAnalysisResult == null || radialPlanResult == null)
            {
                EditorGUILayout.HelpBox("Run Radial V2 to populate the quality summary.", MessageType.Info);
                return;
            }

            AnalyzerQualityReport analysis = radialAnalysisResult.qualityReport;
            PlannerQualityReport planner = radialPlanResult.qualityReport;
            int encounterCount = radialPlanResult.beatMap == null
                || radialPlanResult.beatMap.encounters == null
                ? 0
                : radialPlanResult.beatMap.encounters.Count;
            EditorGUILayout.LabelField(
                "BPM / confidence",
                analysis.detectedBpm.ToString("0.##", CultureInfo.InvariantCulture)
                + " / "
                + analysis.tempoConfidence.ToString("0.###", CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField(
                "Active / silent",
                FormatSeconds(analysis.activeDurationSeconds)
                + " / "
                + FormatSeconds(analysis.silentDurationSeconds));
            EditorGUILayout.LabelField(
                "Sections / candidates",
                FormatInt(analysis.sectionCount) + " / " + FormatInt(analysis.candidateCount));
            EditorGUILayout.LabelField(
                "Encounters / input cost",
                FormatInt(encounterCount) + " / " + FormatInt(planner.totalInputCost));
            EditorGUILayout.LabelField(
                "Density / longest active gap",
                planner.overallDensity.ToString("0.###", CultureInfo.InvariantCulture)
                + " / "
                + FormatSeconds(planner.longestActiveGapSeconds));
            EditorGUILayout.LabelField(
                "Onset / grid-fill ratio",
                planner.onsetToGridFillRatio.ToString("0.###", CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Result", planner.result.ToString());
            EditorGUILayout.LabelField(
                "Repairs",
                planner.repairReasons == null ? "0" : FormatInt(planner.repairReasons.Count));
            EditorGUILayout.LabelField(
                "Actions",
                FormatActionDistribution(planner.actionCounts));
            EditorGUILayout.LabelField(
                "Event types",
                FormatEventTypeDistribution(planner.eventTypeCounts));
            EditorGUILayout.LabelField(
                "Saboteur / Fog effects / duration",
                FormatInt(planner.saboteurEncounterCount)
                + " / "
                + FormatInt(planner.fogFailureEffectCount)
                + " / "
                + FormatSeconds(planner.totalFogDurationSeconds));
        }

        private void DrawBeatmapTimelinePreview()
        {
            timelinePreviewFoldout = EditorGUILayout.Foldout(timelinePreviewFoldout, "Beatmap Timeline Preview", true);
            if (!timelinePreviewFoldout)
            {
                return;
            }

            try
            {
                TryLoadGeneratedJsonAssets();
                TryLoadStyleVariantAssets();
                TextAsset previewPlayableAsset = GetPreviewPlayableJsonAsset();
                string previewPlayablePath = GetPreviewPlayableJsonPath();
                string previewLabel = GetPreviewLabel();
                if (pipelineMode == PipelineMode.LegacyPythonV1)
                {
                    DrawGeneratedAssetStatus("Raw JSON Asset", generatedRawJsonAsset, generatedRawJsonPath);
                }
                DrawGeneratedAssetStatus(previewLabel + " JSON Asset", previewPlayableAsset, previewPlayablePath);
                EditorGUILayout.LabelField("Previewing: " + previewLabel, EditorStyles.miniBoldLabel);

                if (GUILayout.Button("Refresh Generated Assets"))
                {
                    AssetDatabase.Refresh();
                    TryLoadGeneratedJsonAssets();
                    TryLoadStyleVariantAssets();
                    timelinePreviewError = string.Empty;
                    statusMessage = "Generated assets refreshed.";
                }

                if (!string.IsNullOrEmpty(timelinePreviewError))
                {
                    EditorGUILayout.HelpBox(timelinePreviewError, MessageType.Warning);
                }

                if (pipelineMode == PipelineMode.RadialV2)
                {
                    timelinePreviewDrawer.DrawRadial(previewPlayableAsset, 180f);
                }
                else
                {
                    timelinePreviewDrawer.Draw(generatedRawJsonAsset, previewPlayableAsset, 180f);
                }
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception exception)
            {
                timelinePreviewError = "Beatmap timeline preview failed: " + exception.Message;
                EditorGUILayout.HelpBox(timelinePreviewError, MessageType.Warning);
            }
        }

        private TextAsset GetPreviewPlayableJsonAsset()
        {
            int selectedIndex = GetSelectedVariantIndex();
            return selectedIndex < 0 ? generatedPlayableJsonAsset : generatedVariantJsonAssets[selectedIndex];
        }

        private string GetPreviewPlayableJsonPath()
        {
            int selectedIndex = GetSelectedVariantIndex();
            return selectedIndex < 0 ? generatedPlayableJsonPath : generatedVariantJsonPaths[selectedIndex];
        }

        private string GetPreviewLabel()
        {
            int selectedIndex = GetSelectedVariantIndex();
            return selectedIndex < 0
                ? pipelineMode == PipelineMode.RadialV2 ? "Radial V2" : "Playable"
                : StyleVariantLabels[selectedIndex];
        }

        private int GetSelectedVariantIndex()
        {
            return selectedVariant == StyleVariantSelection.None ? -1 : (int)selectedVariant - 1;
        }

        private static void DrawGeneratedAssetStatus(string label, TextAsset asset, string assetPath)
        {
            EditorGUILayout.LabelField(label + " Status", asset == null ? "Not found" : "Found");
            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(assetPath) ? "(path unavailable)" : assetPath, GUILayout.Height(18f));

            if (asset != null)
            {
                EditorGUILayout.ObjectField(label, asset, typeof(TextAsset), false);
            }
        }

        private void TryLoadGeneratedJsonAssets()
        {
            UpdateGeneratedJsonPathsFromInputs();
            generatedRawJsonAsset = LoadTextAssetAtPath(generatedRawJsonPath);
            generatedPlayableJsonAsset = LoadTextAssetAtPath(generatedPlayableJsonPath);
        }

        private void TryLoadStyleVariantAssets()
        {
            UpdateStyleVariantPathsFromInputs();
            for (int i = 0; i < StyleVariantCount; i++)
            {
                generatedVariantJsonAssets[i] = LoadTextAssetAtPath(generatedVariantJsonPaths[i]);
            }
        }

        private void ClearStyleVariantAssets()
        {
            for (int i = 0; i < StyleVariantCount; i++)
            {
                generatedVariantJsonAssets[i] = null;
            }
        }

        private bool AreAllStyleVariantAssetsLoaded()
        {
            for (int i = 0; i < StyleVariantCount; i++)
            {
                if (generatedVariantJsonAssets[i] == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static TextAsset LoadTextAssetAtPath(string assetPath)
        {
            return string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        }

        private void UpdateGeneratedJsonPathsFromInputs()
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                SetGeneratedPath(ref generatedRawJsonPath, ref generatedRawJsonAsset, string.Empty);
                SetGeneratedPath(ref generatedPlayableJsonPath, ref generatedPlayableJsonAsset, string.Empty);
                return;
            }

            string safeOutputName = GetSafeOutputName();
            string safeOutputDirectory = outputDirectory.Trim();
            SetGeneratedPath(
                ref generatedRawJsonPath,
                ref generatedRawJsonAsset,
                pipelineMode == PipelineMode.RadialV2
                    ? string.Empty
                    : BuildGeneratedJsonPath(safeOutputDirectory, "BM_Raw_", safeOutputName));
            SetGeneratedPath(
                ref generatedPlayableJsonPath,
                ref generatedPlayableJsonAsset,
                BuildGeneratedJsonPath(
                    safeOutputDirectory,
                    pipelineMode == PipelineMode.RadialV2 ? "BM_Radial_" : "BM_Playable_",
                    safeOutputName));
        }

        private void UpdateStyleVariantPathsFromInputs()
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                for (int i = 0; i < StyleVariantCount; i++)
                {
                    SetVariantGeneratedPath(i, string.Empty);
                }

                return;
            }

            string safeOutputName = GetSafeOutputName();
            string safeOutputDirectory = outputDirectory.Trim();
            for (int i = 0; i < StyleVariantCount; i++)
            {
                SetVariantGeneratedPath(
                    i,
                    pipelineMode == PipelineMode.RadialV2
                        ? BuildRadialStyleVariantJsonPath(
                            safeOutputDirectory,
                            safeOutputName,
                            StyleVariantLabels[i])
                        : BuildStyleVariantJsonPath(
                            safeOutputDirectory,
                            safeOutputName,
                            StyleVariantLabels[i]));
            }
        }

        private void SetVariantGeneratedPath(int index, string newPath)
        {
            if (string.Equals(generatedVariantJsonPaths[index], newPath, StringComparison.Ordinal))
            {
                return;
            }

            generatedVariantJsonPaths[index] = newPath;
            generatedVariantJsonAssets[index] = null;
        }

        private StyleVariantSummary BuildStyleVariantSummary(int index, TextAsset variantAsset)
        {
            StyleVariantSummary summary = new StyleVariantSummary();
            summary.DroppedCountText = GetStyleVariantDroppedCountText(index);

            if (variantAsset == null)
            {
                return summary;
            }

            try
            {
                IReadOnlyList<BeatEventData> events = DebugBeatMapJsonParser.BuildBeatEvents(variantAsset.text);
                summary.SetEvents(events);
            }
            catch (Exception exception)
            {
                summary.SetParseError("Parse error: " + exception.Message);
            }

            return summary;
        }

        private string GetStyleVariantDroppedCountText(int index)
        {
            string safeOutputName = GetSafeOutputName();
            bool sawReportError = false;

            if (TryReadStyleVariantDroppedCount(
                    BuildStyleVariantReportPath(safeOutputName, StyleVariantCliValues[index]),
                    out int droppedCount,
                    out bool reportExists,
                    out bool reportError))
            {
                return FormatInt(droppedCount);
            }

            sawReportError |= reportExists && reportError;

            if (TryReadStyleVariantDroppedCount(
                    BuildStyleVariantReportPath(safeOutputName, StyleVariantLabels[index]),
                    out droppedCount,
                    out reportExists,
                    out reportError))
            {
                return FormatInt(droppedCount);
            }

            sawReportError |= reportExists && reportError;
            return sawReportError ? "report error" : "n/a";
        }

        private static bool TryReadStyleVariantDroppedCount(
            string reportPath,
            out int droppedCount,
            out bool reportExists,
            out bool reportError)
        {
            droppedCount = 0;
            reportExists = false;
            reportError = false;

            string fullPath = ToProjectFullPath(reportPath);
            if (!File.Exists(fullPath))
            {
                return false;
            }

            reportExists = true;
            try
            {
                string json = File.ReadAllText(fullPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    reportError = true;
                    return false;
                }

                StyleVariantPostprocessReport report = JsonUtility.FromJson<StyleVariantPostprocessReport>(json);
                if (report == null)
                {
                    reportError = true;
                    return false;
                }

                droppedCount = report.droppedEventCount;
                return true;
            }
            catch (Exception)
            {
                reportError = true;
                return false;
            }
        }

        private void UpdateReportPathsFromInputs()
        {
            string safeOutputName = GetSafeOutputName();
            if (pipelineMode == PipelineMode.RadialV2)
            {
                string directory = string.IsNullOrWhiteSpace(outputDirectory)
                    ? string.Empty
                    : outputDirectory.Trim();
                radialAnalysisReportPath = string.IsNullOrEmpty(directory)
                    ? string.Empty
                    : BuildRadialReportPath(directory, safeOutputName, "_analysis_v2_report.json");
                radialPlannerReportPath = string.IsNullOrEmpty(directory)
                    ? string.Empty
                    : BuildRadialReportPath(directory, safeOutputName, "_planner_v2_report.json");
                return;
            }
            analysisReportPath = BuildReportPath(safeOutputName, "_analysis_report.json");
            postprocessReportPath = BuildReportPath(safeOutputName, "_postprocess_report.json");
            compareReportPath = BuildReportPath(safeOutputName, "_compare_report.json");
        }

        private static void SetGeneratedPath(ref string currentPath, ref TextAsset currentAsset, string newPath)
        {
            if (string.Equals(currentPath, newPath, StringComparison.Ordinal))
            {
                return;
            }

            currentPath = newPath;
            currentAsset = null;
        }

        private static DebugRhythmPrototypeController GetSelectedDebugPrototypeController()
        {
            GameObject selectedGameObject = Selection.activeGameObject;
            return selectedGameObject == null ? null : selectedGameObject.GetComponent<DebugRhythmPrototypeController>();
        }

        private void AssignGeneratedJsonToSelectedPrototype(DebugRhythmPrototypeController selectedPrototype)
        {
            if (generatedPlayableJsonAsset == null)
            {
                statusMessage = "Generated playable JSON asset is not available yet.";
                return;
            }

            AssignJsonAssetToSelectedPrototype(
                selectedPrototype,
                generatedPlayableJsonAsset,
                pipelineMode == PipelineMode.RadialV2
                    ? "debugRadialBeatMapJson"
                    : "debugBeatMapJson",
                pipelineMode == PipelineMode.RadialV2
                    ? "Assign PulseForge Radial Beat Map JSON"
                    : "Assign PulseForge Beat Map JSON",
                pipelineMode == PipelineMode.RadialV2
                    ? "Assigned radial V2 JSON to selected DebugRhythmPrototypeController."
                    : "Assigned generated JSON to selected DebugRhythmPrototypeController.");
        }

        private void AssignJsonAssetToSelectedPrototype(
            DebugRhythmPrototypeController selectedPrototype,
            TextAsset jsonAsset,
            string propertyName,
            string undoName,
            string successMessage)
        {
            if (jsonAsset == null)
            {
                statusMessage = "JSON asset is not available yet.";
                return;
            }

            if (selectedPrototype == null)
            {
                statusMessage = "Select a GameObject with DebugRhythmPrototypeController to assign.";
                return;
            }

            SerializedObject serializedObject = new SerializedObject(selectedPrototype);
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                statusMessage = "Could not find serialized property: " + propertyName + ".";
                EditorUtility.DisplayDialog("PulseForge Audio Pipeline", statusMessage, "OK");
                return;
            }

            Undo.RecordObject(selectedPrototype, undoName);
            property.objectReferenceValue = jsonAsset;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(selectedPrototype);
            statusMessage = successMessage;
        }

        private bool TryBuildCommand(out string arguments, out string rawJsonPath, out string playableJsonPath, out string validationError)
        {
            arguments = string.Empty;
            rawJsonPath = string.Empty;
            playableJsonPath = string.Empty;
            validationError = string.Empty;

            if (inputAudioClip == null)
            {
                validationError = "Input Audio Clip is required.";
                return false;
            }

            string audioPath = AssetDatabase.GetAssetPath(inputAudioClip);
            if (string.IsNullOrEmpty(audioPath))
            {
                validationError = "Input Audio Clip must be a project asset.";
                return false;
            }

            if (!string.Equals(Path.GetExtension(audioPath), ".wav", StringComparison.OrdinalIgnoreCase))
            {
                validationError = "Input Audio Clip must be a WAV asset for this first version.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(pythonExecutable))
            {
                validationError = "Python Executable must not be empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                validationError = "Output Directory must not be empty.";
                return false;
            }

            string safeOutputName = GetSafeOutputName();
            string projectRoot = GetProjectRoot();
            string scriptFullPath = Path.Combine(projectRoot, PipelineScriptPath);
            if (!File.Exists(scriptFullPath))
            {
                validationError = "Pipeline script was not found: " + PipelineScriptPath;
                return false;
            }

            StringBuilder builder = new StringBuilder();
            AppendArgument(builder, PipelineScriptPath);
            AppendOption(builder, "--input-wav", audioPath);
            AppendOption(builder, "--output-dir", outputDirectory.Trim());
            AppendOption(builder, "--name", safeOutputName);
            AppendOption(builder, "--combat-style", ToCliValue(combatStyle));
            AppendOption(builder, "--detection-mode", ToCliValue(detectionMode));
            AppendOption(builder, "--difficulty", ToCliValue(difficulty));
            if (combatStyle == CombatStyle.Legacy)
            {
                AppendOption(builder, "--pattern", pattern);
                AppendOption(builder, "--action-mode", ToCliValue(actionMode));
            }
            else
            {
                if (!IsValidBurstWindowSeconds(burstWindowSeconds))
                {
                    validationError = "Burst Window Seconds must be a finite number greater than or equal to zero.";
                    return false;
                }

                AppendOption(builder, "--burst-window-seconds", ToCliFloat(burstWindowSeconds));
            }

            AppendArgument(builder, "--summary");

            if (writeDebugCsv)
            {
                AppendArgument(builder, "--write-debug-csv");
            }

            if (useExpectedCompare)
            {
                if (expectedBeatMapJson == null)
                {
                    validationError = "Expected Beat Map JSON is required when Use Expected Compare is enabled.";
                    return false;
                }

                string expectedPath = AssetDatabase.GetAssetPath(expectedBeatMapJson);
                if (string.IsNullOrEmpty(expectedPath))
                {
                    validationError = "Expected Beat Map JSON must be a project asset.";
                    return false;
                }

                AppendOption(builder, "--expected-json", expectedPath);
            }

            arguments = builder.ToString();
            rawJsonPath = BuildGeneratedJsonPath(outputDirectory.Trim(), "BM_Raw_", safeOutputName);
            playableJsonPath = BuildGeneratedJsonPath(outputDirectory.Trim(), "BM_Playable_", safeOutputName);
            return true;
        }

        private bool TryValidateRadialInputs(
            out string audioAssetPath,
            out string validationError)
        {
            audioAssetPath = string.Empty;
            validationError = string.Empty;
            if (inputAudioClip == null)
            {
                validationError = "Input Audio Clip is required.";
                return false;
            }

            audioAssetPath = AssetDatabase.GetAssetPath(inputAudioClip);
            if (string.IsNullOrWhiteSpace(audioAssetPath))
            {
                validationError = "Input Audio Clip must be a project asset.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                validationError = "Output Directory must not be empty.";
                return false;
            }
            if (!IsProjectAssetOutputDirectory(outputDirectory))
            {
                validationError = "Radial V2 Output Directory must stay inside Assets.";
                return false;
            }

            string safeOutputName = GetSafeOutputName();
            if (safeOutputName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || safeOutputName.IndexOf('/') >= 0
                || safeOutputName.IndexOf('\\') >= 0)
            {
                validationError = "Output Name contains invalid file-name characters.";
                return false;
            }
            return true;
        }

        private bool TryBuildStyleVariantsCommand(out string arguments, out string validationError)
        {
            arguments = string.Empty;
            validationError = string.Empty;

            if (inputAudioClip == null)
            {
                validationError = "Input Audio Clip is required.";
                return false;
            }

            string audioPath = AssetDatabase.GetAssetPath(inputAudioClip);
            if (string.IsNullOrEmpty(audioPath))
            {
                validationError = "Input Audio Clip must be a project asset.";
                return false;
            }

            if (!string.Equals(Path.GetExtension(audioPath), ".wav", StringComparison.OrdinalIgnoreCase))
            {
                validationError = "Input Audio Clip must be a WAV asset for this first version.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(pythonExecutable))
            {
                validationError = "Python Executable must not be empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                validationError = "Output Directory must not be empty.";
                return false;
            }

            if (!IsValidBurstWindowSeconds(burstWindowSeconds))
            {
                validationError = "Burst Window Seconds must be a finite number greater than or equal to zero.";
                return false;
            }

            string projectRoot = GetProjectRoot();
            string scriptFullPath = Path.Combine(projectRoot, StyleVariantsScriptPath);
            if (!File.Exists(scriptFullPath))
            {
                validationError = "Style variant generator script was not found: " + StyleVariantsScriptPath;
                return false;
            }

            StringBuilder builder = new StringBuilder();
            AppendArgument(builder, StyleVariantsScriptPath);
            AppendOption(builder, "--input-wav", audioPath);
            AppendOption(builder, "--output-dir", outputDirectory.Trim());
            AppendOption(builder, "--name", GetSafeOutputName());
            AppendOption(builder, "--difficulty", ToCliValue(difficulty));
            AppendOption(builder, "--detection-mode", ToCliValue(detectionMode));
            AppendOption(builder, "--burst-window-seconds", ToCliFloat(burstWindowSeconds));
            AppendArgument(builder, "--summary");

            if (useExpectedCompare)
            {
                if (expectedBeatMapJson == null)
                {
                    validationError = "Expected Beat Map JSON is required when Use Expected Compare is enabled.";
                    return false;
                }

                string expectedPath = AssetDatabase.GetAssetPath(expectedBeatMapJson);
                if (string.IsNullOrEmpty(expectedPath))
                {
                    validationError = "Expected Beat Map JSON must be a project asset.";
                    return false;
                }

                AppendOption(builder, "--expected-json", expectedPath);
            }

            arguments = builder.ToString();
            return true;
        }

        private string GetSafeOutputName()
        {
            return string.IsNullOrWhiteSpace(outputName) ? DefaultOutputName : outputName.Trim();
        }

        private static string BuildGeneratedJsonPath(string outputDirectoryPath, string filePrefix, string safeOutputName)
        {
            return Path.Combine(outputDirectoryPath, filePrefix + safeOutputName + ".json").Replace('\\', '/');
        }

        private static string BuildStyleVariantJsonPath(string outputDirectoryPath, string safeOutputName, string styleLabel)
        {
            return Path.Combine(outputDirectoryPath, "BM_Playable_" + safeOutputName + "_" + styleLabel + ".json").Replace('\\', '/');
        }

        private static string BuildRadialStyleVariantJsonPath(
            string outputDirectoryPath,
            string safeOutputName,
            string styleLabel)
        {
            return Path.Combine(
                outputDirectoryPath,
                "BM_Radial_" + safeOutputName + "_" + styleLabel + ".json").Replace('\\', '/');
        }

        private static string BuildRadialStyleVariantReportPath(
            string outputDirectoryPath,
            string safeOutputName,
            string styleLabel)
        {
            return BuildRadialReportPath(
                outputDirectoryPath,
                safeOutputName + "_" + styleLabel,
                "_planner_v2_report.json");
        }

        private static string BuildRadialReportPath(
            string outputDirectoryPath,
            string safeOutputName,
            string suffix)
        {
            return Path.Combine(outputDirectoryPath, safeOutputName + suffix).Replace('\\', '/');
        }

        private static string BuildStyleVariantReportPath(string safeOutputName, string styleName)
        {
            return Path.Combine(ReportDirectory, safeOutputName + "_" + styleName + "_postprocess_report.json").Replace('\\', '/');
        }

        private static string BuildReportPath(string safeOutputName, string suffix)
        {
            return Path.Combine(ReportDirectory, safeOutputName + suffix).Replace('\\', '/');
        }

        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        private static string ToProjectFullPath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.Combine(GetProjectRoot(), path);
        }

        private static bool IsProjectAssetOutputDirectory(string path)
        {
            try
            {
                string projectRoot = Path.GetFullPath(GetProjectRoot());
                string assetsRoot = Path.GetFullPath(Path.Combine(projectRoot, "Assets"));
                string candidate = Path.GetFullPath(Path.Combine(projectRoot, path.Trim()));
                return candidate.Equals(assetsRoot, StringComparison.OrdinalIgnoreCase)
                    || candidate.StartsWith(
                        assetsRoot + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void WriteProjectJson(string assetPath, string json)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new ArgumentException("Output asset path is empty.", nameof(assetPath));
            }
            string fullPath = ToProjectFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, json ?? string.Empty, new UTF8Encoding(false));
        }

        private void ClearRadialVariantResults()
        {
            for (int i = 0; i < radialVariantResults.Length; i++)
            {
                radialVariantResults[i] = null;
            }
        }

        private PlannerQualityReport GetRadialVariantQuality(int index, TextAsset asset)
        {
            if (index >= 0
                && index < radialVariantResults.Length
                && radialVariantResults[index] != null)
            {
                return radialVariantResults[index].qualityReport;
            }
            return TryGetRadialArtifact(asset, out RadialBeatMapCacheData artifact)
                ? artifact.plannerQuality
                : null;
        }

        private int GetRadialVariantEncounterCount(int index, TextAsset asset)
        {
            if (index >= 0
                && index < radialVariantResults.Length
                && radialVariantResults[index] != null
                && radialVariantResults[index].beatMap != null
                && radialVariantResults[index].beatMap.encounters != null)
            {
                return radialVariantResults[index].beatMap.encounters.Count;
            }
            return TryGetRadialArtifact(asset, out RadialBeatMapCacheData artifact)
                && artifact.radialBeatMap != null
                && artifact.radialBeatMap.encounters != null
                ? artifact.radialBeatMap.encounters.Count
                : 0;
        }

        private static bool TryGetRadialArtifact(
            TextAsset asset,
            out RadialBeatMapCacheData artifact)
        {
            artifact = null;
            return asset != null
                && RadialBeatMapArtifactSerializer.TryDeserialize(
                    asset.text,
                    out artifact,
                    out _);
        }

        private static RuntimeDetectionMode ToRuntimeDetectionMode(DetectionMode value)
        {
            return value == DetectionMode.Onset
                ? RuntimeDetectionMode.Onset
                : RuntimeDetectionMode.Amplitude;
        }

        private static BeatMapDifficulty ToPlannerDifficulty(Difficulty value)
        {
            switch (value)
            {
                case Difficulty.Easy:
                    return BeatMapDifficulty.Easy;
                case Difficulty.Hard:
                    return BeatMapDifficulty.Hard;
                default:
                    return BeatMapDifficulty.Normal;
            }
        }

        private static PulseForge.BeatMapGeneration.CombatStyle ToPlannerCombatStyle(
            CombatStyle value)
        {
            switch (value)
            {
                case CombatStyle.Balanced:
                    return PulseForge.BeatMapGeneration.CombatStyle.Balanced;
                case CombatStyle.Defensive:
                    return PulseForge.BeatMapGeneration.CombatStyle.Defensive;
                case CombatStyle.Aggressive:
                    return PulseForge.BeatMapGeneration.CombatStyle.Aggressive;
                case CombatStyle.Bursty:
                    return PulseForge.BeatMapGeneration.CombatStyle.Bursty;
                default:
                    return PulseForge.BeatMapGeneration.CombatStyle.Legacy;
            }
        }

        private static CombatStyle GetStyleVariantCombatStyle(int index)
        {
            switch (index)
            {
                case 0:
                    return CombatStyle.Balanced;
                case 1:
                    return CombatStyle.Defensive;
                case 2:
                    return CombatStyle.Aggressive;
                default:
                    return CombatStyle.Bursty;
            }
        }

        private string BuildRadialPresetId(CombatStyle style)
        {
            return "editor-v2-"
                + ToCliValue(detectionMode)
                + "-"
                + ToCliValue(difficulty)
                + "-"
                + ToCliValue(style);
        }

        private static string FormatActionDistribution(IReadOnlyList<ActionCountData> counts)
        {
            if (counts == null || counts.Count == 0)
            {
                return "none";
            }
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < counts.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }
                builder.Append(counts[i].action).Append('=').Append(counts[i].count);
            }
            return builder.ToString();
        }

        private static string FormatEventTypeDistribution(IReadOnlyList<EventTypeCountData> counts)
        {
            if (counts == null || counts.Count == 0)
            {
                return "none";
            }
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < counts.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }
                builder.Append(counts[i].eventType).Append('=').Append(counts[i].count);
            }
            return builder.ToString();
        }

        private static void AppendOption(StringBuilder builder, string option, string value)
        {
            AppendArgument(builder, option);
            AppendArgument(builder, value ?? string.Empty);
        }

        private static void AppendArgument(StringBuilder builder, string value)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(QuoteArgument(value));
        }

        private static string QuoteArgument(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            bool needsQuotes = value.IndexOfAny(new[] { ' ', '\t', '\n', '\r', '"' }) >= 0;
            if (!needsQuotes)
            {
                return value;
            }

            StringBuilder builder = new StringBuilder();
            builder.Append('"');

            int backslashCount = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (character == '\\')
                {
                    backslashCount++;
                    continue;
                }

                if (character == '"')
                {
                    builder.Append('\\', backslashCount * 2 + 1);
                    builder.Append('"');
                    backslashCount = 0;
                    continue;
                }

                builder.Append('\\', backslashCount);
                backslashCount = 0;
                builder.Append(character);
            }

            builder.Append('\\', backslashCount * 2);
            builder.Append('"');
            return builder.ToString();
        }

        private MessageType GetStatusMessageType()
        {
            if (statusMessage.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0
                || statusMessage.IndexOf("required", StringComparison.OrdinalIgnoreCase) >= 0
                || statusMessage.IndexOf("must", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return MessageType.Error;
            }

            if (statusMessage.IndexOf("completed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return MessageType.Info;
            }

            return MessageType.None;
        }

        private static string ToCliValue(DetectionMode value)
        {
            return value == DetectionMode.Onset ? "onset" : "amplitude";
        }

        private static string ToCliValue(Difficulty value)
        {
            switch (value)
            {
                case Difficulty.Easy:
                    return "easy";
                case Difficulty.Hard:
                    return "hard";
                default:
                    return "normal";
            }
        }

        private static string ToCliValue(ActionMode value)
        {
            switch (value)
            {
                case ActionMode.Alternate:
                    return "alternate";
                case ActionMode.Pattern:
                    return "pattern";
                case ActionMode.Intensity:
                    return "intensity";
                default:
                    return "preserve";
            }
        }

        private static string ToCliValue(CombatStyle value)
        {
            switch (value)
            {
                case CombatStyle.Balanced:
                    return "balanced";
                case CombatStyle.Defensive:
                    return "defensive";
                case CombatStyle.Aggressive:
                    return "aggressive";
                case CombatStyle.Bursty:
                    return "bursty";
                default:
                    return "legacy";
            }
        }

        private static string ToCliFloat(float value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string FormatInt(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatSeconds(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture) + "s";
        }

        private static bool IsValidBurstWindowSeconds(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        private sealed class StyleVariantSummary
        {
            public string EventCountText { get; private set; } = "n/a";

            public string GuardCountText { get; private set; } = "n/a";

            public string StrikeCountText { get; private set; } = "n/a";

            public string FirstTimeText { get; private set; } = "n/a";

            public string LastTimeText { get; private set; } = "n/a";

            public string DroppedCountText { get; set; } = "n/a";

            public string ParseErrorText { get; private set; } = string.Empty;

            public void SetEvents(IReadOnlyList<BeatEventData> events)
            {
                int eventCount = events == null ? 0 : events.Count;
                int guardCount = 0;
                int strikeCount = 0;
                double firstTimeSeconds = double.MaxValue;
                double lastTimeSeconds = double.MinValue;

                for (int i = 0; i < eventCount; i++)
                {
                    BeatEventData beatEvent = events[i];
                    if (beatEvent.Action == RhythmAction.Guard)
                    {
                        guardCount++;
                    }
                    else if (beatEvent.Action == RhythmAction.Strike)
                    {
                        strikeCount++;
                    }

                    firstTimeSeconds = Math.Min(firstTimeSeconds, beatEvent.TargetTimeSeconds);
                    lastTimeSeconds = Math.Max(lastTimeSeconds, beatEvent.TargetTimeSeconds);
                }

                EventCountText = FormatInt(eventCount);
                GuardCountText = FormatInt(guardCount);
                StrikeCountText = FormatInt(strikeCount);
                FirstTimeText = eventCount == 0 ? "n/a" : FormatSeconds(firstTimeSeconds);
                LastTimeText = eventCount == 0 ? "n/a" : FormatSeconds(lastTimeSeconds);
                ParseErrorText = string.Empty;
            }

            public void SetParseError(string errorText)
            {
                EventCountText = "error";
                GuardCountText = "error";
                StrikeCountText = "error";
                FirstTimeText = "error";
                LastTimeText = "error";
                ParseErrorText = errorText;
            }
        }

        [Serializable]
        private sealed class StyleVariantPostprocessReport
        {
            public int droppedEventCount = 0;
        }

        private enum DetectionMode
        {
            Amplitude,
            Onset
        }

        private enum PipelineMode
        {
            RadialV2,
            LegacyPythonV1
        }

        private enum Difficulty
        {
            Easy,
            Normal,
            Hard
        }

        private enum ActionMode
        {
            Preserve,
            Alternate,
            Pattern,
            Intensity
        }

        private enum CombatStyle
        {
            Legacy,
            Balanced,
            Defensive,
            Aggressive,
            Bursty
        }

        private enum StyleVariantSelection
        {
            None,
            Balanced,
            Defensive,
            Aggressive,
            Bursty
        }
    }
}
