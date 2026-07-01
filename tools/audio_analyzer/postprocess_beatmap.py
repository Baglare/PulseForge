#!/usr/bin/env python3
"""Post-process PulseForge analyzer beatmaps into playable schemaVersion 1 beatmaps."""

from __future__ import annotations

import argparse
import json
import math
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Sequence


SCHEMA_VERSION = 1
SUPPORTED_ACTIONS = ("Guard", "Strike")
DIFFICULTY_MIN_GAP_SECONDS = {
    "easy": 0.45,
    "normal": 0.28,
    "hard": 0.18,
}
ACTION_MODES = ("preserve", "alternate", "pattern", "intensity")
COMBAT_STYLES = ("legacy", "balanced", "defensive", "aggressive", "bursty")
DEFAULT_BURST_WINDOW_SECONDS = 0.35
BALANCED_COMBAT_PATTERN = ("Guard", "Guard", "Strike", "Guard", "Strike", "Strike")
AGGRESSIVE_COMBAT_PATTERN = ("Strike", "Strike", "Guard", "Strike")


class PostprocessError(Exception):
    """Raised for user-facing post-processing errors."""


@dataclass(frozen=True)
class BeatmapEvent:
    event_id: str
    target_time_seconds: float
    action: str
    intensity: float


@dataclass(frozen=True)
class BeatmapDocument:
    path: Path
    display_name: str
    events: list[BeatmapEvent]


@dataclass(frozen=True)
class DroppedEvent:
    event: BeatmapEvent
    reason: str


@dataclass(frozen=True)
class PostprocessResult:
    input_path: Path
    input_event_count: int
    output_events: list[BeatmapEvent]
    dropped_events: list[DroppedEvent]
    output_document: dict
    difficulty: str
    min_gap_seconds: float
    action_mode: str
    combat_style: str
    resolved_action_mode: str
    burst_window_seconds: float
    max_events: int | None


@dataclass(frozen=True)
class ActionMappingConfig:
    combat_style: str
    action_mode: str
    resolved_action_mode: str
    action_pattern: list[str] | None
    burst_window_seconds: float


def load_beatmap(path: str | Path) -> BeatmapDocument:
    beatmap_path = Path(path)
    try:
        with beatmap_path.open("r", encoding="utf-8") as input_file:
            document = json.load(input_file)
    except json.JSONDecodeError as exception:
        raise PostprocessError("Invalid JSON in " + str(beatmap_path) + ": " + str(exception)) from exception
    except OSError as exception:
        raise PostprocessError("Could not read beatmap file " + str(beatmap_path) + ": " + str(exception)) from exception

    if not isinstance(document, dict):
        raise PostprocessError("Beatmap JSON root must be an object: " + str(beatmap_path))

    schema_version = document.get("schemaVersion")
    if schema_version != SCHEMA_VERSION:
        raise PostprocessError(
            "Unsupported schemaVersion in "
            + str(beatmap_path)
            + ": expected 1, got "
            + str(schema_version)
        )

    if "events" not in document:
        raise PostprocessError("Beatmap JSON is missing 'events': " + str(beatmap_path))

    raw_events = document["events"]
    if not isinstance(raw_events, list):
        raise PostprocessError("Beatmap JSON 'events' must be a list: " + str(beatmap_path))

    display_name = document.get("displayName")
    if not isinstance(display_name, str) or display_name.strip() == "":
        display_name = beatmap_path.stem

    global_offset_seconds = document.get("globalOffsetSeconds", 0.0)
    if not is_finite_number(global_offset_seconds):
        raise PostprocessError("Beatmap JSON globalOffsetSeconds must be a finite number: " + str(beatmap_path))

    events = [
        parse_event(raw_event, index, beatmap_path, float(global_offset_seconds))
        for index, raw_event in enumerate(raw_events)
    ]
    return BeatmapDocument(path=beatmap_path, display_name=display_name, events=events)


def parse_event(
    raw_event: object,
    index: int,
    beatmap_path: Path,
    global_offset_seconds: float,
) -> BeatmapEvent:
    if not isinstance(raw_event, dict):
        raise PostprocessError("Event " + str(index) + " must be an object in " + str(beatmap_path))

    event_id = raw_event.get("eventId", "")
    if event_id is None:
        event_id = ""

    target_time_seconds = raw_event.get("targetTimeSeconds")
    if not is_finite_number(target_time_seconds):
        raise PostprocessError(
            "Event "
            + str(index)
            + " has invalid targetTimeSeconds in "
            + str(beatmap_path)
        )

    intensity = raw_event.get("intensity", 1.0)
    if not is_finite_number(intensity):
        raise PostprocessError("Event " + str(index) + " has invalid intensity in " + str(beatmap_path))

    return BeatmapEvent(
        event_id=str(event_id),
        target_time_seconds=float(target_time_seconds) + global_offset_seconds,
        action=normalize_action(raw_event.get("action")),
        intensity=clamp01(float(intensity)),
    )


def postprocess_beatmap_file(
    input_json: str | Path,
    *,
    display_name: str | None = None,
    difficulty: str = "normal",
    min_gap_seconds: float | None = None,
    max_events: int | None = None,
    action_mode: str | None = None,
    pattern_text: str | None = None,
    intensity_strike_threshold: float = 0.65,
    combat_style: str = "legacy",
    burst_window_seconds: float = DEFAULT_BURST_WINDOW_SECONDS,
) -> PostprocessResult:
    document = load_beatmap(input_json)
    return postprocess_beatmap(
        document,
        display_name=display_name,
        difficulty=difficulty,
        min_gap_seconds=min_gap_seconds,
        max_events=max_events,
        action_mode=action_mode,
        pattern_text=pattern_text,
        intensity_strike_threshold=intensity_strike_threshold,
        combat_style=combat_style,
        burst_window_seconds=burst_window_seconds,
    )


def postprocess_beatmap(
    document: BeatmapDocument,
    *,
    display_name: str | None = None,
    difficulty: str = "normal",
    min_gap_seconds: float | None = None,
    max_events: int | None = None,
    action_mode: str | None = None,
    pattern_text: str | None = None,
    intensity_strike_threshold: float = 0.65,
    combat_style: str = "legacy",
    burst_window_seconds: float = DEFAULT_BURST_WINDOW_SECONDS,
) -> PostprocessResult:
    resolved_difficulty = normalize_difficulty(difficulty)
    resolved_min_gap_seconds = resolve_min_gap_seconds(resolved_difficulty, min_gap_seconds)
    action_mapping = resolve_action_mapping_config(
        combat_style=combat_style,
        action_mode=action_mode,
        pattern_text=pattern_text,
        burst_window_seconds=burst_window_seconds,
    )
    validate_max_events(max_events)
    validate_intensity_threshold(intensity_strike_threshold)

    sorted_events = sorted(document.events, key=lambda beat_event: beat_event.target_time_seconds)
    filtered_events, dropped_events = filter_events_by_min_gap(sorted_events, resolved_min_gap_seconds)
    if max_events is not None and len(filtered_events) > max_events:
        for dropped_event in filtered_events[max_events:]:
            dropped_events.append(DroppedEvent(dropped_event, "max-events"))

        filtered_events = filtered_events[:max_events]

    output_events = assign_output_actions(
        filtered_events,
        action_mode=action_mapping.action_mode,
        action_pattern=action_mapping.action_pattern,
        intensity_strike_threshold=intensity_strike_threshold,
        combat_style=action_mapping.combat_style,
        burst_window_seconds=action_mapping.burst_window_seconds,
    )
    output_document = build_output_document(
        output_events,
        display_name or document.display_name,
    )

    return PostprocessResult(
        input_path=document.path,
        input_event_count=len(document.events),
        output_events=output_events,
        dropped_events=dropped_events,
        output_document=output_document,
        difficulty=resolved_difficulty,
        min_gap_seconds=resolved_min_gap_seconds,
        action_mode=action_mapping.action_mode,
        combat_style=action_mapping.combat_style,
        resolved_action_mode=action_mapping.resolved_action_mode,
        burst_window_seconds=action_mapping.burst_window_seconds,
        max_events=max_events,
    )


def filter_events_by_min_gap(
    events: Sequence[BeatmapEvent],
    min_gap_seconds: float,
) -> tuple[list[BeatmapEvent], list[DroppedEvent]]:
    kept_events: list[BeatmapEvent] = []
    dropped_events: list[DroppedEvent] = []

    for event in events:
        if not kept_events:
            kept_events.append(event)
            continue

        previous_event = kept_events[-1]
        if event.target_time_seconds - previous_event.target_time_seconds >= min_gap_seconds:
            kept_events.append(event)
            continue

        if event.intensity > previous_event.intensity:
            kept_events[-1] = event
            dropped_events.append(DroppedEvent(previous_event, "min-gap lower intensity replaced"))
        elif event.intensity == previous_event.intensity:
            dropped_events.append(DroppedEvent(event, "min-gap equal intensity earlier kept"))
        else:
            dropped_events.append(DroppedEvent(event, "min-gap lower intensity"))

    return kept_events, dropped_events


def assign_output_actions(
    events: Sequence[BeatmapEvent],
    *,
    action_mode: str,
    action_pattern: Sequence[str] | None,
    intensity_strike_threshold: float,
    combat_style: str,
    burst_window_seconds: float,
) -> list[BeatmapEvent]:
    output_events: list[BeatmapEvent] = []
    output_actions: list[str] = []
    for index, event in enumerate(events):
        if combat_style == "legacy":
            action = select_action(
                index,
                event,
                action_mode=action_mode,
                action_pattern=action_pattern,
                intensity_strike_threshold=intensity_strike_threshold,
            )
        else:
            previous_event = events[index - 1] if index > 0 else None
            previous_action = output_actions[-1] if output_actions else None
            action = select_combat_style_action(
                index,
                event,
                previous_event=previous_event,
                previous_action=previous_action,
                combat_style=combat_style,
                intensity_strike_threshold=intensity_strike_threshold,
                burst_window_seconds=burst_window_seconds,
            )

        output_actions.append(action)
        output_events.append(
            BeatmapEvent(
                event_id=format_event_id(index),
                target_time_seconds=event.target_time_seconds,
                action=action,
                intensity=clamp01(event.intensity),
            )
        )

    return output_events


def select_combat_style_action(
    index: int,
    event: BeatmapEvent,
    *,
    previous_event: BeatmapEvent | None,
    previous_action: str | None,
    combat_style: str,
    intensity_strike_threshold: float,
    burst_window_seconds: float,
) -> str:
    if combat_style == "balanced":
        return select_balanced_combat_action(
            index,
            event,
            previous_action=previous_action,
            intensity_strike_threshold=intensity_strike_threshold,
        )

    if combat_style == "defensive":
        return select_defensive_combat_action(
            index,
            event,
            previous_action=previous_action,
            intensity_strike_threshold=intensity_strike_threshold,
        )

    if combat_style == "aggressive":
        return select_aggressive_combat_action(
            index,
            event,
            intensity_strike_threshold=intensity_strike_threshold,
        )

    if combat_style == "bursty":
        return select_bursty_combat_action(
            index,
            event,
            previous_event=previous_event,
            intensity_strike_threshold=intensity_strike_threshold,
            burst_window_seconds=burst_window_seconds,
        )

    raise PostprocessError("--combat-style must be legacy, balanced, defensive, aggressive, or bursty.")


def select_balanced_combat_action(
    index: int,
    event: BeatmapEvent,
    *,
    previous_action: str | None,
    intensity_strike_threshold: float,
) -> str:
    action = BALANCED_COMBAT_PATTERN[index % len(BALANCED_COMBAT_PATTERN)]
    if (
        event.intensity >= intensity_strike_threshold
        and action == "Guard"
        and index % 3 == 1
        and previous_action != "Strike"
    ):
        return "Strike"

    return action


def select_defensive_combat_action(
    index: int,
    event: BeatmapEvent,
    *,
    previous_action: str | None,
    intensity_strike_threshold: float,
) -> str:
    if (
        event.intensity >= intensity_strike_threshold
        and index % 3 == 2
        and previous_action != "Strike"
    ):
        return "Strike"

    return "Guard"


def select_aggressive_combat_action(
    index: int,
    event: BeatmapEvent,
    *,
    intensity_strike_threshold: float,
) -> str:
    if event.intensity < intensity_strike_threshold and index % 4 == 1:
        return "Guard"

    return AGGRESSIVE_COMBAT_PATTERN[index % len(AGGRESSIVE_COMBAT_PATTERN)]


def select_bursty_combat_action(
    index: int,
    event: BeatmapEvent,
    *,
    previous_event: BeatmapEvent | None,
    intensity_strike_threshold: float,
    burst_window_seconds: float,
) -> str:
    if event.intensity >= intensity_strike_threshold:
        return "Strike"

    if (
        previous_event is not None
        and event.target_time_seconds - previous_event.target_time_seconds <= burst_window_seconds
    ):
        return "Strike"

    return BALANCED_COMBAT_PATTERN[index % len(BALANCED_COMBAT_PATTERN)]


def select_action(
    index: int,
    event: BeatmapEvent,
    *,
    action_mode: str,
    action_pattern: Sequence[str] | None,
    intensity_strike_threshold: float,
) -> str:
    if action_mode == "preserve":
        return normalize_action(event.action)

    if action_mode == "alternate":
        return "Guard" if index % 2 == 0 else "Strike"

    if action_mode == "pattern":
        if not action_pattern:
            raise PostprocessError("--pattern must contain at least one action when action mode is pattern.")

        return action_pattern[index % len(action_pattern)]

    if action_mode == "intensity":
        return "Strike" if event.intensity >= intensity_strike_threshold else "Guard"

    raise PostprocessError("--action-mode must be preserve, alternate, pattern, or intensity.")


def build_output_document(events: Sequence[BeatmapEvent], display_name: str) -> dict:
    return {
        "schemaVersion": SCHEMA_VERSION,
        "displayName": display_name,
        "globalOffsetSeconds": 0.0,
        "events": [serialize_event(event) for event in events],
    }


def serialize_event(event: BeatmapEvent) -> dict:
    return {
        "eventId": event.event_id,
        "targetTimeSeconds": round(event.target_time_seconds, 6),
        "action": event.action,
        "intensity": round(clamp01(event.intensity), 6),
    }


def build_report(result: PostprocessResult) -> dict:
    return {
        "inputPath": str(result.input_path),
        "outputEventCount": len(result.output_events),
        "inputEventCount": result.input_event_count,
        "droppedEventCount": len(result.dropped_events),
        "difficulty": result.difficulty,
        "minGapSeconds": result.min_gap_seconds,
        "actionMode": result.action_mode,
        "combatStyle": result.combat_style,
        "resolvedActionMode": result.resolved_action_mode,
        "burstWindowSeconds": result.burst_window_seconds,
        "actionCounts": count_actions(result.output_events),
        "maxEvents": result.max_events,
        "events": [serialize_event(event) for event in result.output_events],
        "droppedEvents": [
            {
                "eventId": dropped_event.event.event_id,
                "targetTimeSeconds": round(dropped_event.event.target_time_seconds, 6),
                "reason": dropped_event.reason,
            }
            for dropped_event in result.dropped_events
        ],
    }


def count_actions(events: Sequence[BeatmapEvent]) -> dict:
    counts = {action: 0 for action in SUPPORTED_ACTIONS}
    for event in events:
        counts[event.action] = counts.get(event.action, 0) + 1

    return counts


def dumps_beatmap(document: dict) -> str:
    return json.dumps(document, indent=2) + "\n"


def dumps_report(result: PostprocessResult) -> str:
    return json.dumps(build_report(result), indent=2) + "\n"


def normalize_action(action: object) -> str:
    if not isinstance(action, str) or action.strip() == "":
        raise PostprocessError("Action must be Guard or Strike.")

    normalized = action.strip()
    for supported_action in SUPPORTED_ACTIONS:
        if normalized.lower() == supported_action.lower():
            return supported_action

    raise PostprocessError(
        "Unsupported action '"
        + normalized
        + "'. Supported actions are Guard and Strike."
    )


def parse_action_pattern(pattern_text: str | None) -> list[str]:
    if pattern_text is None or pattern_text.strip() == "":
        raise PostprocessError("--pattern must contain at least one action when action mode is pattern.")

    parts = [part.strip() for part in pattern_text.split(",")]
    if not parts or any(part == "" for part in parts):
        raise PostprocessError("--pattern must be a comma-separated list of Guard and Strike actions.")

    return [normalize_action(part) for part in parts]


def normalize_difficulty(difficulty: str) -> str:
    if difficulty not in DIFFICULTY_MIN_GAP_SECONDS:
        raise PostprocessError("--difficulty must be easy, normal, or hard.")

    return difficulty


def normalize_action_mode(action_mode: str) -> str:
    if action_mode not in ACTION_MODES:
        raise PostprocessError("--action-mode must be preserve, alternate, pattern, or intensity.")

    return action_mode


def normalize_combat_style(combat_style: str) -> str:
    if combat_style not in COMBAT_STYLES:
        raise PostprocessError("--combat-style must be legacy, balanced, defensive, aggressive, or bursty.")

    return combat_style


def resolve_action_mapping_config(
    *,
    combat_style: str,
    action_mode: str | None,
    pattern_text: str | None,
    burst_window_seconds: float,
) -> ActionMappingConfig:
    resolved_combat_style = normalize_combat_style(combat_style)
    resolved_burst_window_seconds = resolve_burst_window_seconds(burst_window_seconds)

    if resolved_combat_style != "legacy":
        if action_mode is not None:
            raise PostprocessError(
                "--action-mode cannot be used with --combat-style "
                + resolved_combat_style
                + ". Use --combat-style legacy for action-mode mapping."
            )

        if pattern_text is not None and pattern_text.strip() != "":
            raise PostprocessError(
                "--pattern cannot be used with --combat-style "
                + resolved_combat_style
                + ". Use --combat-style legacy with --action-mode pattern."
            )

        return ActionMappingConfig(
            combat_style=resolved_combat_style,
            action_mode="combat-style",
            resolved_action_mode="combat-style:" + resolved_combat_style,
            action_pattern=None,
            burst_window_seconds=resolved_burst_window_seconds,
        )

    resolved_action_mode = normalize_action_mode(action_mode or "preserve")
    action_pattern = parse_action_pattern(pattern_text) if resolved_action_mode == "pattern" else None
    return ActionMappingConfig(
        combat_style=resolved_combat_style,
        action_mode=resolved_action_mode,
        resolved_action_mode=resolved_action_mode,
        action_pattern=action_pattern,
        burst_window_seconds=resolved_burst_window_seconds,
    )


def resolve_min_gap_seconds(difficulty: str, min_gap_seconds: float | None) -> float:
    if min_gap_seconds is None:
        return DIFFICULTY_MIN_GAP_SECONDS[difficulty]

    if not is_finite_number(min_gap_seconds) or min_gap_seconds < 0:
        raise PostprocessError("--min-gap-seconds must be a finite number greater than or equal to zero.")

    return float(min_gap_seconds)


def validate_max_events(max_events: int | None) -> None:
    if max_events is not None and max_events <= 0:
        raise PostprocessError("--max-events must be greater than zero when provided.")


def validate_intensity_threshold(intensity_strike_threshold: float) -> None:
    if not is_finite_number(intensity_strike_threshold):
        raise PostprocessError("--intensity-strike-threshold must be a finite number.")


def resolve_burst_window_seconds(burst_window_seconds: float) -> float:
    if not is_finite_number(burst_window_seconds) or burst_window_seconds < 0:
        raise PostprocessError("--burst-window-seconds must be a finite number greater than or equal to zero.")

    return float(burst_window_seconds)


def format_event_id(index: int) -> str:
    return "event-" + str(index + 1).zfill(3)


def clamp01(value: float) -> float:
    if value < 0.0:
        return 0.0

    if value > 1.0:
        return 1.0

    return value


def is_finite_number(value: object) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def write_output(output_path: str | Path, text: str) -> None:
    path = Path(output_path)
    try:
        if path.parent != Path(""):
            path.parent.mkdir(parents=True, exist_ok=True)

        path.write_text(text, encoding="utf-8")
    except OSError as exception:
        raise PostprocessError("Could not write output file: " + str(exception)) from exception


def build_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Post-process a PulseForge schemaVersion 1 beatmap into a playable beatmap."
    )
    parser.add_argument("input_json", help="Input schemaVersion 1 beatmap JSON path.")
    parser.add_argument("--output", "-o", help="Output beatmap JSON path. If omitted, JSON is printed to stdout.")
    parser.add_argument("--display-name", help="Output beatmap display name.")
    parser.add_argument("--difficulty", choices=tuple(DIFFICULTY_MIN_GAP_SECONDS.keys()), default="normal")
    parser.add_argument("--min-gap-seconds", type=float)
    parser.add_argument("--max-events", type=int)
    parser.add_argument("--action-mode", choices=ACTION_MODES)
    parser.add_argument("--pattern", help="Comma-separated action pattern, for example Guard,Guard,Strike.")
    parser.add_argument("--intensity-strike-threshold", type=float, default=0.65)
    parser.add_argument("--combat-style", choices=COMBAT_STYLES, default="legacy")
    parser.add_argument("--burst-window-seconds", type=float, default=DEFAULT_BURST_WINDOW_SECONDS)
    parser.add_argument("--report-output", help="Optional post-process report JSON path.")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_argument_parser()
    args = parser.parse_args(argv)

    try:
        result = postprocess_beatmap_file(
            args.input_json,
            display_name=args.display_name,
            difficulty=args.difficulty,
            min_gap_seconds=args.min_gap_seconds,
            max_events=args.max_events,
            action_mode=args.action_mode,
            pattern_text=args.pattern,
            intensity_strike_threshold=args.intensity_strike_threshold,
            combat_style=args.combat_style,
            burst_window_seconds=args.burst_window_seconds,
        )
        output_text = dumps_beatmap(result.output_document)
        if args.output:
            write_output(args.output, output_text)
        else:
            sys.stdout.write(output_text)

        if args.report_output:
            write_output(args.report_output, dumps_report(result))

        return 0
    except PostprocessError as exception:
        print("Error: " + str(exception), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
