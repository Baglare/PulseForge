#!/usr/bin/env python3
"""Debug WAV onset analyzer for PulseForge beatmap JSON files."""

from __future__ import annotations

import argparse
import csv
import io
import json
import math
import sys
import wave
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Sequence


SUPPORTED_ACTIONS = ("Guard", "Strike")
DEFAULT_ACTION_PATTERN = ("Guard", "Strike")
DETECTION_MODES = ("amplitude", "onset")
SCHEMA_VERSION = 1


class AnalyzerError(Exception):
    """Raised for user-facing analyzer errors."""


@dataclass(frozen=True)
class FrameAmplitude:
    time_seconds: float
    amplitude: float


@dataclass(frozen=True)
class DetectionFrame:
    time_seconds: float
    amplitude: float
    onset_strength: float
    detection_value: float


@dataclass(frozen=True)
class WavAnalysis:
    input_path: Path
    sample_rate: int
    channel_count: int
    sample_width_bytes: int
    frame_count: int
    frame_ms: float
    frame_amplitudes: list[FrameAmplitude]

    @property
    def duration_seconds(self) -> float:
        return self.frame_count / self.sample_rate


@dataclass(frozen=True)
class DetectedPeak:
    time_seconds: float
    intensity: float


@dataclass(frozen=True)
class AnalyzerRunResult:
    input_path: Path
    display_name: str
    global_offset_seconds: float
    frame_ms: float
    threshold_ratio: float
    min_gap_seconds: float
    max_events: int | None
    detection_mode: str
    baseline_ms: float
    onset_smooth_frames: int
    wav_analysis: WavAnalysis
    detection_frames: list[DetectionFrame]
    peaks: list[DetectedPeak]
    beatmap_document: dict


def normalize_action(action: str) -> str:
    if action is None:
        raise AnalyzerError("Action must not be empty.")

    normalized = action.strip()
    for supported_action in SUPPORTED_ACTIONS:
        if normalized.lower() == supported_action.lower():
            return supported_action

    raise AnalyzerError(
        "Unsupported action '"
        + normalized
        + "'. Supported actions are Guard and Strike."
    )


def parse_action_pattern(pattern_text: str | None) -> list[str]:
    if pattern_text is None:
        return list(DEFAULT_ACTION_PATTERN)

    parts = [part.strip() for part in pattern_text.split(",")]
    if not parts or any(part == "" for part in parts):
        raise AnalyzerError("Pattern must be a comma-separated list of Guard and Strike actions.")

    return [normalize_action(part) for part in parts]


def normalize_detection_mode(detection_mode: str) -> str:
    if detection_mode is None:
        raise AnalyzerError("Detection mode must not be empty.")

    normalized = detection_mode.strip().lower()
    for supported_mode in DETECTION_MODES:
        if normalized == supported_mode:
            return supported_mode

    raise AnalyzerError("--detection-mode must be amplitude or onset.")


def validate_detection_settings(
    detection_mode: str,
    baseline_ms: float,
    onset_smooth_frames: int,
) -> str:
    normalized_mode = normalize_detection_mode(detection_mode)
    if not is_finite_number(baseline_ms) or baseline_ms <= 0:
        raise AnalyzerError("--baseline-ms must be a finite number greater than zero.")

    if onset_smooth_frames < 0:
        raise AnalyzerError("--onset-smooth-frames must be greater than or equal to zero.")

    return normalized_mode


def format_event_id(index: int) -> str:
    if index < 0:
        raise AnalyzerError("Event index must be non-negative.")

    return "event-" + str(index + 1).zfill(3)


def read_wav_frame_amplitudes(input_wav: str | Path, frame_ms: float) -> list[FrameAmplitude]:
    return read_wav_analysis(input_wav, frame_ms).frame_amplitudes


def read_wav_analysis(input_wav: str | Path, frame_ms: float) -> WavAnalysis:
    if frame_ms <= 0:
        raise AnalyzerError("--frame-ms must be greater than zero.")

    input_path = Path(input_wav)
    if not input_path.exists():
        raise AnalyzerError("Input WAV file does not exist: " + str(input_path))

    try:
        with wave.open(str(input_path), "rb") as wav_file:
            channel_count = wav_file.getnchannels()
            sample_width = wav_file.getsampwidth()
            sample_rate = wav_file.getframerate()
            frame_count = wav_file.getnframes()
            compression_type = wav_file.getcomptype()
            raw_frames = wav_file.readframes(frame_count)
    except wave.Error as exception:
        raise AnalyzerError("Could not read WAV file: " + str(exception)) from exception
    except OSError as exception:
        raise AnalyzerError("Could not open WAV file: " + str(exception)) from exception

    if compression_type != "NONE":
        raise AnalyzerError("Only uncompressed PCM WAV files are supported.")

    if channel_count <= 0:
        raise AnalyzerError("WAV file must contain at least one channel.")

    if sample_rate <= 0:
        raise AnalyzerError("WAV file has an invalid sample rate.")

    if sample_width not in (1, 2, 4):
        raise AnalyzerError("Only 8-bit, 16-bit, and 32-bit PCM WAV files are supported.")

    samples_per_analysis_frame = max(1, int(round(sample_rate * frame_ms / 1000.0)))
    sample_amplitudes = iter_normalized_sample_amplitudes(raw_frames, sample_width, channel_count)
    frame_amplitudes = build_frame_amplitudes(
        sample_amplitudes,
        sample_rate,
        samples_per_analysis_frame,
    )
    return WavAnalysis(
        input_path=input_path,
        sample_rate=sample_rate,
        channel_count=channel_count,
        sample_width_bytes=sample_width,
        frame_count=frame_count,
        frame_ms=frame_ms,
        frame_amplitudes=frame_amplitudes,
    )


def iter_normalized_sample_amplitudes(
    raw_frames: bytes,
    sample_width: int,
    channel_count: int,
) -> Iterable[float]:
    bytes_per_audio_frame = sample_width * channel_count
    usable_length = len(raw_frames) - (len(raw_frames) % bytes_per_audio_frame)

    for frame_start in range(0, usable_length, bytes_per_audio_frame):
        amplitude_total = 0.0
        for channel_index in range(channel_count):
            sample_start = frame_start + channel_index * sample_width
            sample_bytes = raw_frames[sample_start : sample_start + sample_width]
            amplitude_total += abs(normalize_sample(sample_bytes, sample_width))

        yield clamp01(amplitude_total / channel_count)


def normalize_sample(sample_bytes: bytes, sample_width: int) -> float:
    if sample_width == 1:
        return (sample_bytes[0] - 128) / 128.0

    if sample_width == 2:
        value = int.from_bytes(sample_bytes, byteorder="little", signed=True)
        return value / 32768.0

    if sample_width == 4:
        value = int.from_bytes(sample_bytes, byteorder="little", signed=True)
        return value / 2147483648.0

    raise AnalyzerError("Unsupported sample width: " + str(sample_width))


def build_frame_amplitudes(
    sample_amplitudes: Iterable[float],
    sample_rate: int,
    samples_per_analysis_frame: int,
) -> list[FrameAmplitude]:
    frames: list[FrameAmplitude] = []
    frame_peak = 0.0
    samples_in_frame = 0
    frame_index = 0

    for amplitude in sample_amplitudes:
        if amplitude > frame_peak:
            frame_peak = amplitude

        samples_in_frame += 1
        if samples_in_frame >= samples_per_analysis_frame:
            frames.append(
                FrameAmplitude(
                    time_seconds=(frame_index * samples_per_analysis_frame) / sample_rate,
                    amplitude=frame_peak,
                )
            )
            frame_index += 1
            frame_peak = 0.0
            samples_in_frame = 0

    if samples_in_frame > 0:
        frames.append(
            FrameAmplitude(
                time_seconds=(frame_index * samples_per_analysis_frame) / sample_rate,
                amplitude=frame_peak,
            )
        )

    return frames


def detect_peaks(
    frame_amplitudes: Sequence[FrameAmplitude],
    threshold_ratio: float,
    min_gap_seconds: float,
    max_events: int | None = None,
) -> list[DetectedPeak]:
    detection_frames = [
        DetectionFrame(
            time_seconds=frame.time_seconds,
            amplitude=frame.amplitude,
            onset_strength=0.0,
            detection_value=frame.amplitude,
        )
        for frame in frame_amplitudes
    ]
    return detect_peaks_in_curve(
        detection_frames,
        threshold_ratio,
        min_gap_seconds,
        max_events,
    )


def detect_peaks_in_curve(
    detection_frames: Sequence[DetectionFrame],
    threshold_ratio: float,
    min_gap_seconds: float,
    max_events: int | None = None,
) -> list[DetectedPeak]:
    if threshold_ratio < 0:
        raise AnalyzerError("--threshold-ratio must be greater than or equal to zero.")

    if min_gap_seconds < 0:
        raise AnalyzerError("--min-gap-seconds must be greater than or equal to zero.")

    if max_events is not None and max_events <= 0:
        raise AnalyzerError("--max-events must be greater than zero when provided.")

    if not detection_frames:
        return []

    max_detection_value = max(frame.detection_value for frame in detection_frames)
    if max_detection_value <= 0:
        return []

    threshold = max_detection_value * threshold_ratio
    peaks: list[DetectedPeak] = []

    for index, frame in enumerate(detection_frames):
        if frame.detection_value <= 0 or frame.detection_value < threshold:
            continue

        previous_value = detection_frames[index - 1].detection_value if index > 0 else -1.0
        next_value = (
            detection_frames[index + 1].detection_value
            if index < len(detection_frames) - 1
            else -1.0
        )
        if not is_local_peak(frame.detection_value, previous_value, next_value):
            continue

        peak = DetectedPeak(
            time_seconds=frame.time_seconds,
            intensity=clamp01(frame.detection_value / max_detection_value),
        )
        add_peak_with_gap(peaks, peak, min_gap_seconds, max_events)

    return peaks


def is_local_peak(amplitude: float, previous_amplitude: float, next_amplitude: float) -> bool:
    return (
        amplitude >= previous_amplitude
        and amplitude >= next_amplitude
        and (amplitude > previous_amplitude or amplitude > next_amplitude)
    )


def add_peak_with_gap(
    peaks: list[DetectedPeak],
    peak: DetectedPeak,
    min_gap_seconds: float,
    max_events: int | None,
) -> None:
    if peaks and peak.time_seconds - peaks[-1].time_seconds < min_gap_seconds:
        if peak.intensity > peaks[-1].intensity:
            peaks[-1] = peak
        return

    if max_events is not None and len(peaks) >= max_events:
        return

    peaks.append(peak)


def build_detection_frames(
    frame_amplitudes: Sequence[FrameAmplitude],
    *,
    detection_mode: str,
    frame_ms: float,
    baseline_ms: float,
    onset_smooth_frames: int,
) -> list[DetectionFrame]:
    normalized_mode = validate_detection_settings(detection_mode, baseline_ms, onset_smooth_frames)
    onset_strengths = build_onset_strengths(frame_amplitudes, frame_ms, baseline_ms)
    if onset_smooth_frames > 0:
        onset_strengths = smooth_values(onset_strengths, onset_smooth_frames)

    detection_frames: list[DetectionFrame] = []
    for frame, onset_strength in zip(frame_amplitudes, onset_strengths):
        detection_value = frame.amplitude if normalized_mode == "amplitude" else onset_strength
        detection_frames.append(
            DetectionFrame(
                time_seconds=frame.time_seconds,
                amplitude=frame.amplitude,
                onset_strength=onset_strength,
                detection_value=detection_value,
            )
        )

    return detection_frames


def build_onset_strengths(
    frame_amplitudes: Sequence[FrameAmplitude],
    frame_ms: float,
    baseline_ms: float,
) -> list[float]:
    if frame_ms <= 0:
        raise AnalyzerError("--frame-ms must be greater than zero.")

    if not is_finite_number(baseline_ms) or baseline_ms <= 0:
        raise AnalyzerError("--baseline-ms must be a finite number greater than zero.")

    baseline_frame_count = max(1, int(round(baseline_ms / frame_ms)))
    amplitudes = [frame.amplitude for frame in frame_amplitudes]
    prefix_sums = [0.0]
    for amplitude in amplitudes:
        prefix_sums.append(prefix_sums[-1] + amplitude)

    onset_strengths: list[float] = []
    for index, amplitude in enumerate(amplitudes):
        baseline_start_index = max(0, index - baseline_frame_count)
        baseline_sample_count = index - baseline_start_index
        if baseline_sample_count == 0:
            baseline = 0.0
        else:
            baseline = (prefix_sums[index] - prefix_sums[baseline_start_index]) / baseline_sample_count

        onset_strengths.append(max(0.0, amplitude - baseline))

    return onset_strengths


def smooth_values(values: Sequence[float], radius: int) -> list[float]:
    if radius < 0:
        raise AnalyzerError("--onset-smooth-frames must be greater than or equal to zero.")

    if radius == 0 or not values:
        return list(values)

    smoothed_values: list[float] = []
    for index in range(len(values)):
        start_index = max(0, index - radius)
        end_index = min(len(values), index + radius + 1)
        window = values[start_index:end_index]
        smoothed_values.append(sum(window) / len(window))

    return smoothed_values


def build_beatmap_document(
    peaks: Sequence[DetectedPeak],
    display_name: str,
    global_offset_seconds: float,
    action_pattern: Sequence[str],
) -> dict:
    if not action_pattern:
        raise AnalyzerError("Action pattern must contain at least one action.")

    events = []
    for index, peak in enumerate(peaks):
        action = action_pattern[index % len(action_pattern)]
        events.append(
            {
                "eventId": format_event_id(index),
                "targetTimeSeconds": round(peak.time_seconds, 6),
                "action": action,
                "intensity": round(clamp01(peak.intensity), 6),
            }
        )

    return {
        "schemaVersion": SCHEMA_VERSION,
        "displayName": display_name,
        "globalOffsetSeconds": global_offset_seconds,
        "events": events,
    }


def analyze_wav_file(
    input_wav: str | Path,
    *,
    display_name: str | None = None,
    global_offset_seconds: float = 0.0,
    frame_ms: float = 10.0,
    threshold_ratio: float = 0.35,
    min_gap_seconds: float = 0.18,
    max_events: int | None = None,
    pattern_text: str | None = None,
    detection_mode: str = "amplitude",
    baseline_ms: float = 120.0,
    onset_smooth_frames: int = 1,
) -> dict:
    return analyze_wav(
        input_wav,
        display_name=display_name,
        global_offset_seconds=global_offset_seconds,
        frame_ms=frame_ms,
        threshold_ratio=threshold_ratio,
        min_gap_seconds=min_gap_seconds,
        max_events=max_events,
        pattern_text=pattern_text,
        detection_mode=detection_mode,
        baseline_ms=baseline_ms,
        onset_smooth_frames=onset_smooth_frames,
    ).beatmap_document


def analyze_wav(
    input_wav: str | Path,
    *,
    display_name: str | None = None,
    global_offset_seconds: float = 0.0,
    frame_ms: float = 10.0,
    threshold_ratio: float = 0.35,
    min_gap_seconds: float = 0.18,
    max_events: int | None = None,
    pattern_text: str | None = None,
    detection_mode: str = "amplitude",
    baseline_ms: float = 120.0,
    onset_smooth_frames: int = 1,
) -> AnalyzerRunResult:
    input_path = Path(input_wav)
    action_pattern = parse_action_pattern(pattern_text)
    normalized_detection_mode = validate_detection_settings(
        detection_mode,
        baseline_ms,
        onset_smooth_frames,
    )
    wav_analysis = read_wav_analysis(input_path, frame_ms)
    detection_frames = build_detection_frames(
        wav_analysis.frame_amplitudes,
        detection_mode=normalized_detection_mode,
        frame_ms=frame_ms,
        baseline_ms=baseline_ms,
        onset_smooth_frames=onset_smooth_frames,
    )
    peaks = detect_peaks_in_curve(
        detection_frames,
        threshold_ratio,
        min_gap_seconds,
        max_events,
    )
    resolved_display_name = display_name or input_path.stem
    beatmap_document = build_beatmap_document(
        peaks,
        resolved_display_name,
        global_offset_seconds,
        action_pattern,
    )

    return AnalyzerRunResult(
        input_path=input_path,
        display_name=resolved_display_name,
        global_offset_seconds=global_offset_seconds,
        frame_ms=frame_ms,
        threshold_ratio=threshold_ratio,
        min_gap_seconds=min_gap_seconds,
        max_events=max_events,
        detection_mode=normalized_detection_mode,
        baseline_ms=baseline_ms,
        onset_smooth_frames=onset_smooth_frames,
        wav_analysis=wav_analysis,
        detection_frames=detection_frames,
        peaks=peaks,
        beatmap_document=beatmap_document,
    )


def dumps_beatmap(beatmap_document: dict) -> str:
    return json.dumps(beatmap_document, indent=2) + "\n"


def build_analysis_report(run_result: AnalyzerRunResult) -> dict:
    events = run_result.beatmap_document["events"]
    return {
        "inputPath": str(run_result.input_path),
        "displayName": run_result.display_name,
        "sampleRate": run_result.wav_analysis.sample_rate,
        "channels": run_result.wav_analysis.channel_count,
        "sampleWidthBytes": run_result.wav_analysis.sample_width_bytes,
        "durationSeconds": round(run_result.wav_analysis.duration_seconds, 6),
        "frameMs": run_result.frame_ms,
        "thresholdRatio": run_result.threshold_ratio,
        "minGapSeconds": run_result.min_gap_seconds,
        "maxEvents": run_result.max_events,
        "globalOffsetSeconds": run_result.global_offset_seconds,
        "detectionMode": run_result.detection_mode,
        "baselineMs": run_result.baseline_ms,
        "onsetSmoothFrames": run_result.onset_smooth_frames,
        "detectedEventCount": len(events),
        "maxFrameAmplitude": round(get_max_frame_amplitude(run_result.wav_analysis.frame_amplitudes), 6),
        "detectionCurveMax": round(get_max_detection_value(run_result.detection_frames), 6),
        "events": events,
    }


def dumps_analysis_report(run_result: AnalyzerRunResult) -> str:
    return json.dumps(build_analysis_report(run_result), indent=2) + "\n"


def dumps_debug_csv(run_result: AnalyzerRunResult) -> str:
    output = io.StringIO()
    writer = csv.writer(output, lineterminator="\n")
    writer.writerow(
        [
            "frameIndex",
            "timeSeconds",
            "amplitude",
            "onsetStrength",
            "detectionValue",
            "isLocalPeak",
            "isSelectedPeak",
        ]
    )

    selected_peak_times = {peak.time_seconds for peak in run_result.peaks}
    detection_frames = run_result.detection_frames
    for index, frame in enumerate(detection_frames):
        writer.writerow(
            [
                index,
                format_float(frame.time_seconds),
                format_float(frame.amplitude),
                format_float(frame.onset_strength),
                format_float(frame.detection_value),
                format_bool(is_detection_frame_local_peak(detection_frames, index)),
                format_bool(frame.time_seconds in selected_peak_times),
            ]
        )

    return output.getvalue()


def get_max_frame_amplitude(frame_amplitudes: Sequence[FrameAmplitude]) -> float:
    if not frame_amplitudes:
        return 0.0

    return max(frame.amplitude for frame in frame_amplitudes)


def get_max_detection_value(detection_frames: Sequence[DetectionFrame]) -> float:
    if not detection_frames:
        return 0.0

    return max(frame.detection_value for frame in detection_frames)


def is_frame_local_peak(frame_amplitudes: Sequence[FrameAmplitude], index: int) -> bool:
    if index < 0 or index >= len(frame_amplitudes):
        raise AnalyzerError("Frame index is out of range.")

    frame = frame_amplitudes[index]
    if frame.amplitude <= 0:
        return False

    previous_amplitude = frame_amplitudes[index - 1].amplitude if index > 0 else -1.0
    next_amplitude = (
        frame_amplitudes[index + 1].amplitude
        if index < len(frame_amplitudes) - 1
        else -1.0
    )
    return is_local_peak(frame.amplitude, previous_amplitude, next_amplitude)


def is_detection_frame_local_peak(detection_frames: Sequence[DetectionFrame], index: int) -> bool:
    if index < 0 or index >= len(detection_frames):
        raise AnalyzerError("Frame index is out of range.")

    frame = detection_frames[index]
    if frame.detection_value <= 0:
        return False

    previous_value = detection_frames[index - 1].detection_value if index > 0 else -1.0
    next_value = (
        detection_frames[index + 1].detection_value
        if index < len(detection_frames) - 1
        else -1.0
    )
    return is_local_peak(frame.detection_value, previous_value, next_value)


def format_bool(value: bool) -> str:
    return "true" if value else "false"


def format_float(value: float) -> str:
    return f"{value:.6f}"


def is_finite_number(value: object) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def write_output(output_path: str | Path, text: str) -> None:
    path = Path(output_path)
    try:
        if path.parent != Path(""):
            path.parent.mkdir(parents=True, exist_ok=True)

        path.write_text(text, encoding="utf-8")
    except OSError as exception:
        raise AnalyzerError("Could not write output file: " + str(exception)) from exception


def write_summary(run_result: AnalyzerRunResult) -> None:
    event_count = len(run_result.beatmap_document["events"])
    summary = (
        "Detected "
        + str(event_count)
        + " events from "
        + str(run_result.input_path)
        + " (duration "
        + format_float(run_result.wav_analysis.duration_seconds)
        + "s, max frame amplitude "
        + format_float(get_max_frame_amplitude(run_result.wav_analysis.frame_amplitudes))
        + ", detection mode "
        + run_result.detection_mode
        + ", detection curve max "
        + format_float(get_max_detection_value(run_result.detection_frames))
        + ").\n"
    )
    sys.stderr.write(summary)


def build_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Analyze a debug PCM WAV click track and emit PulseForge beatmap JSON."
    )
    parser.add_argument("input_wav", help="Input PCM WAV file.")
    parser.add_argument("--output", "-o", help="Output JSON path. If omitted, JSON is printed to stdout.")
    parser.add_argument("--display-name", help="Beatmap display name. Defaults to the input file name.")
    parser.add_argument("--global-offset-seconds", type=float, default=0.0)
    parser.add_argument("--frame-ms", type=float, default=10.0)
    parser.add_argument("--threshold-ratio", type=float, default=0.35)
    parser.add_argument("--min-gap-seconds", type=float, default=0.18)
    parser.add_argument("--max-events", type=int)
    parser.add_argument("--pattern", help="Comma-separated action pattern, for example Guard,Strike.")
    parser.add_argument("--detection-mode", choices=DETECTION_MODES, default="amplitude")
    parser.add_argument("--baseline-ms", type=float, default=120.0)
    parser.add_argument("--onset-smooth-frames", type=int, default=1)
    parser.add_argument("--report-output", help="Optional analysis report JSON path.")
    parser.add_argument("--debug-csv-output", help="Optional frame diagnostics CSV path.")
    parser.add_argument("--summary", action="store_true", help="Write a short analysis summary to stderr.")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_argument_parser()
    args = parser.parse_args(argv)

    try:
        run_result = analyze_wav(
            args.input_wav,
            display_name=args.display_name,
            global_offset_seconds=args.global_offset_seconds,
            frame_ms=args.frame_ms,
            threshold_ratio=args.threshold_ratio,
            min_gap_seconds=args.min_gap_seconds,
            max_events=args.max_events,
            pattern_text=args.pattern,
            detection_mode=args.detection_mode,
            baseline_ms=args.baseline_ms,
            onset_smooth_frames=args.onset_smooth_frames,
        )
        output_text = dumps_beatmap(run_result.beatmap_document)
        if args.output:
            write_output(args.output, output_text)
        else:
            sys.stdout.write(output_text)

        if args.report_output:
            write_output(args.report_output, dumps_analysis_report(run_result))

        if args.debug_csv_output:
            write_output(args.debug_csv_output, dumps_debug_csv(run_result))

        if args.summary:
            write_summary(run_result)

        return 0
    except AnalyzerError as exception:
        print("Error: " + str(exception), file=sys.stderr)
        return 1


def clamp01(value: float) -> float:
    if value < 0.0:
        return 0.0

    if value > 1.0:
        return 1.0

    return value


if __name__ == "__main__":
    raise SystemExit(main())
