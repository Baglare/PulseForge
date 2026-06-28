using System;
using System.Diagnostics;
using System.IO;
using System.Text;
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
        private const string DefaultOutputDirectory = "Assets/PulseForge/Demo/BeatMaps";
        private const string DefaultOutputName = "Debug_120BPM";
        private const string DefaultPattern = "Guard,Guard,Strike,Guard,Strike,Strike,Guard,Strike,Guard,Strike";

        private AudioClip inputAudioClip;
        private TextAsset expectedBeatMapJson;
        private string outputName = DefaultOutputName;
        private string pattern = DefaultPattern;
        private DetectionMode detectionMode = DetectionMode.Amplitude;
        private Difficulty difficulty = Difficulty.Normal;
        private ActionMode actionMode = ActionMode.Pattern;
        private bool writeDebugCsv;
        private bool useExpectedCompare;
        private string pythonExecutable = "python";
        private string outputDirectory = DefaultOutputDirectory;
        private string generatedRawJsonPath = string.Empty;
        private string generatedPlayableJsonPath = string.Empty;
        private TextAsset generatedRawJsonAsset;
        private TextAsset generatedPlayableJsonAsset;
        private string statusMessage = "Ready";
        private string lastStdout = string.Empty;
        private string lastStderr = string.Empty;
        private string timelinePreviewError = string.Empty;
        private Vector2 windowScroll;
        private Vector2 outputScroll;
        private bool timelinePreviewFoldout = true;
        private readonly BeatmapTimelinePreviewDrawer timelinePreviewDrawer = new BeatmapTimelinePreviewDrawer();

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

            inputAudioClip = (AudioClip)EditorGUILayout.ObjectField("Input Audio Clip", inputAudioClip, typeof(AudioClip), false);
            expectedBeatMapJson = (TextAsset)EditorGUILayout.ObjectField("Expected Beat Map JSON", expectedBeatMapJson, typeof(TextAsset), false);
            outputName = EditorGUILayout.TextField("Output Name", outputName);
            pattern = EditorGUILayout.TextField("Pattern", pattern);
            detectionMode = (DetectionMode)EditorGUILayout.EnumPopup("Detection Mode", detectionMode);
            difficulty = (Difficulty)EditorGUILayout.EnumPopup("Difficulty", difficulty);
            actionMode = (ActionMode)EditorGUILayout.EnumPopup("Action Mode", actionMode);
            writeDebugCsv = EditorGUILayout.Toggle("Write Debug CSV", writeDebugCsv);
            useExpectedCompare = EditorGUILayout.Toggle("Use Expected Compare", useExpectedCompare);
            pythonExecutable = EditorGUILayout.TextField("Python Executable", pythonExecutable);
            outputDirectory = EditorGUILayout.TextField("Output Directory", outputDirectory);
            UpdateGeneratedJsonPathsFromInputs();

            EditorGUILayout.Space();
            if (GUILayout.Button("Run Pipeline"))
            {
                RunPipeline();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generated Playable JSON", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(generatedPlayableJsonPath) ? "(not generated yet)" : generatedPlayableJsonPath, GUILayout.Height(18f));
            DrawGeneratedJsonWorkflow();

            EditorGUILayout.Space();
            DrawBeatmapTimelinePreview();

            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(statusMessage, GetStatusMessageType());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Process Output", EditorStyles.boldLabel);
            outputScroll = EditorGUILayout.BeginScrollView(outputScroll);
            EditorGUILayout.LabelField("stdout", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(lastStdout, GUILayout.MinHeight(120f));
            EditorGUILayout.LabelField("stderr", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(lastStderr, GUILayout.MinHeight(120f));
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndScrollView();
        }

        private void RunPipeline()
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
                DrawGeneratedAssetStatus("Raw JSON Asset", generatedRawJsonAsset, generatedRawJsonPath);
                DrawGeneratedAssetStatus("Playable JSON Asset", generatedPlayableJsonAsset, generatedPlayableJsonPath);

                if (GUILayout.Button("Refresh Generated Assets"))
                {
                    AssetDatabase.Refresh();
                    TryLoadGeneratedJsonAssets();
                    timelinePreviewError = string.Empty;
                    statusMessage = "Generated assets refreshed.";
                }

                if (!string.IsNullOrEmpty(timelinePreviewError))
                {
                    EditorGUILayout.HelpBox(timelinePreviewError, MessageType.Warning);
                }

                timelinePreviewDrawer.Draw(generatedRawJsonAsset, generatedPlayableJsonAsset, 180f);
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
                BuildGeneratedJsonPath(safeOutputDirectory, "BM_Raw_", safeOutputName));
            SetGeneratedPath(
                ref generatedPlayableJsonPath,
                ref generatedPlayableJsonAsset,
                BuildGeneratedJsonPath(safeOutputDirectory, "BM_Playable_", safeOutputName));
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

            if (selectedPrototype == null)
            {
                statusMessage = "Select a GameObject with DebugRhythmPrototypeController to assign.";
                return;
            }

            SerializedObject serializedObject = new SerializedObject(selectedPrototype);
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty("debugBeatMapJson");
            if (property == null)
            {
                statusMessage = "Could not find serialized property: debugBeatMapJson.";
                EditorUtility.DisplayDialog("PulseForge Audio Pipeline", statusMessage, "OK");
                return;
            }

            Undo.RecordObject(selectedPrototype, "Assign PulseForge Beat Map JSON");
            property.objectReferenceValue = generatedPlayableJsonAsset;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(selectedPrototype);
            statusMessage = "Assigned generated JSON to selected DebugRhythmPrototypeController.";
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
            AppendOption(builder, "--pattern", pattern);
            AppendOption(builder, "--detection-mode", ToCliValue(detectionMode));
            AppendOption(builder, "--difficulty", ToCliValue(difficulty));
            AppendOption(builder, "--action-mode", ToCliValue(actionMode));
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

        private string GetSafeOutputName()
        {
            return string.IsNullOrWhiteSpace(outputName) ? DefaultOutputName : outputName.Trim();
        }

        private static string BuildGeneratedJsonPath(string outputDirectoryPath, string filePrefix, string safeOutputName)
        {
            return Path.Combine(outputDirectoryPath, filePrefix + safeOutputName + ".json").Replace('\\', '/');
        }

        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
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

        private enum DetectionMode
        {
            Amplitude,
            Onset
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
    }
}
