using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using PulseForge.Domain.Rhythm;
using UnityEngine;
using UnityEngine.Networking;

namespace PulseForge.Runtime.Unity.Audio
{
    internal static class RuntimeAudioImportService
    {
        private const long MaximumSourceFileBytes = 256L * 1024L * 1024L;
        private const float MaximumAudioDurationSeconds = 15f * 60f;
        private const int ConversionTimeoutMilliseconds = 10 * 60 * 1000;
        private const int MaximumErrorLength = 600;

        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".wav",
            ".mp3",
            ".m4a",
            ".aac",
            ".flac",
            ".ogg",
            ".opus",
            ".wma",
            ".aif",
            ".aiff"
        };

        public static IEnumerator ImportAudio(
            string sourcePath,
            RuntimeAudioPipelineSettings pipelineSettings,
            Action<string> statusChanged,
            Action<RuntimeAudioImportResult> completed,
            Action<string> failed)
        {
            string validationError = ValidateSourcePath(sourcePath);
            if (!string.IsNullOrEmpty(validationError))
            {
                failed(validationError);
                yield break;
            }

            string ffmpegPath = ResolveFfmpegExecutable();
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                failed("FFmpeg was not found. Run tools/runtime_audio/setup_ffmpeg.ps1 before building.");
                yield break;
            }

            string outputDirectory = Path.Combine(Application.persistentDataPath, "ImportedAudio");
            string outputPath = Path.Combine(outputDirectory, "pulseforge-imported.wav");
            statusChanged("Converting to WAV...");

            Task<ConversionResult> conversionTask = Task.Run(
                () => ConvertToWav(ffmpegPath, sourcePath, outputDirectory, outputPath));

            while (!conversionTask.IsCompleted)
            {
                yield return null;
            }

            if (conversionTask.IsFaulted)
            {
                Exception exception = conversionTask.Exception == null
                    ? null
                    : conversionTask.Exception.GetBaseException();
                failed("Audio conversion failed: " + (exception == null ? "Unknown error." : exception.Message));
                yield break;
            }

            ConversionResult conversionResult = conversionTask.Result;
            if (!conversionResult.Success)
            {
                failed(conversionResult.ErrorMessage);
                yield break;
            }

            statusChanged("Loading converted audio...");
            string audioUri = new Uri(outputPath).AbsoluteUri;
            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(audioUri, AudioType.WAV))
            {
                DownloadHandlerAudioClip downloadHandler = request.downloadHandler as DownloadHandlerAudioClip;
                if (downloadHandler != null)
                {
                    downloadHandler.streamAudio = false;
                }

                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    failed("Converted WAV could not be loaded: " + request.error);
                    yield break;
                }

                AudioClip audioClip = DownloadHandlerAudioClip.GetContent(request);
                if (audioClip == null)
                {
                    failed("Converted WAV did not produce a Unity AudioClip.");
                    yield break;
                }

                if (audioClip.length <= 0f || audioClip.length > MaximumAudioDurationSeconds)
                {
                    UnityEngine.Object.Destroy(audioClip);
                    failed("Audio duration must be between 0 and 15 minutes.");
                    yield break;
                }

                audioClip.name = Path.GetFileNameWithoutExtension(sourcePath);
                statusChanged("Detecting rhythm...");
                yield return null;

                IReadOnlyList<BeatEventData> beatEvents;
                try
                {
                    beatEvents = RuntimeBeatMapAnalyzer.BuildBeatEvents(audioClip, pipelineSettings);
                }
                catch (Exception exception)
                {
                    UnityEngine.Object.Destroy(audioClip);
                    failed("Beat detection failed: " + exception.Message);
                    yield break;
                }

                statusChanged("Building combat sequence...");
                yield return null;

                completed(new RuntimeAudioImportResult(
                    audioClip,
                    beatEvents,
                    sourcePath,
                    outputPath,
                    Path.GetFileNameWithoutExtension(sourcePath)));
            }
        }

        private static string ValidateSourcePath(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return "No audio file was selected.";
            }

            if (!File.Exists(sourcePath))
            {
                return "Selected audio file no longer exists.";
            }

            string extension = Path.GetExtension(sourcePath);
            if (!SupportedExtensions.Contains(extension))
            {
                return "Unsupported audio format: " + (string.IsNullOrEmpty(extension) ? "(none)" : extension);
            }

            try
            {
                if (new FileInfo(sourcePath).Length > MaximumSourceFileBytes)
                {
                    return "Audio file is larger than the 256 MB runtime import limit.";
                }
            }
            catch (Exception exception)
            {
                return "Audio file could not be inspected: " + exception.Message;
            }

            return string.Empty;
        }

        private static string ResolveFfmpegExecutable()
        {
            string[] candidates =
            {
                Path.Combine(Application.streamingAssetsPath, "PulseForge", "ffmpeg.exe"),
                Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"),
                FindExecutableOnPath("ffmpeg.exe")
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private static string FindExecutableOnPath(string executableName)
        {
            string pathValue = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathValue))
            {
                return string.Empty;
            }

            string[] pathEntries = pathValue.Split(Path.PathSeparator);
            for (int i = 0; i < pathEntries.Length; i++)
            {
                string pathEntry = pathEntries[i].Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(pathEntry))
                {
                    continue;
                }

                try
                {
                    string candidate = Path.Combine(pathEntry, executableName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (Exception)
                {
                    // Ignore malformed PATH entries and continue searching.
                }
            }

            return string.Empty;
        }

        private static ConversionResult ConvertToWav(
            string ffmpegPath,
            string sourcePath,
            string outputDirectory,
            string outputPath)
        {
            Directory.CreateDirectory(outputDirectory);
            string temporaryOutputPath = outputPath + ".tmp.wav";
            TryDeleteFile(temporaryOutputPath);

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = BuildFfmpegArguments(sourcePath, temporaryOutputPath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WorkingDirectory = outputDirectory
            };

            try
            {
                using (Process process = new Process { StartInfo = startInfo })
                {
                    if (!process.Start())
                    {
                        return ConversionResult.Failed("FFmpeg could not be started.");
                    }

                    Task<string> errorOutputTask = process.StandardError.ReadToEndAsync();
                    Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
                    if (!process.WaitForExit(ConversionTimeoutMilliseconds))
                    {
                        TryKillProcess(process);
                        return ConversionResult.Failed("Audio conversion timed out after 10 minutes.");
                    }

                    Task.WaitAll(errorOutputTask, standardOutputTask);
                    string errorOutput = errorOutputTask.Result;
                    if (process.ExitCode != 0)
                    {
                        return ConversionResult.Failed(
                            "FFmpeg conversion failed: " + TrimErrorMessage(errorOutput));
                    }
                }

                FileInfo outputInfo = new FileInfo(temporaryOutputPath);
                if (!outputInfo.Exists || outputInfo.Length <= 44L)
                {
                    return ConversionResult.Failed("FFmpeg did not create a valid WAV file.");
                }

                TryDeleteFile(outputPath);
                File.Move(temporaryOutputPath, outputPath);
                return ConversionResult.Succeeded();
            }
            catch (Exception exception)
            {
                TryDeleteFile(temporaryOutputPath);
                return ConversionResult.Failed("Audio conversion failed: " + exception.Message);
            }
        }

        private static string BuildFfmpegArguments(string sourcePath, string outputPath)
        {
            StringBuilder arguments = new StringBuilder();
            AppendArgument(arguments, "-hide_banner");
            AppendArgument(arguments, "-loglevel");
            AppendArgument(arguments, "error");
            AppendArgument(arguments, "-nostdin");
            AppendArgument(arguments, "-y");
            AppendArgument(arguments, "-i");
            AppendArgument(arguments, sourcePath);
            AppendArgument(arguments, "-vn");
            AppendArgument(arguments, "-acodec");
            AppendArgument(arguments, "pcm_s16le");
            AppendArgument(arguments, "-ar");
            AppendArgument(arguments, "44100");
            AppendArgument(arguments, "-ac");
            AppendArgument(arguments, "2");
            AppendArgument(arguments, outputPath);
            return arguments.ToString();
        }

        private static void AppendArgument(StringBuilder builder, string value)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(QuoteArgument(value ?? string.Empty));
        }

        private static string QuoteArgument(string value)
        {
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

        private static string TrimErrorMessage(string errorMessage)
        {
            string normalized = string.IsNullOrWhiteSpace(errorMessage)
                ? "No error details were returned."
                : errorMessage.Trim();
            return normalized.Length <= MaximumErrorLength
                ? normalized
                : normalized.Substring(0, MaximumErrorLength) + "...";
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // A later conversion/file operation will report the actionable error.
            }
        }

        private static void TryKillProcess(Process process)
        {
            try
            {
                process.Kill();
            }
            catch (Exception)
            {
                // The process may have exited between the timeout and this call.
            }
        }

        private readonly struct ConversionResult
        {
            private ConversionResult(bool success, string errorMessage)
            {
                Success = success;
                ErrorMessage = errorMessage;
            }

            public bool Success { get; }

            public string ErrorMessage { get; }

            public static ConversionResult Succeeded()
            {
                return new ConversionResult(true, string.Empty);
            }

            public static ConversionResult Failed(string errorMessage)
            {
                return new ConversionResult(false, errorMessage);
            }
        }
    }

    internal sealed class RuntimeAudioImportResult
    {
        public RuntimeAudioImportResult(
            AudioClip audioClip,
            IReadOnlyList<BeatEventData> beatEvents,
            string sourcePath,
            string convertedWavPath,
            string displayName)
        {
            AudioClip = audioClip;
            BeatEvents = beatEvents;
            SourcePath = sourcePath;
            ConvertedWavPath = convertedWavPath;
            DisplayName = displayName;
        }

        public AudioClip AudioClip { get; }

        public IReadOnlyList<BeatEventData> BeatEvents { get; }

        public string SourcePath { get; }

        public string ConvertedWavPath { get; }

        public string DisplayName { get; }
    }
}
