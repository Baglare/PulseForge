import contextlib
import csv
import io
import json
import struct
import sys
import tempfile
import unittest
import wave
from pathlib import Path


ANALYZER_DIR = Path(__file__).resolve().parents[1]
if str(ANALYZER_DIR) not in sys.path:
    sys.path.insert(0, str(ANALYZER_DIR))

import pulseforge_audio_analyzer as analyzer
import generate_debug_click_track as click_generator


class PulseForgeAudioAnalyzerTests(unittest.TestCase):
    def test_action_pattern_parse_accepts_guard_and_strike_case_insensitive(self):
        pattern = analyzer.parse_action_pattern("guard, STRIKE,Guard")

        self.assertEqual(pattern, ["Guard", "Strike", "Guard"])

    def test_action_pattern_parse_rejects_unknown_action(self):
        with self.assertRaises(analyzer.AnalyzerError):
            analyzer.parse_action_pattern("Guard,Dodge")

    def test_event_id_format_is_deterministic(self):
        self.assertEqual(analyzer.format_event_id(0), "event-001")
        self.assertEqual(analyzer.format_event_id(9), "event-010")
        self.assertEqual(analyzer.format_event_id(123), "event-124")

    def test_simple_click_track_detects_expected_peak_count(self):
        with temporary_click_wav([0.10, 0.30, 0.70]) as wav_path:
            beatmap = analyzer.analyze_wav_file(
                wav_path,
                display_name="Synthetic Clicks",
                frame_ms=10,
                threshold_ratio=0.5,
                min_gap_seconds=0.05,
                pattern_text="Guard,Strike",
            )

        self.assertEqual(len(beatmap["events"]), 3)
        self.assertAlmostEqual(beatmap["events"][0]["targetTimeSeconds"], 0.10, places=2)
        self.assertAlmostEqual(beatmap["events"][1]["targetTimeSeconds"], 0.30, places=2)
        self.assertAlmostEqual(beatmap["events"][2]["targetTimeSeconds"], 0.70, places=2)

    def test_close_clicks_inside_min_gap_collapse_to_one_event(self):
        with temporary_click_wav([0.10, 0.12]) as wav_path:
            beatmap = analyzer.analyze_wav_file(
                wav_path,
                frame_ms=10,
                threshold_ratio=0.5,
                min_gap_seconds=0.05,
            )

        self.assertEqual(len(beatmap["events"]), 1)

    def test_output_json_contains_schema_version_one(self):
        document = analyzer.build_beatmap_document(
            [analyzer.DetectedPeak(time_seconds=1.0, intensity=0.75)],
            "Test Map",
            0.0,
            ["Guard"],
        )
        parsed = json.loads(analyzer.dumps_beatmap(document))

        self.assertEqual(parsed["schemaVersion"], 1)

    def test_output_events_contain_target_time_action_and_intensity(self):
        document = analyzer.build_beatmap_document(
            [analyzer.DetectedPeak(time_seconds=1.25, intensity=0.5)],
            "Test Map",
            0.0,
            ["Strike"],
        )
        event = document["events"][0]

        self.assertIn("targetTimeSeconds", event)
        self.assertEqual(event["action"], "Strike")
        self.assertIn("intensity", event)

    def test_intensity_is_clamped_between_zero_and_one(self):
        document = analyzer.build_beatmap_document(
            [
                analyzer.DetectedPeak(time_seconds=1.0, intensity=-1.0),
                analyzer.DetectedPeak(time_seconds=2.0, intensity=2.0),
            ],
            "Test Map",
            0.0,
            ["Guard"],
        )

        intensities = [event["intensity"] for event in document["events"]]
        self.assertEqual(intensities, [0.0, 1.0])

    def test_silent_wav_returns_empty_events(self):
        with temporary_click_wav([]) as wav_path:
            beatmap = analyzer.analyze_wav_file(
                wav_path,
                frame_ms=10,
                threshold_ratio=0.5,
                min_gap_seconds=0.05,
            )

        self.assertEqual(beatmap["events"], [])

    def test_invalid_input_path_raises_user_facing_error(self):
        with self.assertRaises(analyzer.AnalyzerError):
            analyzer.analyze_wav_file("missing-input.wav")

    def test_generator_parse_click_times_accepts_valid_values(self):
        click_times = click_generator.parse_click_times("1.00, 1.50,2.25")

        self.assertEqual(click_times, [1.0, 1.5, 2.25])

    def test_generator_parse_click_times_rejects_negative_values(self):
        with self.assertRaises(click_generator.GeneratorError):
            click_generator.parse_click_times("0.5,-1.0")

    def test_generator_writes_valid_wav_to_tempfile(self):
        with tempfile.TemporaryDirectory() as temp_directory:
            wav_path = Path(temp_directory) / "generated.wav"

            click_generator.write_debug_click_track(
                wav_path,
                [0.10, 0.30],
                sample_rate=8000,
                duration_seconds=0.60,
            )

            self.assertTrue(wav_path.exists())
            self.assertGreater(wav_path.stat().st_size, 44)

    def test_generated_wav_can_be_opened_by_wave_module(self):
        with tempfile.TemporaryDirectory() as temp_directory:
            wav_path = Path(temp_directory) / "generated.wav"
            click_generator.write_debug_click_track(
                wav_path,
                [0.10],
                sample_rate=8000,
                duration_seconds=0.50,
                channels=1,
            )

            with wave.open(str(wav_path), "rb") as wav_file:
                self.assertEqual(wav_file.getnchannels(), 1)
                self.assertEqual(wav_file.getsampwidth(), 2)
                self.assertEqual(wav_file.getframerate(), 8000)

    def test_generated_wav_analyzes_to_expected_event_count(self):
        with tempfile.TemporaryDirectory() as temp_directory:
            wav_path = Path(temp_directory) / "generated.wav"
            click_generator.write_debug_click_track(
                wav_path,
                [0.10, 0.30, 0.70],
                sample_rate=8000,
                duration_seconds=1.0,
            )

            beatmap = analyzer.analyze_wav_file(
                wav_path,
                frame_ms=10,
                threshold_ratio=0.35,
                min_gap_seconds=0.10,
            )

            self.assertEqual(len(beatmap["events"]), 3)

    def test_detection_mode_amplitude_preserves_generated_click_count(self):
        with tempfile.TemporaryDirectory() as temp_directory:
            wav_path = Path(temp_directory) / "generated.wav"
            click_generator.write_debug_click_track(
                wav_path,
                [0.10, 0.30, 0.70],
                sample_rate=8000,
                duration_seconds=1.0,
            )

            beatmap = analyzer.analyze_wav_file(
                wav_path,
                frame_ms=10,
                threshold_ratio=0.35,
                min_gap_seconds=0.10,
                detection_mode="amplitude",
            )

            self.assertEqual(len(beatmap["events"]), 3)

    def test_detection_mode_onset_detects_generated_click_count(self):
        with tempfile.TemporaryDirectory() as temp_directory:
            wav_path = Path(temp_directory) / "generated.wav"
            click_generator.write_debug_click_track(
                wav_path,
                [0.10, 0.30, 0.70],
                sample_rate=8000,
                duration_seconds=1.0,
            )

            beatmap = analyzer.analyze_wav_file(
                wav_path,
                frame_ms=10,
                threshold_ratio=0.35,
                min_gap_seconds=0.10,
                detection_mode="onset",
            )

            self.assertEqual(len(beatmap["events"]), 3)

    def test_generated_wav_analyzes_to_approximate_click_times(self):
        expected_times = [0.10, 0.30, 0.70]
        with tempfile.TemporaryDirectory() as temp_directory:
            wav_path = Path(temp_directory) / "generated.wav"
            click_generator.write_debug_click_track(
                wav_path,
                expected_times,
                sample_rate=8000,
                duration_seconds=1.0,
            )

            beatmap = analyzer.analyze_wav_file(
                wav_path,
                frame_ms=10,
                threshold_ratio=0.35,
                min_gap_seconds=0.10,
            )

            actual_times = [event["targetTimeSeconds"] for event in beatmap["events"]]
            self.assertEqual(len(actual_times), len(expected_times))
            for actual_time, expected_time in zip(actual_times, expected_times):
                self.assertAlmostEqual(actual_time, expected_time, delta=0.04)

    def test_detection_mode_onset_detects_approximate_click_times(self):
        expected_times = [0.10, 0.30, 0.70]
        with tempfile.TemporaryDirectory() as temp_directory:
            wav_path = Path(temp_directory) / "generated.wav"
            click_generator.write_debug_click_track(
                wav_path,
                expected_times,
                sample_rate=8000,
                duration_seconds=1.0,
            )

            beatmap = analyzer.analyze_wav_file(
                wav_path,
                frame_ms=10,
                threshold_ratio=0.35,
                min_gap_seconds=0.10,
                detection_mode="onset",
            )

            actual_times = [event["targetTimeSeconds"] for event in beatmap["events"]]
            self.assertEqual(len(actual_times), len(expected_times))
            for actual_time, expected_time in zip(actual_times, expected_times):
                self.assertAlmostEqual(actual_time, expected_time, delta=0.04)

    def test_invalid_baseline_ms_raises_user_facing_error(self):
        with self.assertRaises(analyzer.AnalyzerError):
            analyzer.analyze_wav_file(
                "missing-input.wav",
                detection_mode="onset",
                baseline_ms=0,
            )

    def test_negative_onset_smooth_frames_raises_user_facing_error(self):
        with self.assertRaises(analyzer.AnalyzerError):
            analyzer.analyze_wav_file(
                "missing-input.wav",
                detection_mode="onset",
                onset_smooth_frames=-1,
            )

    def test_report_output_file_is_created(self):
        with tempfile.TemporaryDirectory() as temp_directory:
            report_path = Path(temp_directory) / "reports" / "analysis.json"
            beatmap_path = Path(temp_directory) / "beatmap.json"
            with temporary_click_wav([0.10, 0.30]) as wav_path:
                exit_code, _, _ = run_analyzer_main(
                    [
                        str(wav_path),
                        "--output",
                        str(beatmap_path),
                        "--report-output",
                        str(report_path),
                        "--frame-ms",
                        "10",
                        "--threshold-ratio",
                        "0.5",
                        "--min-gap-seconds",
                        "0.05",
                    ]
                )

            self.assertEqual(exit_code, 0)
            self.assertTrue(report_path.exists())

    def test_report_output_contains_detected_event_count(self):
        with tempfile.TemporaryDirectory() as temp_directory:
            report_path = Path(temp_directory) / "analysis.json"
            beatmap_path = Path(temp_directory) / "beatmap.json"
            with temporary_click_wav([0.10, 0.30, 0.70]) as wav_path:
                exit_code, _, _ = run_analyzer_main(
                    [
                        str(wav_path),
                        "--output",
                        str(beatmap_path),
                        "--report-output",
                        str(report_path),
                        "--frame-ms",
                        "10",
                        "--threshold-ratio",
                        "0.5",
                        "--min-gap-seconds",
                        "0.05",
                    ]
                )

            report = json.loads(report_path.read_text(encoding="utf-8"))
            self.assertEqual(exit_code, 0)
            self.assertEqual(report["detectedEventCount"], 3)

    def test_report_output_contains_sample_rate_and_duration(self):
        with tempfile.TemporaryDirectory() as temp_directory:
            report_path = Path(temp_directory) / "analysis.json"
            beatmap_path = Path(temp_directory) / "beatmap.json"
            with temporary_click_wav([0.10], sample_rate=1000, duration_seconds=1.0) as wav_path:
                exit_code, _, _ = run_analyzer_main(
                    [
                        str(wav_path),
                        "--output",
                        str(beatmap_path),
                        "--report-output",
                        str(report_path),
                        "--frame-ms",
                        "10",
                    ]
                )

            report = json.loads(report_path.read_text(encoding="utf-8"))
            self.assertEqual(exit_code, 0)
            self.assertEqual(report["sampleRate"], 1000)
            self.assertAlmostEqual(report["durationSeconds"], 1.0)

    def test_report_output_contains_detection_mode(self):
        with tempfile.TemporaryDirectory() as temp_directory:
            report_path = Path(temp_directory) / "analysis.json"
            beatmap_path = Path(temp_directory) / "beatmap.json"
            with temporary_click_wav([0.10]) as wav_path:
                exit_code, _, _ = run_analyzer_main(
                    [
                        str(wav_path),
                        "--output",
                        str(beatmap_path),
                        "--report-output",
                        str(report_path),
                        "--detection-mode",
                        "onset",
                    ]
                )

            report = json.loads(report_path.read_text(encoding="utf-8"))
            self.assertEqual(exit_code, 0)
            self.assertEqual(report["detectionMode"], "onset")

    def test_report_output_contains_detection_curve_max(self):
        with tempfile.TemporaryDirectory() as temp_directory:
            report_path = Path(temp_directory) / "analysis.json"
            beatmap_path = Path(temp_directory) / "beatmap.json"
            with temporary_click_wav([0.10]) as wav_path:
                exit_code, _, _ = run_analyzer_main(
                    [
                        str(wav_path),
                        "--output",
                        str(beatmap_path),
                        "--report-output",
                        str(report_path),
                    ]
                )

            report = json.loads(report_path.read_text(encoding="utf-8"))
            self.assertEqual(exit_code, 0)
            self.assertIn("detectionCurveMax", report)
            self.assertGreater(report["detectionCurveMax"], 0)

    def test_debug_csv_output_file_is_created(self):
        with tempfile.TemporaryDirectory() as temp_directory:
            csv_path = Path(temp_directory) / "debug" / "frames.csv"
            beatmap_path = Path(temp_directory) / "beatmap.json"
            with temporary_click_wav([0.10, 0.30]) as wav_path:
                exit_code, _, _ = run_analyzer_main(
                    [
                        str(wav_path),
                        "--output",
                        str(beatmap_path),
                        "--debug-csv-output",
                        str(csv_path),
                        "--frame-ms",
                        "10",
                        "--threshold-ratio",
                        "0.5",
                        "--min-gap-seconds",
                        "0.05",
                    ]
                )

            self.assertEqual(exit_code, 0)
            self.assertTrue(csv_path.exists())

    def test_debug_csv_header_contains_expected_columns(self):
        with tempfile.TemporaryDirectory() as temp_directory:
            csv_path = Path(temp_directory) / "frames.csv"
            beatmap_path = Path(temp_directory) / "beatmap.json"
            with temporary_click_wav([0.10]) as wav_path:
                run_analyzer_main(
                    [
                        str(wav_path),
                        "--output",
                        str(beatmap_path),
                        "--debug-csv-output",
                        str(csv_path),
                        "--frame-ms",
                        "10",
                    ]
                )

            header = csv_path.read_text(encoding="utf-8").splitlines()[0]
            self.assertEqual(
                header,
                "frameIndex,timeSeconds,amplitude,onsetStrength,detectionValue,isLocalPeak,isSelectedPeak",
            )

    def test_debug_csv_marks_at_least_one_selected_peak(self):
        with tempfile.TemporaryDirectory() as temp_directory:
            csv_path = Path(temp_directory) / "frames.csv"
            beatmap_path = Path(temp_directory) / "beatmap.json"
            with temporary_click_wav([0.10, 0.30]) as wav_path:
                exit_code, _, _ = run_analyzer_main(
                    [
                        str(wav_path),
                        "--output",
                        str(beatmap_path),
                        "--debug-csv-output",
                        str(csv_path),
                        "--frame-ms",
                        "10",
                        "--threshold-ratio",
                        "0.5",
                        "--min-gap-seconds",
                        "0.05",
                    ]
                )

            rows = list(csv.DictReader(io.StringIO(csv_path.read_text(encoding="utf-8"))))
            self.assertEqual(exit_code, 0)
            self.assertTrue(any(row["isSelectedPeak"] == "true" for row in rows))

    def test_cli_without_output_keeps_stdout_beatmap_json(self):
        with temporary_click_wav([0.10, 0.30]) as wav_path:
            exit_code, stdout, stderr = run_analyzer_main(
                [
                    str(wav_path),
                    "--frame-ms",
                    "10",
                    "--threshold-ratio",
                    "0.5",
                    "--min-gap-seconds",
                    "0.05",
                ]
            )

        document = json.loads(stdout)
        self.assertEqual(exit_code, 0)
        self.assertEqual(stderr, "")
        self.assertEqual(document["schemaVersion"], 1)
        self.assertEqual(len(document["events"]), 2)

    def test_summary_writes_to_stderr_without_polluting_stdout_json(self):
        with temporary_click_wav([0.10]) as wav_path:
            exit_code, stdout, stderr = run_analyzer_main(
                [
                    str(wav_path),
                    "--summary",
                    "--frame-ms",
                    "10",
                    "--threshold-ratio",
                    "0.5",
                    "--min-gap-seconds",
                    "0.05",
                ]
            )

        document = json.loads(stdout)
        self.assertEqual(exit_code, 0)
        self.assertEqual(document["schemaVersion"], 1)
        self.assertIn("Detected 1 events", stderr)

    def test_amplitude_mode_keeps_schema_version_one(self):
        with temporary_click_wav([0.10]) as wav_path:
            beatmap = analyzer.analyze_wav_file(
                wav_path,
                detection_mode="amplitude",
            )

        self.assertEqual(beatmap["schemaVersion"], 1)

    def test_onset_mode_keeps_schema_version_one(self):
        with temporary_click_wav([0.10]) as wav_path:
            beatmap = analyzer.analyze_wav_file(
                wav_path,
                detection_mode="onset",
            )

        self.assertEqual(beatmap["schemaVersion"], 1)


class temporary_click_wav:
    def __init__(self, click_times, sample_rate=1000, duration_seconds=1.0):
        self.click_times = click_times
        self.sample_rate = sample_rate
        self.duration_seconds = duration_seconds
        self.temp_directory = None
        self.path = None

    def __enter__(self):
        self.temp_directory = tempfile.TemporaryDirectory()
        self.path = Path(self.temp_directory.name) / "clicks.wav"
        write_click_wav(self.path, self.click_times, self.sample_rate, self.duration_seconds)
        return self.path

    def __exit__(self, exc_type, exc_value, traceback):
        self.temp_directory.cleanup()


def write_click_wav(path, click_times, sample_rate, duration_seconds):
    sample_count = int(sample_rate * duration_seconds)
    samples = [0] * sample_count

    for click_time in click_times:
        start_index = int(round(click_time * sample_rate))
        for offset in range(3):
            sample_index = start_index + offset
            if 0 <= sample_index < sample_count:
                samples[sample_index] = 24000

    with wave.open(str(path), "wb") as wav_file:
        wav_file.setnchannels(1)
        wav_file.setsampwidth(2)
        wav_file.setframerate(sample_rate)
        wav_file.writeframes(b"".join(struct.pack("<h", sample) for sample in samples))


def run_analyzer_main(arguments):
    stdout = io.StringIO()
    stderr = io.StringIO()
    with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
        exit_code = analyzer.main(arguments)

    return exit_code, stdout.getvalue(), stderr.getvalue()


if __name__ == "__main__":
    unittest.main()
