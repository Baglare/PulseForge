import contextlib
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path


ANALYZER_DIR = Path(__file__).resolve().parents[1]
if str(ANALYZER_DIR) not in sys.path:
    sys.path.insert(0, str(ANALYZER_DIR))

import postprocess_beatmap as postprocess


class PostprocessBeatmapTests(unittest.TestCase):
    def test_schema_version_one_input_is_loaded(self):
        with tempfile.TemporaryDirectory() as temp_directory:
            beatmap_path = Path(temp_directory) / "input.json"
            write_input_beatmap(beatmap_path, [make_event("raw-001", 1.0, "Guard", 1.0)])

            document = postprocess.load_beatmap(beatmap_path)

            self.assertEqual(len(document.events), 1)
            self.assertEqual(document.events[0].action, "Guard")

    def test_close_events_keep_higher_intensity(self):
        document = make_document(
            [
                make_event("raw-001", 1.0, "Guard", 0.2),
                make_event("raw-002", 1.1, "Strike", 0.9),
            ]
        )

        result = postprocess.postprocess_beatmap(document, min_gap_seconds=0.28)

        self.assertEqual(len(result.output_events), 1)
        self.assertAlmostEqual(result.output_events[0].target_time_seconds, 1.1)
        self.assertEqual(result.dropped_events[0].event.event_id, "raw-001")

    def test_equal_intensity_keeps_earlier_event(self):
        document = make_document(
            [
                make_event("raw-001", 1.0, "Guard", 0.5),
                make_event("raw-002", 1.1, "Strike", 0.5),
            ]
        )

        result = postprocess.postprocess_beatmap(document, min_gap_seconds=0.28)

        self.assertEqual(len(result.output_events), 1)
        self.assertAlmostEqual(result.output_events[0].target_time_seconds, 1.0)
        self.assertEqual(result.dropped_events[0].event.event_id, "raw-002")

    def test_min_gap_filter_keeps_spaced_events(self):
        document = make_document(
            [
                make_event("raw-001", 1.0, "Guard", 0.5),
                make_event("raw-002", 1.2, "Strike", 0.9),
                make_event("raw-003", 1.5, "Guard", 0.4),
            ]
        )

        result = postprocess.postprocess_beatmap(document, min_gap_seconds=0.28)

        self.assertEqual([event.target_time_seconds for event in result.output_events], [1.2, 1.5])

    def test_max_events_limits_output_count(self):
        document = make_document(
            [
                make_event("raw-001", 1.0, "Guard", 0.5),
                make_event("raw-002", 1.5, "Strike", 0.5),
                make_event("raw-003", 2.0, "Guard", 0.5),
            ]
        )

        result = postprocess.postprocess_beatmap(document, min_gap_seconds=0.1, max_events=2)

        self.assertEqual(len(result.output_events), 2)
        self.assertEqual(result.dropped_events[-1].reason, "max-events")

    def test_preserve_action_mode_keeps_input_actions(self):
        document = make_document(
            [
                make_event("raw-001", 1.0, "Guard", 0.5),
                make_event("raw-002", 1.5, "Strike", 0.5),
            ]
        )

        result = postprocess.postprocess_beatmap(document, min_gap_seconds=0.1, action_mode="preserve")

        self.assertEqual([event.action for event in result.output_events], ["Guard", "Strike"])

    def test_legacy_combat_style_preserves_action_mode_behavior(self):
        document = make_document(
            [
                make_event("raw-001", 1.0, "Guard", 0.5),
                make_event("raw-002", 1.5, "Strike", 0.5),
            ]
        )

        result = postprocess.postprocess_beatmap(
            document,
            min_gap_seconds=0.1,
            combat_style="legacy",
            action_mode="preserve",
        )

        self.assertEqual(actions(result), ["Guard", "Strike"])
        self.assertEqual(result.resolved_action_mode, "preserve")

    def test_alternate_action_mode_generates_guard_strike_sequence(self):
        document = make_document(
            [
                make_event("raw-001", 1.0, "Strike", 0.5),
                make_event("raw-002", 1.5, "Strike", 0.5),
                make_event("raw-003", 2.0, "Strike", 0.5),
            ]
        )

        result = postprocess.postprocess_beatmap(document, min_gap_seconds=0.1, action_mode="alternate")

        self.assertEqual([event.action for event in result.output_events], ["Guard", "Strike", "Guard"])

    def test_pattern_action_mode_cycles_pattern(self):
        document = make_document(
            [
                make_event("raw-001", 1.0, "Guard", 0.5),
                make_event("raw-002", 1.5, "Guard", 0.5),
                make_event("raw-003", 2.0, "Guard", 0.5),
            ]
        )

        result = postprocess.postprocess_beatmap(
            document,
            min_gap_seconds=0.1,
            action_mode="pattern",
            pattern_text="Guard,Strike",
        )

        self.assertEqual([event.action for event in result.output_events], ["Guard", "Strike", "Guard"])

    def test_intensity_action_mode_uses_threshold(self):
        document = make_document(
            [
                make_event("raw-001", 1.0, "Guard", 0.4),
                make_event("raw-002", 1.5, "Guard", 0.8),
            ]
        )

        result = postprocess.postprocess_beatmap(
            document,
            min_gap_seconds=0.1,
            action_mode="intensity",
            intensity_strike_threshold=0.65,
        )

        self.assertEqual([event.action for event in result.output_events], ["Guard", "Strike"])

    def test_balanced_combat_style_generates_deterministic_sequence(self):
        document = make_document(make_spaced_events([0.4, 0.4, 0.4, 0.4, 0.4, 0.4]))

        result = postprocess.postprocess_beatmap(
            document,
            min_gap_seconds=0.1,
            combat_style="balanced",
        )

        self.assertEqual(actions(result), ["Guard", "Guard", "Strike", "Guard", "Strike", "Strike"])

    def test_defensive_combat_style_prefers_guard(self):
        document = make_document(make_spaced_events([0.2, 0.9, 0.9, 0.3, 0.9, 0.9]))

        result = postprocess.postprocess_beatmap(
            document,
            min_gap_seconds=0.1,
            combat_style="defensive",
            intensity_strike_threshold=0.65,
        )

        counts = action_counts(result)
        self.assertGreater(counts["Guard"], counts["Strike"])

    def test_aggressive_combat_style_prefers_strike(self):
        document = make_document(make_spaced_events([0.9, 0.9, 0.9, 0.9, 0.9, 0.9]))

        result = postprocess.postprocess_beatmap(
            document,
            min_gap_seconds=0.1,
            combat_style="aggressive",
            intensity_strike_threshold=0.65,
        )

        counts = action_counts(result)
        self.assertGreater(counts["Strike"], counts["Guard"])

    def test_bursty_combat_style_strikes_on_high_intensity_or_near_events(self):
        document = make_document(
            [
                make_event("raw-001", 1.0, "Guard", 0.2),
                make_event("raw-002", 1.3, "Guard", 0.2),
                make_event("raw-003", 2.0, "Guard", 0.9),
                make_event("raw-004", 2.6, "Guard", 0.2),
            ]
        )

        result = postprocess.postprocess_beatmap(
            document,
            min_gap_seconds=0.1,
            combat_style="bursty",
            intensity_strike_threshold=0.65,
            burst_window_seconds=0.35,
        )

        self.assertEqual(result.output_events[1].action, "Strike")
        self.assertEqual(result.output_events[2].action, "Strike")

    def test_non_legacy_combat_style_rejects_action_mode(self):
        document = make_document([make_event("raw-001", 1.0, "Guard", 0.5)])

        with self.assertRaisesRegex(postprocess.PostprocessError, "--action-mode"):
            postprocess.postprocess_beatmap(
                document,
                combat_style="balanced",
                action_mode="preserve",
            )

    def test_non_legacy_combat_style_rejects_pattern(self):
        document = make_document([make_event("raw-001", 1.0, "Guard", 0.5)])

        with self.assertRaisesRegex(postprocess.PostprocessError, "--pattern"):
            postprocess.postprocess_beatmap(
                document,
                combat_style="balanced",
                pattern_text="Guard,Strike",
            )

    def test_negative_burst_window_raises_user_facing_error(self):
        document = make_document([make_event("raw-001", 1.0, "Guard", 0.5)])

        with self.assertRaisesRegex(postprocess.PostprocessError, "--burst-window-seconds"):
            postprocess.postprocess_beatmap(
                document,
                combat_style="bursty",
                burst_window_seconds=-0.1,
            )

    def test_invalid_action_raises_user_facing_error(self):
        with tempfile.TemporaryDirectory() as temp_directory:
            beatmap_path = Path(temp_directory) / "input.json"
            write_input_beatmap(beatmap_path, [make_event("raw-001", 1.0, "Dodge", 1.0)])

            with self.assertRaises(postprocess.PostprocessError):
                postprocess.load_beatmap(beatmap_path)

    def test_empty_pattern_raises_user_facing_error(self):
        document = make_document([make_event("raw-001", 1.0, "Guard", 1.0)])

        with self.assertRaises(postprocess.PostprocessError):
            postprocess.postprocess_beatmap(
                document,
                action_mode="pattern",
                pattern_text="",
            )

    def test_output_event_ids_are_regenerated(self):
        document = make_document(
            [
                make_event("raw-a", 1.0, "Guard", 0.5),
                make_event("raw-b", 1.5, "Strike", 0.5),
            ]
        )

        result = postprocess.postprocess_beatmap(document, min_gap_seconds=0.1)

        self.assertEqual([event.event_id for event in result.output_events], ["event-001", "event-002"])

    def test_output_schema_version_is_one(self):
        document = make_document([make_event("raw-001", 1.0, "Guard", 1.0)])

        result = postprocess.postprocess_beatmap(document, min_gap_seconds=0.1)

        self.assertEqual(result.output_document["schemaVersion"], 1)

    def test_report_json_is_created(self):
        with tempfile.TemporaryDirectory() as temp_directory:
            temp_path = Path(temp_directory)
            input_path = temp_path / "input.json"
            report_path = temp_path / "reports" / "postprocess.json"
            write_input_beatmap(input_path, [make_event("raw-001", 1.0, "Guard", 1.0)])

            exit_code, _, _ = run_postprocess_main(
                [
                    str(input_path),
                    "--output",
                    str(temp_path / "output.json"),
                    "--report-output",
                    str(report_path),
                ]
            )

            report = json.loads(report_path.read_text(encoding="utf-8"))
            self.assertEqual(exit_code, 0)
            self.assertTrue(report_path.exists())
            self.assertEqual(report["inputEventCount"], 1)
            self.assertEqual(report["outputEventCount"], 1)

    def test_report_json_includes_combat_style_and_action_counts(self):
        document = make_document(make_spaced_events([0.4, 0.4, 0.4]))

        result = postprocess.postprocess_beatmap(
            document,
            min_gap_seconds=0.1,
            combat_style="balanced",
        )

        report = postprocess.build_report(result)
        self.assertEqual(report["combatStyle"], "balanced")
        self.assertEqual(report["resolvedActionMode"], "combat-style:balanced")
        self.assertEqual(report["actionCounts"]["Guard"], 2)
        self.assertEqual(report["actionCounts"]["Strike"], 1)
        self.assertEqual(report["burstWindowSeconds"], 0.35)

    def test_invalid_schema_version_raises_user_facing_error(self):
        with tempfile.TemporaryDirectory() as temp_directory:
            input_path = Path(temp_directory) / "input.json"
            input_path.write_text(
                json.dumps({"schemaVersion": 2, "events": []}),
                encoding="utf-8",
            )

            with self.assertRaises(postprocess.PostprocessError):
                postprocess.load_beatmap(input_path)

    def test_stdout_json_when_output_is_omitted(self):
        with tempfile.TemporaryDirectory() as temp_directory:
            input_path = Path(temp_directory) / "input.json"
            write_input_beatmap(input_path, [make_event("raw-001", 1.0, "Guard", 1.0)])

            exit_code, stdout, stderr = run_postprocess_main([str(input_path)])

        document = json.loads(stdout)
        self.assertEqual(exit_code, 0)
        self.assertEqual(stderr, "")
        self.assertEqual(document["schemaVersion"], 1)
        self.assertEqual(len(document["events"]), 1)

    def test_combat_style_output_schema_version_stays_one(self):
        document = make_document(make_spaced_events([0.4, 0.4]))

        result = postprocess.postprocess_beatmap(
            document,
            min_gap_seconds=0.1,
            combat_style="balanced",
        )

        self.assertEqual(result.output_document["schemaVersion"], 1)

    def test_combat_style_outputs_only_unity_supported_actions(self):
        document = make_document(make_spaced_events([0.2, 0.9, 0.4, 0.8, 0.3, 0.7]))

        result = postprocess.postprocess_beatmap(
            document,
            min_gap_seconds=0.1,
            combat_style="bursty",
        )

        self.assertEqual(set(actions(result)), {"Guard", "Strike"})


def make_document(events):
    return postprocess.BeatmapDocument(
        path=Path("memory.json"),
        display_name="Memory Beatmap",
        events=[
            postprocess.BeatmapEvent(
                event_id=event["eventId"],
                target_time_seconds=event["targetTimeSeconds"],
                action=event["action"],
                intensity=event["intensity"],
            )
            for event in events
        ],
    )


def make_event(event_id, target_time_seconds, action, intensity):
    return {
        "eventId": event_id,
        "targetTimeSeconds": target_time_seconds,
        "action": action,
        "intensity": intensity,
    }


def make_spaced_events(intensities):
    return [
        make_event("raw-" + str(index + 1).zfill(3), 1.0 + index * 0.5, "Guard", intensity)
        for index, intensity in enumerate(intensities)
    ]


def actions(result):
    return [event.action for event in result.output_events]


def action_counts(result):
    return postprocess.count_actions(result.output_events)


def write_input_beatmap(path, events):
    path.write_text(
        json.dumps(
            {
                "schemaVersion": 1,
                "displayName": "Input Beatmap",
                "globalOffsetSeconds": 0.0,
                "events": events,
            }
        ),
        encoding="utf-8",
    )


def run_postprocess_main(arguments):
    stdout = io.StringIO()
    stderr = io.StringIO()
    with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
        exit_code = postprocess.main(arguments)

    return exit_code, stdout.getvalue(), stderr.getvalue()


if __name__ == "__main__":
    unittest.main()
