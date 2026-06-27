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


if __name__ == "__main__":
    unittest.main()
