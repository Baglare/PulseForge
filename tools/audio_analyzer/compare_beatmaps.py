#!/usr/bin/env python3
"""Compare two PulseForge schemaVersion 1 beatmap JSON files."""

from __future__ import annotations

import argparse
import json
import math
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Sequence


SCHEMA_VERSION = 1


class CompareError(Exception):
    """Raised for user-facing comparison errors."""


@dataclass(frozen=True)
class BeatmapEvent:
    event_id: str
    target_time_seconds: float
    action: str


@dataclass(frozen=True)
class BeatmapDocument:
    path: Path
    events: list[BeatmapEvent]


def load_beatmap(path: str | Path) -> BeatmapDocument:
    beatmap_path = Path(path)
    try:
        with beatmap_path.open("r", encoding="utf-8") as input_file:
            document = json.load(input_file)
    except json.JSONDecodeError as exception:
        raise CompareError("Invalid JSON in " + str(beatmap_path) + ": " + str(exception)) from exception
    except OSError as exception:
        raise CompareError("Could not read beatmap file " + str(beatmap_path) + ": " + str(exception)) from exception

    if not isinstance(document, dict):
        raise CompareError("Beatmap JSON root must be an object: " + str(beatmap_path))

    schema_version = document.get("schemaVersion")
    if schema_version != SCHEMA_VERSION:
        raise CompareError(
            "Unsupported schemaVersion in "
            + str(beatmap_path)
            + ": expected 1, got "
            + str(schema_version)
        )

    if "events" not in document:
        raise CompareError("Beatmap JSON is missing 'events': " + str(beatmap_path))

    raw_events = document["events"]
    if not isinstance(raw_events, list):
        raise CompareError("Beatmap JSON 'events' must be a list: " + str(beatmap_path))

    events = [parse_event(raw_event, index, beatmap_path) for index, raw_event in enumerate(raw_events)]
    return BeatmapDocument(path=beatmap_path, events=events)


def parse_event(raw_event: object, index: int, beatmap_path: Path) -> BeatmapEvent:
    if not isinstance(raw_event, dict):
        raise CompareError("Event " + str(index) + " must be an object in " + str(beatmap_path))

    event_id = raw_event.get("eventId", "")
    if event_id is None:
        event_id = ""

    target_time_seconds = raw_event.get("targetTimeSeconds")
    if not is_finite_number(target_time_seconds):
        raise CompareError(
            "Event "
            + str(index)
            + " has invalid targetTimeSeconds in "
            + str(beatmap_path)
        )

    action = raw_event.get("action")
    if not isinstance(action, str) or action.strip() == "":
        raise CompareError("Event " + str(index) + " has invalid action in " + str(beatmap_path))

    return BeatmapEvent(
        event_id=str(event_id),
        target_time_seconds=float(target_time_seconds),
        action=action,
    )


def compare_beatmap_files(
    expected_json: str | Path,
    actual_json: str | Path,
    *,
    tolerance_ms: float = 40.0,
) -> dict:
    expected = load_beatmap(expected_json)
    actual = load_beatmap(actual_json)
    return compare_beatmaps(expected, actual, tolerance_ms=tolerance_ms)


def compare_beatmaps(
    expected: BeatmapDocument,
    actual: BeatmapDocument,
    *,
    tolerance_ms: float = 40.0,
) -> dict:
    if tolerance_ms < 0:
        raise CompareError("--tolerance-ms must be greater than or equal to zero.")

    compared_event_count = min(len(expected.events), len(actual.events))
    comparisons = [
        build_comparison_item(index, expected.events[index], actual.events[index], tolerance_ms)
        for index in range(compared_event_count)
    ]
    summary = build_summary(
        expected_event_count=len(expected.events),
        actual_event_count=len(actual.events),
        comparisons=comparisons,
    )

    return {
        "expectedPath": str(expected.path),
        "actualPath": str(actual.path),
        "toleranceMs": tolerance_ms,
        "summary": summary,
        "comparisons": comparisons,
    }


def build_comparison_item(
    index: int,
    expected_event: BeatmapEvent,
    actual_event: BeatmapEvent,
    tolerance_ms: float,
) -> dict:
    error_ms = (actual_event.target_time_seconds - expected_event.target_time_seconds) * 1000.0
    abs_error_ms = abs(error_ms)
    return {
        "index": index,
        "expectedEventId": expected_event.event_id,
        "actualEventId": actual_event.event_id,
        "expectedTimeSeconds": expected_event.target_time_seconds,
        "actualTimeSeconds": actual_event.target_time_seconds,
        "errorMs": round(error_ms, 6),
        "absErrorMs": round(abs_error_ms, 6),
        "expectedAction": expected_event.action,
        "actualAction": actual_event.action,
        "actionMatches": expected_event.action == actual_event.action,
        "withinTolerance": abs_error_ms <= tolerance_ms,
    }


def build_summary(expected_event_count: int, actual_event_count: int, comparisons: Sequence[dict]) -> dict:
    compared_event_count = len(comparisons)
    missing_event_count = max(0, expected_event_count - actual_event_count)
    extra_event_count = max(0, actual_event_count - expected_event_count)
    action_mismatch_count = sum(1 for item in comparisons if not item["actionMatches"])
    within_tolerance_count = sum(1 for item in comparisons if item["withinTolerance"])
    outside_tolerance_count = compared_event_count - within_tolerance_count

    mean_signed_error_ms = mean([item["errorMs"] for item in comparisons])
    mean_absolute_error_ms = mean([item["absErrorMs"] for item in comparisons])
    max_absolute_error_ms = max((item["absErrorMs"] for item in comparisons), default=0.0)

    return {
        "expectedEventCount": expected_event_count,
        "actualEventCount": actual_event_count,
        "comparedEventCount": compared_event_count,
        "missingEventCount": missing_event_count,
        "extraEventCount": extra_event_count,
        "actionMismatchCount": action_mismatch_count,
        "withinToleranceCount": within_tolerance_count,
        "outsideToleranceCount": outside_tolerance_count,
        "meanSignedErrorMs": round(mean_signed_error_ms, 6),
        "meanAbsoluteErrorMs": round(mean_absolute_error_ms, 6),
        "maxAbsoluteErrorMs": round(max_absolute_error_ms, 6),
        "suggestedGlobalOffsetSeconds": normalize_zero(round(-(mean_signed_error_ms / 1000.0), 6)),
    }


def mean(values: Sequence[float]) -> float:
    if not values:
        return 0.0

    return sum(values) / len(values)


def is_strict_failure(summary: dict) -> bool:
    return (
        summary["missingEventCount"] > 0
        or summary["extraEventCount"] > 0
        or summary["actionMismatchCount"] > 0
        or summary["outsideToleranceCount"] > 0
    )


def format_console_summary(report: dict) -> str:
    summary = report["summary"]
    lines = [
        "Beatmap comparison",
        "expectedEventCount: " + str(summary["expectedEventCount"]),
        "actualEventCount: " + str(summary["actualEventCount"]),
        "comparedEventCount: " + str(summary["comparedEventCount"]),
        "missingEventCount: " + str(summary["missingEventCount"]),
        "extraEventCount: " + str(summary["extraEventCount"]),
        "actionMismatchCount: " + str(summary["actionMismatchCount"]),
        "withinToleranceCount: " + str(summary["withinToleranceCount"]),
        "outsideToleranceCount: " + str(summary["outsideToleranceCount"]),
        "meanSignedErrorMs: " + format_float(summary["meanSignedErrorMs"]),
        "meanAbsoluteErrorMs: " + format_float(summary["meanAbsoluteErrorMs"]),
        "maxAbsoluteErrorMs: " + format_float(summary["maxAbsoluteErrorMs"]),
        "suggestedGlobalOffsetSeconds: " + format_float(summary["suggestedGlobalOffsetSeconds"]),
    ]
    return "\n".join(lines) + "\n"


def dumps_report(report: dict) -> str:
    return json.dumps(report, indent=2) + "\n"


def write_output(output_path: str | Path, text: str) -> None:
    path = Path(output_path)
    try:
        if path.parent != Path(""):
            path.parent.mkdir(parents=True, exist_ok=True)

        path.write_text(text, encoding="utf-8")
    except OSError as exception:
        raise CompareError("Could not write report file: " + str(exception)) from exception


def is_finite_number(value: object) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def format_float(value: float) -> str:
    return f"{normalize_zero(value):.6f}"


def normalize_zero(value: float) -> float:
    if value == 0:
        return 0.0

    return value


def build_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Compare two PulseForge schemaVersion 1 beatmap JSON files."
    )
    parser.add_argument("expected_json", help="Expected/reference beatmap JSON path.")
    parser.add_argument("actual_json", help="Actual/generated beatmap JSON path.")
    parser.add_argument("--tolerance-ms", type=float, default=40.0)
    parser.add_argument("--report-output", help="Optional comparison report JSON path.")
    parser.add_argument(
        "--strict",
        action="store_true",
        help="Return a non-zero exit code on count, action, or tolerance mismatches.",
    )
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_argument_parser()
    args = parser.parse_args(argv)

    try:
        report = compare_beatmap_files(
            args.expected_json,
            args.actual_json,
            tolerance_ms=args.tolerance_ms,
        )
        sys.stdout.write(format_console_summary(report))

        if args.report_output:
            write_output(args.report_output, dumps_report(report))

        if args.strict and is_strict_failure(report["summary"]):
            return 2

        return 0
    except CompareError as exception:
        print("Error: " + str(exception), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
