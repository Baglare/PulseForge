---
{"critical_rules":[{"id":"analysis-planner-separation","kind":"invariant","scope":"Assets/PulseForge/Runtime/**","severity":"error","statement":"Preserve audio-analysis data, deterministic encounter planning and runtime/presentation ownership boundaries."},{"id":"planner-reproducibility","kind":"invariant","scope":"Assets/PulseForge/Runtime/BeatMapGeneration/**","severity":"error","statement":"Retain deterministic seed streams, beatmap validation and versioned fingerprints for cache-compatible planning."},{"id":"pure-rhythm-domain","kind":"invariant","scope":"Assets/PulseForge/Runtime/Domain/**","severity":"error","statement":"Keep timing, judgement and session rules independent of Unity scene, input device, presentation and filesystem dependencies."},{"id":"save-cache-compatibility","kind":"invariant","scope":"Assets/PulseForge/Runtime/Unity/Persistence/**","severity":"error","statement":"Preserve save normalization/backup recovery and distinguish cached beatmap artifacts from user settings and performance data."},{"id":"unity-scene-tools","kind":"invariant","scope":"Assets/PulseForge/Editor/**","severity":"error","statement":"Prefer repeatable duplicate-safe Undo-supported scene tools; do not silently save scenes or change project packages/settings."}],"manifest_version":1,"project_id":"pulseforge","project_name":"PulseForge","schema":"project-ai-manifest-v1"}
---
# Purpose

Unity rhythm-combat prototype transforming local audio into deterministic playable radial beatmaps and DSP-synchronized sessions.

# Repository Map

- audio-analysis: Assets/PulseForge/Runtime/AudioAnalysis
- encounter-generation: Assets/PulseForge/Runtime/BeatMapGeneration
- rhythm-domain: Assets/PulseForge/Runtime/Domain/Rhythm
- runtime-audio-import: Assets/PulseForge/Runtime/Unity/Audio, tools/runtime_audio
- input-dsp-timing: Assets/PulseForge/Runtime/Unity/Input, Assets/PulseForge/Runtime/Unity/Timing
- persistence-library: Assets/PulseForge/Runtime/Unity/Persistence
- presentation-onboarding: Assets/PulseForge/Runtime/Unity/UI, Assets/PulseForge/Runtime/Unity/Prototype, Assets/PulseForge/Runtime/Unity/Onboarding
- editor-preview: Assets/PulseForge/Editor
- legacy-audio-pipeline: tools/audio_analyzer

# Architecture

Assets/PulseForge/Runtime separates C# AudioAnalysis, BeatMapGeneration, pure Domain/Rhythm and Unity audio/input/timing/persistence/presentation. RadialAudioAnalyzerV2 feeds RadialEncounterPlanner, validation and fingerprints; radial domain sessions own timing and action resolution. Runtime audio import uses FFmpeg/file picker. Editor AudioPipeline retains Radial V2 and Legacy Python V1; tools/audio_analyzer owns the legacy offline pipeline.

# Validation Notes

Target pure changed logic and mapped EditMode or Python analyzer tests only when requested. Unity Editor/PlayMode/build and custom-song acceptance need explicit execution authorization. README/docs describe historical milestones; current code and existing tests determine implemented V2 capability.

# Sensitive Areas

Preserve deterministic seeds/fingerprints, DSP timing and compatible saved settings/library/cache. Do not hand-edit scene YAML or alter ProjectSettings/Packages as routine code work.

# Non-goals

No multiplayer, accounts, online leaderboard or perfect universal music choreography. Legacy Python and Radial V2 are distinct pipelines, not interchangeable outputs.
