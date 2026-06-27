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

import compare_beatmaps as compare


class CompareBeatmapsTests(unittest.TestCase):
    def test_identical_beatmaps_have_zero_mean_and_max_error(self):
        expected = make_document([make_event("event-001", 1.0, "Guard")])
        actual = make_document([make_event("event-001", 1.0, "Guard")])

        report = compare.compare_beatmaps(expected, actual, tolerance_ms=40)

        summary = report["summary"]
        self.assertEqual(summary["meanSignedErrorMs"], 0.0)
        self.assertEqual(summary["meanAbsoluteErrorMs"], 0.0)
        self.assertEqual(summary["maxAbsoluteErrorMs"], 0.0)

    def test_actual_beatmap_late_by_10_ms_reports_positive_mean_error(self):
        expected = make_document(
            [
                make_event("event-001", 1.0, "Guard"),
                make_event("event-002", 2.0, "Strike"),
            ]
        )
        actual = make_document(
            [
                make_event("event-001", 1.01, "Guard"),
                make_event("event-002", 2.01, "Strike"),
            ]
        )

        report = compare.compare_beatmaps(expected, actual, tolerance_ms=40)

        self.assertAlmostEqual(report["summary"]["meanSignedErrorMs"], 10.0)

    def test_suggested_global_offset_negates_mean_signed_error(self):
        expected = make_document([make_event("event-001", 1.0, "Guard")])
        actual = make_document([make_event("event-001", 1.01, "Guard")])

        report = compare.compare_beatmaps(expected, actual, tolerance_ms=40)

        self.assertAlmostEqual(report["summary"]["suggestedGlobalOffsetSeconds"], -0.010)

    def test_action_mismatch_is_counted(self):
        expected = make_document([make_event("event-001", 1.0, "Guard")])
        actual = make_document([make_event("event-001", 1.0, "Strike")])

        report = compare.compare_beatmaps(expected, actual, tolerance_ms=40)

        self.assertEqual(report["summary"]["actionMismatchCount"], 1)
        self.assertFalse(report["comparisons"][0]["actionMatches"])

    def test_extra_actual_event_is_counted(self):
        expected = make_document([make_event("event-001", 1.0, "Guard")])
        actual = make_document(
            [
                make_event("event-001", 1.0, "Guard"),
                make_event("event-002", 2.0, "Strike"),
            ]
        )

        report = compare.compare_beatmaps(expected, actual, tolerance_ms=40)

        self.assertEqual(report["summary"]["extraEventCount"], 1)
        self.assertEqual(report["summary"]["missingEventCount"], 0)

    def test_missing_actual_event_is_counted(self):
        expected = make_document(
            [
                make_event("event-001", 1.0, "Guard"),
                make_event("event-002", 2.0, "Strike"),
            ]
        )
        actual = make_document([make_event("event-001", 1.0, "Guard")])

        report = compare.compare_beatmaps(expected, actual, tolerance_ms=40)

        self.assertEqual(report["summary"]["missingEventCount"], 1)
        self.assertEqual(report["summary"]["extraEventCount"], 0)

    def test_tolerance_inside_and_outside_counts_are_correct(self):
        expected = make_document(
            [
                make_event("event-001", 1.0, "Guard"),
                make_event("event-002", 2.0, "Strike"),
            ]
        )
        actual = make_document(
            [
                make_event("event-001", 1.02, "Guard"),
                make_event("event-002", 2.08, "Strike"),
            ]
        )

        report = compare.compare_beatmaps(expected, actual, tolerance_ms=40)

        self.assertEqual(report["summary"]["withinToleranceCount"], 1)
        self.assertEqual(report["summary"]["outsideToleranceCount"], 1)

    def test_report_json_file_is_created(self):
        with tempfile.TemporaryDirectory() as temp_directory:
            temp_path = Path(temp_directory)
            expected_path = temp_path / "expected.json"
            actual_path = temp_path / "actual.json"
            report_path = temp_path / "reports" / "compare.json"
            write_beatmap(expected_path, [make_event("event-001", 1.0, "Guard")])
            write_beatmap(actual_path, [make_event("event-001", 1.0, "Guard")])

            exit_code, _, _ = run_compare_main(
                [
                    str(expected_path),
                    str(actual_path),
                    "--report-output",
                    str(report_path),
                ]
            )

            self.assertEqual(exit_code, 0)
            self.assertTrue(report_path.exists())
            self.assertEqual(json.loads(report_path.read_text(encoding="utf-8"))["summary"]["comparedEventCount"], 1)

    def test_wrong_schema_version_raises_user_facing_error(self):
        with tempfile.TemporaryDirectory() as temp_directory:
            beatmap_path = Path(temp_directory) / "wrong_schema.json"
            beatmap_path.write_text(
                json.dumps({"schemaVersion": 2, "events": []}),
                encoding="utf-8",
            )

            with self.assertRaises(compare.CompareError):
                compare.load_beatmap(beatmap_path)

    def test_strict_returns_non_zero_for_tolerance_failure(self):
        with tempfile.TemporaryDirectory() as temp_directory:
            temp_path = Path(temp_directory)
            expected_path = temp_path / "expected.json"
            actual_path = temp_path / "actual.json"
            write_beatmap(expected_path, [make_event("event-001", 1.0, "Guard")])
            write_beatmap(actual_path, [make_event("event-001", 1.1, "Guard")])

            exit_code, _, _ = run_compare_main(
                [
                    str(expected_path),
                    str(actual_path),
                    "--tolerance-ms",
                    "40",
                    "--strict",
                ]
            )

            self.assertEqual(exit_code, 2)


def make_document(events):
    return compare.BeatmapDocument(
        path=Path("memory.json"),
        events=[
            compare.BeatmapEvent(
                event_id=event["eventId"],
                target_time_seconds=event["targetTimeSeconds"],
                action=event["action"],
            )
            for event in events
        ],
    )


def make_event(event_id, target_time_seconds, action):
    return {
        "eventId": event_id,
        "targetTimeSeconds": target_time_seconds,
        "action": action,
        "intensity": 1.0,
    }


def write_beatmap(path, events):
    path.write_text(
        json.dumps(
            {
                "schemaVersion": 1,
                "displayName": "Test Beatmap",
                "globalOffsetSeconds": 0.0,
                "events": events,
            }
        ),
        encoding="utf-8",
    )


def run_compare_main(arguments):
    stdout = io.StringIO()
    stderr = io.StringIO()
    with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
        exit_code = compare.main(arguments)

    return exit_code, stdout.getvalue(), stderr.getvalue()


if __name__ == "__main__":
    unittest.main()
