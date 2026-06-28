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

import generate_debug_click_track as click_generator
import run_debug_pipeline as pipeline


class RunDebugPipelineTests(unittest.TestCase):
    def test_pipeline_creates_raw_json(self):
        with pipeline_workspace([0.10, 0.30, 0.70]) as workspace:
            result = pipeline.run_pipeline(
                input_wav=workspace.wav_path,
                output_dir=workspace.output_dir,
                diagnostics_dir=workspace.diagnostics_dir,
                name="Test",
                pattern="Guard,Strike,Guard",
                difficulty="hard",
            )

            self.assertTrue(result.raw_beatmap_path.exists())

    def test_pipeline_creates_playable_json(self):
        with pipeline_workspace([0.10, 0.30, 0.70]) as workspace:
            result = pipeline.run_pipeline(
                input_wav=workspace.wav_path,
                output_dir=workspace.output_dir,
                diagnostics_dir=workspace.diagnostics_dir,
                name="Test",
                pattern="Guard,Strike,Guard",
                difficulty="hard",
            )

            self.assertTrue(result.playable_beatmap_path.exists())

    def test_playable_json_contains_schema_version_one(self):
        with pipeline_workspace([0.10, 0.30, 0.70]) as workspace:
            result = pipeline.run_pipeline(
                input_wav=workspace.wav_path,
                output_dir=workspace.output_dir,
                diagnostics_dir=workspace.diagnostics_dir,
                name="Test",
                pattern="Guard,Strike,Guard",
                difficulty="hard",
            )

            document = json.loads(result.playable_beatmap_path.read_text(encoding="utf-8"))
            self.assertEqual(document["schemaVersion"], 1)

    def test_pipeline_with_expected_json_creates_compare_report(self):
        with pipeline_workspace([0.10, 0.30, 0.70]) as workspace:
            expected_path = workspace.temp_path / "expected.json"
            write_expected_beatmap(
                expected_path,
                [
                    make_event("event-001", 0.10, "Guard"),
                    make_event("event-002", 0.30, "Strike"),
                    make_event("event-003", 0.70, "Guard"),
                ],
            )

            result = pipeline.run_pipeline(
                input_wav=workspace.wav_path,
                output_dir=workspace.output_dir,
                diagnostics_dir=workspace.diagnostics_dir,
                name="Test",
                pattern="Guard,Strike,Guard",
                difficulty="hard",
                expected_json=expected_path,
            )

            self.assertIsNotNone(result.compare_report_path)
            self.assertTrue(result.compare_report_path.exists())

    def test_pipeline_with_debug_csv_creates_csv(self):
        with pipeline_workspace([0.10, 0.30, 0.70]) as workspace:
            result = pipeline.run_pipeline(
                input_wav=workspace.wav_path,
                output_dir=workspace.output_dir,
                diagnostics_dir=workspace.diagnostics_dir,
                name="Test",
                pattern="Guard,Strike,Guard",
                difficulty="hard",
                write_debug_csv=True,
            )

            self.assertIsNotNone(result.debug_csv_path)
            self.assertTrue(result.debug_csv_path.exists())

    def test_pattern_controls_output_action_sequence(self):
        with pipeline_workspace([0.10, 0.30, 0.70]) as workspace:
            result = pipeline.run_pipeline(
                input_wav=workspace.wav_path,
                output_dir=workspace.output_dir,
                diagnostics_dir=workspace.diagnostics_dir,
                name="Test",
                pattern="Guard,Strike",
                difficulty="hard",
            )

            document = json.loads(result.playable_beatmap_path.read_text(encoding="utf-8"))
            actions = [event["action"] for event in document["events"]]
            self.assertEqual(actions, ["Guard", "Strike", "Guard"])

    def test_hard_difficulty_can_keep_more_fast_clicks_than_normal(self):
        click_times = [0.10, 0.30, 0.50]
        with pipeline_workspace(click_times) as normal_workspace:
            normal_result = pipeline.run_pipeline(
                input_wav=normal_workspace.wav_path,
                output_dir=normal_workspace.output_dir,
                diagnostics_dir=normal_workspace.diagnostics_dir,
                name="Normal",
                pattern="Guard,Strike,Guard",
                difficulty="normal",
            )

        with pipeline_workspace(click_times) as hard_workspace:
            hard_result = pipeline.run_pipeline(
                input_wav=hard_workspace.wav_path,
                output_dir=hard_workspace.output_dir,
                diagnostics_dir=hard_workspace.diagnostics_dir,
                name="Hard",
                pattern="Guard,Strike,Guard",
                difficulty="hard",
            )

        self.assertGreaterEqual(hard_result.playable_event_count, normal_result.playable_event_count)
        self.assertGreater(hard_result.playable_event_count, normal_result.playable_event_count)

    def test_invalid_input_path_returns_non_zero(self):
        with tempfile.TemporaryDirectory() as temp_directory:
            exit_code, _, stderr = run_pipeline_main(
                [
                    "--input-wav",
                    str(Path(temp_directory) / "missing.wav"),
                    "--output-dir",
                    str(Path(temp_directory) / "beatmaps"),
                ]
            )

            self.assertEqual(exit_code, 1)
            self.assertIn("Input WAV file does not exist", stderr)


class pipeline_workspace:
    def __init__(self, click_times):
        self.click_times = click_times
        self.temp_directory = None
        self.temp_path = None
        self.wav_path = None
        self.output_dir = None
        self.diagnostics_dir = None

    def __enter__(self):
        self.temp_directory = tempfile.TemporaryDirectory()
        self.temp_path = Path(self.temp_directory.name)
        self.wav_path = self.temp_path / "clicks.wav"
        self.output_dir = self.temp_path / "beatmaps"
        self.diagnostics_dir = self.temp_path / "out"
        click_generator.write_debug_click_track(
            self.wav_path,
            self.click_times,
            sample_rate=8000,
            duration_seconds=max(self.click_times) + 0.5,
        )
        return self

    def __exit__(self, exc_type, exc_value, traceback):
        self.temp_directory.cleanup()


def make_event(event_id, target_time_seconds, action):
    return {
        "eventId": event_id,
        "targetTimeSeconds": target_time_seconds,
        "action": action,
        "intensity": 1.0,
    }


def write_expected_beatmap(path, events):
    path.write_text(
        json.dumps(
            {
                "schemaVersion": 1,
                "displayName": "Expected Beatmap",
                "globalOffsetSeconds": 0.0,
                "events": events,
            }
        ),
        encoding="utf-8",
    )


def run_pipeline_main(arguments):
    stdout = io.StringIO()
    stderr = io.StringIO()
    with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
        exit_code = pipeline.main(arguments)

    return exit_code, stdout.getvalue(), stderr.getvalue()


if __name__ == "__main__":
    unittest.main()
