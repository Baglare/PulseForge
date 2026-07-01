import contextlib
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


ANALYZER_DIR = Path(__file__).resolve().parents[1]
if str(ANALYZER_DIR) not in sys.path:
    sys.path.insert(0, str(ANALYZER_DIR))

import generate_debug_click_track as click_generator
import generate_style_variants as variants


class GenerateStyleVariantsTests(unittest.TestCase):
    def test_wav_input_generates_raw_and_playable_variants(self):
        with variant_workspace() as workspace:
            result = variants.generate_style_variants(
                input_wav=workspace.wav_path,
                output_dir=workspace.output_dir,
                diagnostics_dir=workspace.diagnostics_dir,
                name="Debug",
                difficulty="hard",
            )

            self.assertTrue(result.raw_beatmap_path.exists())
            for style_label in ("Balanced", "Defensive", "Aggressive", "Bursty"):
                self.assertTrue((workspace.output_dir / ("BM_Playable_Debug_" + style_label + ".json")).exists())

    def test_input_raw_json_skips_analyzer(self):
        with variant_workspace() as workspace:
            raw_path = workspace.temp_path / "BM_Raw_Source.json"
            write_raw_beatmap(raw_path)

            with mock.patch.object(variants.pulseforge_audio_analyzer, "analyze_wav") as analyze_wav:
                analyze_wav.side_effect = AssertionError("analyzer should not run")
                result = variants.generate_style_variants(
                    input_raw_json=raw_path,
                    output_dir=workspace.output_dir,
                    diagnostics_dir=workspace.diagnostics_dir,
                    name="RawSource",
                    difficulty="hard",
                )

            self.assertEqual(result.raw_beatmap_path, raw_path)
            self.assertFalse(analyze_wav.called)
            self.assertTrue((workspace.output_dir / "BM_Playable_RawSource_Balanced.json").exists())

    def test_output_json_schema_version_is_one(self):
        with variant_workspace() as workspace:
            variants.generate_style_variants(
                input_raw_json=workspace.raw_json_path,
                output_dir=workspace.output_dir,
                diagnostics_dir=workspace.diagnostics_dir,
                name="Schema",
                difficulty="hard",
            )

            document = read_json(workspace.output_dir / "BM_Playable_Schema_Balanced.json")
            self.assertEqual(document["schemaVersion"], 1)

    def test_defensive_output_prefers_guard(self):
        with variant_workspace() as workspace:
            variants.generate_style_variants(
                input_raw_json=workspace.raw_json_path,
                output_dir=workspace.output_dir,
                diagnostics_dir=workspace.diagnostics_dir,
                name="Defensive",
                difficulty="hard",
                styles="defensive",
            )

            counts = count_actions(read_json(workspace.output_dir / "BM_Playable_Defensive_Defensive.json"))
            self.assertGreater(counts["Guard"], counts["Strike"])

    def test_aggressive_output_prefers_strike(self):
        with variant_workspace() as workspace:
            variants.generate_style_variants(
                input_raw_json=workspace.raw_json_path,
                output_dir=workspace.output_dir,
                diagnostics_dir=workspace.diagnostics_dir,
                name="Aggressive",
                difficulty="hard",
                styles="aggressive",
            )

            counts = count_actions(read_json(workspace.output_dir / "BM_Playable_Aggressive_Aggressive.json"))
            self.assertGreater(counts["Strike"], counts["Guard"])

    def test_expected_json_creates_compare_reports(self):
        with variant_workspace() as workspace:
            expected_path = workspace.temp_path / "expected.json"
            write_expected_beatmap(expected_path)

            variants.generate_style_variants(
                input_raw_json=workspace.raw_json_path,
                output_dir=workspace.output_dir,
                diagnostics_dir=workspace.diagnostics_dir,
                name="Compare",
                difficulty="hard",
                expected_json=expected_path,
            )

            for style in ("balanced", "defensive", "aggressive", "bursty"):
                self.assertTrue((workspace.diagnostics_dir / ("Compare_" + style + "_compare_report.json")).exists())

    def test_input_wav_and_raw_json_together_raise_error(self):
        with variant_workspace() as workspace:
            with self.assertRaisesRegex(variants.StyleVariantError, "exactly one input"):
                variants.generate_style_variants(
                    input_wav=workspace.wav_path,
                    input_raw_json=workspace.raw_json_path,
                    output_dir=workspace.output_dir,
                )

    def test_missing_input_raises_error(self):
        with variant_workspace() as workspace:
            with self.assertRaisesRegex(variants.StyleVariantError, "exactly one input"):
                variants.generate_style_variants(output_dir=workspace.output_dir)

    def test_invalid_style_raises_error(self):
        with variant_workspace() as workspace:
            with self.assertRaisesRegex(variants.StyleVariantError, "Unsupported style"):
                variants.generate_style_variants(
                    input_raw_json=workspace.raw_json_path,
                    output_dir=workspace.output_dir,
                    styles="balanced,invalid",
                )

    def test_summary_mode_outputs_paths(self):
        with variant_workspace() as workspace:
            exit_code, stdout, stderr = run_variants_main(
                [
                    "--input-raw-json",
                    str(workspace.raw_json_path),
                    "--output-dir",
                    str(workspace.output_dir),
                    "--name",
                    "Summary",
                    "--difficulty",
                    "hard",
                    "--styles",
                    "balanced",
                    "--summary",
                ]
            )

            self.assertEqual(exit_code, 0)
            self.assertEqual(stderr, "")
            self.assertIn("PulseForge style variants", stdout)
            self.assertIn("playableOutput:", stdout)


class variant_workspace:
    def __init__(self):
        self.temp_directory = None
        self.temp_path = None
        self.wav_path = None
        self.raw_json_path = None
        self.output_dir = None
        self.diagnostics_dir = None

    def __enter__(self):
        self.temp_directory = tempfile.TemporaryDirectory()
        self.temp_path = Path(self.temp_directory.name)
        self.wav_path = self.temp_path / "clicks.wav"
        self.raw_json_path = self.temp_path / "BM_Raw_Input.json"
        self.output_dir = self.temp_path / "beatmaps"
        self.diagnostics_dir = self.temp_path / "out"
        click_generator.write_debug_click_track(
            self.wav_path,
            [0.10, 0.40, 0.80, 1.20],
            sample_rate=8000,
            duration_seconds=1.8,
        )
        write_raw_beatmap(self.raw_json_path)
        return self

    def __exit__(self, exc_type, exc_value, traceback):
        self.temp_directory.cleanup()


def write_raw_beatmap(path):
    path.write_text(
        json.dumps(
            {
                "schemaVersion": 1,
                "displayName": "Raw Input",
                "globalOffsetSeconds": 0.0,
                "events": [
                    make_event("raw-001", 0.10, "Guard", 1.0),
                    make_event("raw-002", 0.40, "Guard", 1.0),
                    make_event("raw-003", 0.80, "Guard", 1.0),
                    make_event("raw-004", 1.20, "Guard", 1.0),
                ],
            }
        ),
        encoding="utf-8",
    )


def write_expected_beatmap(path):
    path.write_text(
        json.dumps(
            {
                "schemaVersion": 1,
                "displayName": "Expected",
                "globalOffsetSeconds": 0.0,
                "events": [
                    make_event("event-001", 0.10, "Guard", 1.0),
                    make_event("event-002", 0.40, "Guard", 1.0),
                    make_event("event-003", 0.80, "Strike", 1.0),
                    make_event("event-004", 1.20, "Guard", 1.0),
                ],
            }
        ),
        encoding="utf-8",
    )


def make_event(event_id, target_time_seconds, action, intensity):
    return {
        "eventId": event_id,
        "targetTimeSeconds": target_time_seconds,
        "action": action,
        "intensity": intensity,
    }


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


def count_actions(document):
    counts = {"Guard": 0, "Strike": 0}
    for event in document["events"]:
        counts[event["action"]] = counts.get(event["action"], 0) + 1

    return counts


def run_variants_main(arguments):
    stdout = io.StringIO()
    stderr = io.StringIO()
    with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
        exit_code = variants.main(arguments)

    return exit_code, stdout.getvalue(), stderr.getvalue()


if __name__ == "__main__":
    unittest.main()
