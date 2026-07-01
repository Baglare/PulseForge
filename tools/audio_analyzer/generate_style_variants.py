#!/usr/bin/env python3
"""Generate multiple PulseForge combat-style playable beatmap variants."""

from __future__ import annotations

import argparse
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Sequence


SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import compare_beatmaps
import postprocess_beatmap
import pulseforge_audio_analyzer
import run_debug_pipeline


DEFAULT_STYLES = ("balanced", "defensive", "aggressive", "bursty")
STYLE_LABELS = {
    "balanced": "Balanced",
    "defensive": "Defensive",
    "aggressive": "Aggressive",
    "bursty": "Bursty",
}


class StyleVariantError(Exception):
    """Raised for user-facing style variant generation errors."""


@dataclass(frozen=True)
class StyleVariantResult:
    style: str
    playable_beatmap_path: Path
    postprocess_report_path: Path
    compare_report_path: Path | None
    output_event_count: int
    dropped_event_count: int
    action_counts: dict
    compare_summary: dict | None


@dataclass(frozen=True)
class StyleVariantRunResult:
    raw_beatmap_path: Path
    raw_event_count: int
    variants: list[StyleVariantResult]


def generate_style_variants(
    *,
    input_wav: str | Path | None = None,
    input_raw_json: str | Path | None = None,
    output_dir: str | Path,
    name: str | None = None,
    difficulty: str = "normal",
    detection_mode: str = "amplitude",
    threshold_ratio: float = run_debug_pipeline.ANALYZER_DEFAULT_THRESHOLD_RATIO,
    min_analyzer_gap_seconds: float | None = None,
    styles: str | Sequence[str] = DEFAULT_STYLES,
    burst_window_seconds: float = postprocess_beatmap.DEFAULT_BURST_WINDOW_SECONDS,
    expected_json: str | Path | None = None,
    compare_tolerance_ms: float = run_debug_pipeline.DEFAULT_COMPARE_TOLERANCE_MS,
    diagnostics_dir: str | Path | None = None,
) -> StyleVariantRunResult:
    validate_input_selection(input_wav, input_raw_json)
    resolved_output_dir = Path(output_dir)
    resolved_output_dir.mkdir(parents=True, exist_ok=True)
    resolved_diagnostics_dir = Path(diagnostics_dir) if diagnostics_dir is not None else SCRIPT_DIR / "out"
    resolved_diagnostics_dir.mkdir(parents=True, exist_ok=True)

    resolved_styles = resolve_styles(styles)
    postprocess_beatmap.resolve_burst_window_seconds(burst_window_seconds)
    resolved_name = resolve_name(name, input_wav=input_wav, input_raw_json=input_raw_json)
    raw_beatmap_path = resolve_raw_beatmap(
        input_wav=input_wav,
        input_raw_json=input_raw_json,
        output_dir=resolved_output_dir,
        name=resolved_name,
        detection_mode=detection_mode,
        threshold_ratio=threshold_ratio,
        min_analyzer_gap_seconds=min_analyzer_gap_seconds,
    )
    raw_document = postprocess_beatmap.load_beatmap(raw_beatmap_path)
    variants = [
        generate_single_style_variant(
            raw_document,
            output_dir=resolved_output_dir,
            diagnostics_dir=resolved_diagnostics_dir,
            name=resolved_name,
            style=style,
            difficulty=difficulty,
            burst_window_seconds=burst_window_seconds,
            expected_json=expected_json,
            compare_tolerance_ms=compare_tolerance_ms,
        )
        for style in resolved_styles
    ]

    return StyleVariantRunResult(
        raw_beatmap_path=raw_beatmap_path,
        raw_event_count=len(raw_document.events),
        variants=variants,
    )


def validate_input_selection(input_wav: str | Path | None, input_raw_json: str | Path | None) -> None:
    has_input_wav = input_wav is not None and str(input_wav).strip() != ""
    has_input_raw_json = input_raw_json is not None and str(input_raw_json).strip() != ""
    if has_input_wav == has_input_raw_json:
        raise StyleVariantError("Provide exactly one input: --input-wav or --input-raw-json.")


def resolve_name(
    name: str | None,
    *,
    input_wav: str | Path | None,
    input_raw_json: str | Path | None,
) -> str:
    if name is not None and name.strip() != "":
        return name.strip()

    source_path = Path(input_wav) if input_wav is not None else Path(input_raw_json)
    source_name = source_path.stem
    if source_name.startswith("BM_Raw_"):
        source_name = source_name[len("BM_Raw_"):]

    return source_name


def resolve_raw_beatmap(
    *,
    input_wav: str | Path | None,
    input_raw_json: str | Path | None,
    output_dir: Path,
    name: str,
    detection_mode: str,
    threshold_ratio: float,
    min_analyzer_gap_seconds: float | None,
) -> Path:
    if input_raw_json is not None and str(input_raw_json).strip() != "":
        raw_json_path = Path(input_raw_json)
        if not raw_json_path.exists():
            raise StyleVariantError("Input raw JSON file does not exist: " + str(raw_json_path))

        return raw_json_path

    input_wav_path = Path(input_wav)
    if not input_wav_path.exists():
        raise StyleVariantError("Input WAV file does not exist: " + str(input_wav_path))

    analyzer_result = pulseforge_audio_analyzer.analyze_wav(
        input_wav_path,
        display_name="Raw " + name,
        threshold_ratio=threshold_ratio,
        min_gap_seconds=run_debug_pipeline.resolve_analyzer_min_gap_seconds(min_analyzer_gap_seconds),
        detection_mode=detection_mode,
    )
    raw_beatmap_path = output_dir / ("BM_Raw_" + name + ".json")
    write_text(raw_beatmap_path, pulseforge_audio_analyzer.dumps_beatmap(analyzer_result.beatmap_document))
    return raw_beatmap_path


def generate_single_style_variant(
    raw_document: postprocess_beatmap.BeatmapDocument,
    *,
    output_dir: Path,
    diagnostics_dir: Path,
    name: str,
    style: str,
    difficulty: str,
    burst_window_seconds: float,
    expected_json: str | Path | None,
    compare_tolerance_ms: float,
) -> StyleVariantResult:
    style_label = STYLE_LABELS[style]
    postprocess_result = postprocess_beatmap.postprocess_beatmap(
        raw_document,
        display_name="Playable " + name + " " + style_label,
        difficulty=difficulty,
        combat_style=style,
        burst_window_seconds=burst_window_seconds,
    )
    playable_beatmap_path = output_dir / ("BM_Playable_" + name + "_" + style_label + ".json")
    postprocess_report_path = diagnostics_dir / (name + "_" + style + "_postprocess_report.json")
    write_text(playable_beatmap_path, postprocess_beatmap.dumps_beatmap(postprocess_result.output_document))
    write_text(postprocess_report_path, postprocess_beatmap.dumps_report(postprocess_result))

    compare_report_path = None
    compare_summary = None
    if expected_json is not None:
        compare_report_path = diagnostics_dir / (name + "_" + style + "_compare_report.json")
        compare_report = compare_beatmaps.compare_beatmap_files(
            expected_json,
            playable_beatmap_path,
            tolerance_ms=compare_tolerance_ms,
        )
        write_text(compare_report_path, compare_beatmaps.dumps_report(compare_report))
        compare_summary = compare_report["summary"]

    return StyleVariantResult(
        style=style,
        playable_beatmap_path=playable_beatmap_path,
        postprocess_report_path=postprocess_report_path,
        compare_report_path=compare_report_path,
        output_event_count=len(postprocess_result.output_events),
        dropped_event_count=len(postprocess_result.dropped_events),
        action_counts=postprocess_beatmap.count_actions(postprocess_result.output_events),
        compare_summary=compare_summary,
    )


def resolve_styles(styles: str | Sequence[str]) -> list[str]:
    if isinstance(styles, str):
        raw_styles = [style.strip().lower() for style in styles.split(",")]
    else:
        raw_styles = [str(style).strip().lower() for style in styles]

    if not raw_styles or any(style == "" for style in raw_styles):
        raise StyleVariantError("--styles must be a comma-separated list of combat styles.")

    resolved_styles: list[str] = []
    for style in raw_styles:
        if style not in DEFAULT_STYLES:
            raise StyleVariantError(
                "Unsupported style '"
                + style
                + "'. Supported styles are balanced, defensive, aggressive, and bursty."
            )

        if style not in resolved_styles:
            resolved_styles.append(style)

    return resolved_styles


def write_text(path: Path, text: str) -> None:
    try:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
    except OSError as exception:
        raise StyleVariantError("Could not write file " + str(path) + ": " + str(exception)) from exception


def format_console_summary(result: StyleVariantRunResult, include_paths: bool) -> str:
    lines = [
        "PulseForge style variants",
        "rawSource: " + str(result.raw_beatmap_path),
        "rawEventCount: " + str(result.raw_event_count),
    ]

    for variant in result.variants:
        lines.extend(
            [
                "",
                "style: " + variant.style,
                "outputEventCount: " + str(variant.output_event_count),
                "droppedEventCount: " + str(variant.dropped_event_count),
                "Guard: " + str(variant.action_counts.get("Guard", 0)),
                "Strike: " + str(variant.action_counts.get("Strike", 0)),
            ]
        )
        if variant.compare_summary is not None:
            lines.extend(
                [
                    "compareMeanAbsoluteErrorMs: " + format_float(variant.compare_summary["meanAbsoluteErrorMs"]),
                    "compareMaxAbsoluteErrorMs: " + format_float(variant.compare_summary["maxAbsoluteErrorMs"]),
                ]
            )
        if include_paths:
            lines.extend(
                [
                    "playableOutput: " + str(variant.playable_beatmap_path),
                    "postprocessReport: " + str(variant.postprocess_report_path),
                ]
            )
            if variant.compare_report_path is not None:
                lines.append("compareReport: " + str(variant.compare_report_path))

    return "\n".join(lines) + "\n"


def format_float(value: float) -> str:
    return f"{value:.6f}"


def build_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Generate PulseForge playable beatmap variants for multiple combat styles."
    )
    parser.add_argument("--input-wav", help="Input PCM WAV path. Mutually exclusive with --input-raw-json.")
    parser.add_argument("--input-raw-json", help="Existing raw schemaVersion 1 beatmap JSON path.")
    parser.add_argument("--output-dir", required=True, help="Directory for generated raw/playable beatmap JSON files.")
    parser.add_argument("--name", help="Output name suffix. Defaults to the input file name.")
    parser.add_argument("--difficulty", choices=tuple(postprocess_beatmap.DIFFICULTY_MIN_GAP_SECONDS.keys()), default="normal")
    parser.add_argument("--detection-mode", choices=pulseforge_audio_analyzer.DETECTION_MODES, default="amplitude")
    parser.add_argument("--threshold-ratio", type=float, default=run_debug_pipeline.ANALYZER_DEFAULT_THRESHOLD_RATIO)
    parser.add_argument("--min-analyzer-gap-seconds", type=float)
    parser.add_argument("--styles", default=",".join(DEFAULT_STYLES))
    parser.add_argument("--burst-window-seconds", type=float, default=postprocess_beatmap.DEFAULT_BURST_WINDOW_SECONDS)
    parser.add_argument("--expected-json", help="Optional expected/reference beatmap JSON path.")
    parser.add_argument("--compare-tolerance-ms", type=float, default=run_debug_pipeline.DEFAULT_COMPARE_TOLERANCE_MS)
    parser.add_argument("--summary", action="store_true", help="Print generated output and report paths.")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_argument_parser()
    args = parser.parse_args(argv)

    try:
        result = generate_style_variants(
            input_wav=args.input_wav,
            input_raw_json=args.input_raw_json,
            output_dir=args.output_dir,
            name=args.name,
            difficulty=args.difficulty,
            detection_mode=args.detection_mode,
            threshold_ratio=args.threshold_ratio,
            min_analyzer_gap_seconds=args.min_analyzer_gap_seconds,
            styles=args.styles,
            burst_window_seconds=args.burst_window_seconds,
            expected_json=args.expected_json,
            compare_tolerance_ms=args.compare_tolerance_ms,
        )
        sys.stdout.write(format_console_summary(result, args.summary))
        return 0
    except (
        StyleVariantError,
        pulseforge_audio_analyzer.AnalyzerError,
        postprocess_beatmap.PostprocessError,
        compare_beatmaps.CompareError,
    ) as exception:
        print("Error: " + str(exception), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
