# Codex Prompt 01 — Rhythm Domain Foundation

Aşağıdaki promptu PulseForge Unity repository'sini Codex'e bağladıktan sonra tek görev olarak ver.

---

You are working in the PulseForge Unity repository. Read `AGENTS.md` and the files under `docs/` before making changes.

## Goal

Create the smallest testable rhythm-domain foundation that classifies a player's input timing against one fixed beat event as `Perfect`, `Good`, or `Miss`.

## Scope

You may create or modify files only under:

- `Assets/PulseForge/Runtime/Domain/Rhythm/`
- `Assets/PulseForge/Tests/EditMode/`

You may create the required assembly definition files in those two areas.

## Required production types

Use the namespace `PulseForge.Domain.Rhythm`.

1. `RhythmAction` enum
   - `Guard`
   - `Strike`

2. `HitGrade` enum
   - `Perfect`
   - `Good`
   - `Miss`

3. `BeatEventData`
   - Immutable after construction.
   - Properties:
     - `string EventId`
     - `double TargetTimeSeconds`
     - `RhythmAction Action`
     - `float Intensity`
   - Validation:
     - EventId must not be null, empty, or whitespace.
     - TargetTimeSeconds must be finite and greater than or equal to zero.
     - Intensity must be finite and in the inclusive range [0, 1].

4. `JudgementWindows`
   - Immutable after construction.
   - Properties:
     - `double PerfectWindowSeconds`
     - `double GoodWindowSeconds`
   - Validation:
     - Both values must be finite and greater than zero.
     - PerfectWindowSeconds must be less than or equal to GoodWindowSeconds.

5. `HitResult`
   - Immutable after construction.
   - Properties:
     - `string EventId`
     - `HitGrade Grade`
     - `double TimingErrorSeconds`
   - Timing error must be signed:
     - negative means early;
     - positive means late;
     - zero means exactly on time.

6. `HitJudge`
   - Pure C# class with no Unity API usage.
   - Provide a method equivalent to:
     - `HitResult Judge(double inputTimeSeconds, BeatEventData beatEvent, JudgementWindows windows)`
   - Validate null arguments and reject non-finite input time.
   - Calculate:
     - `error = inputTimeSeconds - beatEvent.TargetTimeSeconds`
     - `absoluteError = abs(error)`
   - Inclusive classification rules:
     - absoluteError <= perfect window: Perfect
     - otherwise absoluteError <= good window: Good
     - otherwise: Miss

## Required tests

Use Unity Edit Mode tests with NUnit. Include focused tests for at least:

- exact target time is Perfect;
- early input inside Perfect is Perfect;
- late input inside Perfect is Perfect;
- exact positive Perfect boundary is Perfect;
- exact negative Perfect boundary is Perfect;
- just outside Perfect but inside Good is Good;
- exact positive Good boundary is Good;
- exact negative Good boundary is Good;
- outside Good is Miss;
- timing error preserves early/late sign;
- invalid BeatEventData constructor values are rejected;
- invalid JudgementWindows constructor values are rejected;
- invalid input time is rejected.

Use a small explicit tolerance where floating-point comparison requires it. Avoid tests that depend on frame timing or the Unity scene.

## Architecture constraints

- Production domain assembly must not reference `UnityEngine`.
- None of these types may inherit from `MonoBehaviour` or `ScriptableObject`.
- Do not add `IHitJudge`, factories, dependency injection containers, event buses, managers, logging frameworks, JSON support, input handling, scoring, audio, scenes, prefabs, or UI.
- Do not modify packages or project settings.
- Do not implement future roadmap items.

## Completion report

When finished, provide:

1. A concise file list.
2. A short explanation of each type's responsibility.
3. Tests added.
4. Tests actually executed and exact result.
5. Anything you could not verify.

Do not claim that Unity tests passed if the environment could not run the Unity Test Framework.

---
