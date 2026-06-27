#!/usr/bin/env python3
"""Debug WAV onset analyzer for PulseForge beatmap JSON files."""

from __future__ import annotations

import argparse
import json
import sys
import wave
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Sequence


SUPPORTED_ACTIONS = ("Guard", "Strike")
DEFAULT_ACTION_PATTERN = ("Guard", "Strike")
SCHEMA_VERSION = 1


class AnalyzerError(Exception):
    """Raised for user-facing analyzer errors."""


@dataclass(frozen=True)
class FrameAmplitude:
    time_seconds: float
    amplitude: float


@dataclass(frozen=True)
class DetectedPeak:
    time_seconds: float
    intensity: float


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


def format_event_id(index: int) -> str:
    if index < 0:
        raise AnalyzerError("Event index must be non-negative.")

    return "event-" + str(index + 1).zfill(3)


def read_wav_frame_amplitudes(input_wav: str | Path, frame_ms: float) -> list[FrameAmplitude]:
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
    return build_frame_amplitudes(sample_amplitudes, sample_rate, samples_per_analysis_frame)


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
    if threshold_ratio < 0:
        raise AnalyzerError("--threshold-ratio must be greater than or equal to zero.")

    if min_gap_seconds < 0:
        raise AnalyzerError("--min-gap-seconds must be greater than or equal to zero.")

    if max_events is not None and max_events <= 0:
        raise AnalyzerError("--max-events must be greater than zero when provided.")

    if not frame_amplitudes:
        return []

    max_amplitude = max(frame.amplitude for frame in frame_amplitudes)
    if max_amplitude <= 0:
        return []

    threshold = max_amplitude * threshold_ratio
    peaks: list[DetectedPeak] = []

    for index, frame in enumerate(frame_amplitudes):
        if frame.amplitude <= 0 or frame.amplitude < threshold:
            continue

        previous_amplitude = frame_amplitudes[index - 1].amplitude if index > 0 else -1.0
        next_amplitude = (
            frame_amplitudes[index + 1].amplitude
            if index < len(frame_amplitudes) - 1
            else -1.0
        )
        if not is_local_peak(frame.amplitude, previous_amplitude, next_amplitude):
            continue

        peak = DetectedPeak(
            time_seconds=frame.time_seconds,
            intensity=clamp01(frame.amplitude / max_amplitude),
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
) -> dict:
    input_path = Path(input_wav)
    action_pattern = parse_action_pattern(pattern_text)
    frame_amplitudes = read_wav_frame_amplitudes(input_path, frame_ms)
    peaks = detect_peaks(frame_amplitudes, threshold_ratio, min_gap_seconds, max_events)
    return build_beatmap_document(
        peaks,
        display_name or input_path.stem,
        global_offset_seconds,
        action_pattern,
    )


def dumps_beatmap(beatmap_document: dict) -> str:
    return json.dumps(beatmap_document, indent=2) + "\n"


def write_output(output_path: str | Path, text: str) -> None:
    path = Path(output_path)
    if path.parent != Path(""):
        path.parent.mkdir(parents=True, exist_ok=True)

    path.write_text(text, encoding="utf-8")


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
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_argument_parser()
    args = parser.parse_args(argv)

    try:
        beatmap_document = analyze_wav_file(
            args.input_wav,
            display_name=args.display_name,
            global_offset_seconds=args.global_offset_seconds,
            frame_ms=args.frame_ms,
            threshold_ratio=args.threshold_ratio,
            min_gap_seconds=args.min_gap_seconds,
            max_events=args.max_events,
            pattern_text=args.pattern,
        )
        output_text = dumps_beatmap(beatmap_document)
        if args.output:
            write_output(args.output, output_text)
        else:
            sys.stdout.write(output_text)

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
