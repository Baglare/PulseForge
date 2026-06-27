#!/usr/bin/env python3
"""Generate deterministic PCM WAV click tracks for PulseForge analyzer demos."""

from __future__ import annotations

import argparse
import math
import struct
import sys
import wave
from pathlib import Path
from typing import Sequence


DEFAULT_SAMPLE_RATE = 44100
DEFAULT_CLICK_DURATION_MS = 25.0
DEFAULT_CLICK_FREQUENCY_HZ = 1000.0
DEFAULT_AMPLITUDE = 0.8
DEFAULT_CHANNELS = 1
DEFAULT_TRAILING_SECONDS = 1.0
PCM_16_MAX = 32767


class GeneratorError(Exception):
    """Raised for user-facing generator errors."""


def parse_click_times(times_text: str) -> list[float]:
    if times_text is None or times_text.strip() == "":
        raise GeneratorError("--times must contain at least one click time.")

    click_times: list[float] = []
    for raw_part in times_text.split(","):
        part = raw_part.strip()
        if part == "":
            raise GeneratorError("--times must be a comma-separated list of numbers.")

        try:
            click_time = float(part)
        except ValueError as exception:
            raise GeneratorError("Invalid click time '" + part + "'.") from exception

        if click_time < 0:
            raise GeneratorError("Click times must not be negative: " + part)

        click_times.append(click_time)

    if not click_times:
        raise GeneratorError("--times must contain at least one click time.")

    return click_times


def calculate_duration_seconds(click_times: Sequence[float], duration_seconds: float | None) -> float:
    if duration_seconds is not None:
        if duration_seconds <= 0:
            raise GeneratorError("--duration-seconds must be greater than zero.")

        return duration_seconds

    return max(click_times) + DEFAULT_TRAILING_SECONDS


def generate_click_track_samples(
    click_times: Sequence[float],
    *,
    sample_rate: int = DEFAULT_SAMPLE_RATE,
    duration_seconds: float | None = None,
    click_duration_ms: float = DEFAULT_CLICK_DURATION_MS,
    click_frequency_hz: float = DEFAULT_CLICK_FREQUENCY_HZ,
    amplitude: float = DEFAULT_AMPLITUDE,
) -> list[int]:
    validate_generation_settings(
        click_times,
        sample_rate,
        click_duration_ms,
        click_frequency_hz,
        amplitude,
    )
    resolved_duration_seconds = calculate_duration_seconds(click_times, duration_seconds)
    sample_count = int(math.ceil(resolved_duration_seconds * sample_rate))
    samples = [0.0] * sample_count
    click_sample_count = max(1, int(round(sample_rate * click_duration_ms / 1000.0)))

    for click_time in click_times:
        click_start = int(round(click_time * sample_rate))
        add_sine_burst(
            samples,
            click_start,
            click_sample_count,
            sample_rate,
            click_frequency_hz,
            amplitude,
        )

    return [float_to_pcm16(sample) for sample in samples]


def validate_generation_settings(
    click_times: Sequence[float],
    sample_rate: int,
    click_duration_ms: float,
    click_frequency_hz: float,
    amplitude: float,
) -> None:
    if not click_times:
        raise GeneratorError("At least one click time is required.")

    if any(click_time < 0 for click_time in click_times):
        raise GeneratorError("Click times must not be negative.")

    if sample_rate <= 0:
        raise GeneratorError("--sample-rate must be greater than zero.")

    if click_duration_ms <= 0:
        raise GeneratorError("--click-duration-ms must be greater than zero.")

    if click_frequency_hz <= 0:
        raise GeneratorError("--click-frequency-hz must be greater than zero.")

    if amplitude < 0 or amplitude > 1:
        raise GeneratorError("--amplitude must be between 0 and 1.")


def add_sine_burst(
    samples: list[float],
    click_start: int,
    click_sample_count: int,
    sample_rate: int,
    click_frequency_hz: float,
    amplitude: float,
) -> None:
    for offset in range(click_sample_count):
        sample_index = click_start + offset
        if sample_index < 0 or sample_index >= len(samples):
            continue

        phase = 2.0 * math.pi * click_frequency_hz * (offset / sample_rate)
        envelope = 1.0 - (offset / click_sample_count)
        samples[sample_index] = clamp(samples[sample_index] + math.sin(phase) * envelope * amplitude)


def write_debug_click_track(
    output_path: str | Path,
    click_times: Sequence[float],
    *,
    sample_rate: int = DEFAULT_SAMPLE_RATE,
    duration_seconds: float | None = None,
    click_duration_ms: float = DEFAULT_CLICK_DURATION_MS,
    click_frequency_hz: float = DEFAULT_CLICK_FREQUENCY_HZ,
    amplitude: float = DEFAULT_AMPLITUDE,
    channels: int = DEFAULT_CHANNELS,
) -> Path:
    if channels <= 0:
        raise GeneratorError("--channels must be greater than zero.")

    samples = generate_click_track_samples(
        click_times,
        sample_rate=sample_rate,
        duration_seconds=duration_seconds,
        click_duration_ms=click_duration_ms,
        click_frequency_hz=click_frequency_hz,
        amplitude=amplitude,
    )

    path = Path(output_path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as wav_file:
        wav_file.setnchannels(channels)
        wav_file.setsampwidth(2)
        wav_file.setframerate(sample_rate)
        wav_file.writeframes(build_interleaved_pcm16_frames(samples, channels))

    return path


def build_interleaved_pcm16_frames(samples: Sequence[int], channels: int) -> bytes:
    frames = bytearray()
    for sample in samples:
        for _ in range(channels):
            frames.extend(struct.pack("<h", sample))

    return bytes(frames)


def float_to_pcm16(value: float) -> int:
    return int(round(clamp(value) * PCM_16_MAX))


def clamp(value: float) -> float:
    if value < -1.0:
        return -1.0

    if value > 1.0:
        return 1.0

    return value


def build_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Generate a deterministic PCM WAV debug click track.")
    parser.add_argument("--output", "-o", required=True, help="Output WAV path.")
    parser.add_argument("--times", required=True, help="Comma-separated click times in seconds.")
    parser.add_argument("--sample-rate", type=int, default=DEFAULT_SAMPLE_RATE)
    parser.add_argument("--duration-seconds", type=float)
    parser.add_argument("--click-duration-ms", type=float, default=DEFAULT_CLICK_DURATION_MS)
    parser.add_argument("--click-frequency-hz", type=float, default=DEFAULT_CLICK_FREQUENCY_HZ)
    parser.add_argument("--amplitude", type=float, default=DEFAULT_AMPLITUDE)
    parser.add_argument("--channels", type=int, default=DEFAULT_CHANNELS)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_argument_parser()
    args = parser.parse_args(argv)

    try:
        click_times = parse_click_times(args.times)
        output_path = write_debug_click_track(
            args.output,
            click_times,
            sample_rate=args.sample_rate,
            duration_seconds=args.duration_seconds,
            click_duration_ms=args.click_duration_ms,
            click_frequency_hz=args.click_frequency_hz,
            amplitude=args.amplitude,
            channels=args.channels,
        )
        print("Wrote debug click track: " + str(output_path))
        return 0
    except GeneratorError as exception:
        print("Error: " + str(exception), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
