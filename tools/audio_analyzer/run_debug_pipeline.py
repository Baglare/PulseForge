#!/usr/bin/env python3
"""Run the PulseForge debug audio analyzer pipeline with one command."""

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


ANALYZER_DEFAULT_THRESHOLD_RATIO = 0.35
ANALYZER_DEFAULT_MIN_GAP_SECONDS = 0.18
DEFAULT_COMPARE_TOLERANCE_MS = 40.0


class PipelineError(Exception):
    """Raised for user-facing pipeline errors."""


@dataclass(frozen=True)
class PipelineResult:
    raw_beatmap_path: Path
    playable_beatmap_path: Path
    analysis_report_path: Path
    postprocess_report_path: Path
    compare_report_path: Path | None
    debug_csv_path: Path | None
    raw_event_count: int
    playable_event_count: int
    dropped_event_count: int
    compare_summary: dict | None


def run_pipeline(
    *,
    input_wav: str | Path,
    output_dir: str | Path,
    name: str | None = None,
    pattern: str | None = None,
    detection_mode: str = "amplitude",
    difficulty: str = "normal",
    min_gap_seconds: float | None = None,
    action_mode: str | None = None,
    threshold_ratio: float = ANALYZER_DEFAULT_THRESHOLD_RATIO,
    min_analyzer_gap_seconds: float | None = None,
    expected_json: str | Path | None = None,
    compare_tolerance_ms: float = DEFAULT_COMPARE_TOLERANCE_MS,
    write_debug_csv: bool = False,
    diagnostics_dir: str | Path | None = None,
) -> PipelineResult:
    input_wav_path = Path(input_wav)
    if not input_wav_path.exists():
        raise PipelineError("Input WAV file does not exist: " + str(input_wav_path))

    resolved_name = resolve_name(name, input_wav_path)
    resolved_action_mode = resolve_action_mode(action_mode, pattern)
    resolved_output_dir = Path(output_dir)
    resolved_diagnostics_dir = Path(diagnostics_dir) if diagnostics_dir is not None else SCRIPT_DIR / "out"
    resolved_output_dir.mkdir(parents=True, exist_ok=True)
    resolved_diagnostics_dir.mkdir(parents=True, exist_ok=True)

    raw_beatmap_path = resolved_output_dir / ("BM_Raw_" + resolved_name + ".json")
    playable_beatmap_path = resolved_output_dir / ("BM_Playable_" + resolved_name + ".json")
    analysis_report_path = resolved_diagnostics_dir / (resolved_name + "_analysis_report.json")
    postprocess_report_path = resolved_diagnostics_dir / (resolved_name + "_postprocess_report.json")
    debug_csv_path = (
        resolved_diagnostics_dir / (resolved_name + "_frames.csv")
        if write_debug_csv
        else None
    )

    analyzer_result = pulseforge_audio_analyzer.analyze_wav(
        input_wav_path,
        display_name="Raw " + resolved_name,
        threshold_ratio=threshold_ratio,
        min_gap_seconds=resolve_analyzer_min_gap_seconds(min_analyzer_gap_seconds),
        pattern_text=pattern,
        detection_mode=detection_mode,
    )
    write_text(raw_beatmap_path, pulseforge_audio_analyzer.dumps_beatmap(analyzer_result.beatmap_document))
    write_text(analysis_report_path, pulseforge_audio_analyzer.dumps_analysis_report(analyzer_result))
    if debug_csv_path is not None:
        write_text(debug_csv_path, pulseforge_audio_analyzer.dumps_debug_csv(analyzer_result))

    postprocess_result = postprocess_beatmap.postprocess_beatmap_file(
        raw_beatmap_path,
        display_name="Playable " + resolved_name,
        difficulty=difficulty,
        min_gap_seconds=min_gap_seconds,
        action_mode=resolved_action_mode,
        pattern_text=pattern,
    )
    write_text(playable_beatmap_path, postprocess_beatmap.dumps_beatmap(postprocess_result.output_document))
    write_text(postprocess_report_path, postprocess_beatmap.dumps_report(postprocess_result))

    compare_report_path = None
    compare_summary = None
    if expected_json is not None:
        compare_report_path = resolved_diagnostics_dir / (resolved_name + "_compare_report.json")
        compare_report = compare_beatmaps.compare_beatmap_files(
            expected_json,
            playable_beatmap_path,
            tolerance_ms=compare_tolerance_ms,
        )
        write_text(compare_report_path, compare_beatmaps.dumps_report(compare_report))
        compare_summary = compare_report["summary"]

    return PipelineResult(
        raw_beatmap_path=raw_beatmap_path,
        playable_beatmap_path=playable_beatmap_path,
        analysis_report_path=analysis_report_path,
        postprocess_report_path=postprocess_report_path,
        compare_report_path=compare_report_path,
        debug_csv_path=debug_csv_path,
        raw_event_count=len(analyzer_result.beatmap_document["events"]),
        playable_event_count=len(postprocess_result.output_document["events"]),
        dropped_event_count=len(postprocess_result.dropped_events),
        compare_summary=compare_summary,
    )


def resolve_name(name: str | None, input_wav_path: Path) -> str:
    if name is not None and name.strip() != "":
        return name.strip()

    return input_wav_path.stem


def resolve_action_mode(action_mode: str | None, pattern: str | None) -> str:
    if action_mode is not None:
        return action_mode

    if pattern is not None and pattern.strip() != "":
        return "pattern"

    return "preserve"


def resolve_analyzer_min_gap_seconds(min_analyzer_gap_seconds: float | None) -> float:
    if min_analyzer_gap_seconds is None:
        return ANALYZER_DEFAULT_MIN_GAP_SECONDS

    return min_analyzer_gap_seconds


def write_text(path: Path, text: str) -> None:
    try:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
    except OSError as exception:
        raise PipelineError("Could not write file " + str(path) + ": " + str(exception)) from exception


def format_console_summary(result: PipelineResult, include_paths: bool) -> str:
    lines = [
        "PulseForge debug pipeline",
        "rawEventCount: " + str(result.raw_event_count),
        "playableEventCount: " + str(result.playable_event_count),
        "droppedEventCount: " + str(result.dropped_event_count),
        "playableOutput: " + str(result.playable_beatmap_path),
    ]

    if result.compare_summary is not None:
        lines.extend(
            [
                "compareMeanSignedErrorMs: " + format_float(result.compare_summary["meanSignedErrorMs"]),
                "compareMeanAbsoluteErrorMs: " + format_float(result.compare_summary["meanAbsoluteErrorMs"]),
                "compareMaxAbsoluteErrorMs: " + format_float(result.compare_summary["maxAbsoluteErrorMs"]),
                "compareSuggestedGlobalOffsetSeconds: "
                + format_float(result.compare_summary["suggestedGlobalOffsetSeconds"]),
            ]
        )

    if include_paths:
        lines.extend(
            [
                "rawOutput: " + str(result.raw_beatmap_path),
                "analysisReport: " + str(result.analysis_report_path),
                "postprocessReport: " + str(result.postprocess_report_path),
            ]
        )
        if result.compare_report_path is not None:
            lines.append("compareReport: " + str(result.compare_report_path))
        if result.debug_csv_path is not None:
            lines.append("debugCsv: " + str(result.debug_csv_path))

    return "\n".join(lines) + "\n"


def format_float(value: float) -> str:
    return f"{value:.6f}"


def build_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Run the PulseForge debug audio analyzer pipeline.")
    parser.add_argument("--input-wav", required=True, help="Input PCM WAV path.")
    parser.add_argument("--output-dir", required=True, help="Directory for raw and playable beatmap JSON files.")
    parser.add_argument("--name", help="Output name suffix. Defaults to the input WAV file name.")
    parser.add_argument("--pattern", help="Comma-separated action pattern, for example Guard,Strike.")
    parser.add_argument("--detection-mode", choices=("amplitude", "onset"), default="amplitude")
    parser.add_argument("--difficulty", choices=("easy", "normal", "hard"), default="normal")
    parser.add_argument("--min-gap-seconds", type=float)
    parser.add_argument("--action-mode", choices=("preserve", "alternate", "pattern", "intensity"))
    parser.add_argument("--threshold-ratio", type=float, default=ANALYZER_DEFAULT_THRESHOLD_RATIO)
    parser.add_argument("--min-analyzer-gap-seconds", type=float)
    parser.add_argument("--expected-json", help="Optional expected/reference beatmap JSON path.")
    parser.add_argument("--compare-tolerance-ms", type=float, default=DEFAULT_COMPARE_TOLERANCE_MS)
    parser.add_argument("--write-debug-csv", action="store_true")
    parser.add_argument("--summary", action="store_true", help="Print generated report paths in the summary.")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_argument_parser()
    args = parser.parse_args(argv)

    try:
        result = run_pipeline(
            input_wav=args.input_wav,
            output_dir=args.output_dir,
            name=args.name,
            pattern=args.pattern,
            detection_mode=args.detection_mode,
            difficulty=args.difficulty,
            min_gap_seconds=args.min_gap_seconds,
            action_mode=args.action_mode,
            threshold_ratio=args.threshold_ratio,
            min_analyzer_gap_seconds=args.min_analyzer_gap_seconds,
            expected_json=args.expected_json,
            compare_tolerance_ms=args.compare_tolerance_ms,
            write_debug_csv=args.write_debug_csv,
        )
        sys.stdout.write(format_console_summary(result, args.summary))
        return 0
    except (
        PipelineError,
        pulseforge_audio_analyzer.AnalyzerError,
        postprocess_beatmap.PostprocessError,
        compare_beatmaps.CompareError,
    ) as exception:
        print("Error: " + str(exception), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
