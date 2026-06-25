# PulseForge Agent Instructions

## Project baseline

- Target engine family: Unity 6.3 LTS.
- Language: C#.
- The project is a local-first 2D rhythm-combat application.
- Work in small, reviewable increments.

## Architecture rules

- Keep Domain logic independent from `UnityEngine` whenever possible.
- Do not put unrelated responsibilities into generic `Manager` classes.
- Do not introduce global singletons, service locators, or event buses unless a task explicitly requires and justifies them.
- Do not create an interface for a class merely for decoration. Add an interface only at a real boundary or when a test requires substitution.
- Separate immutable map/configuration data from mutable runtime session state.
- Use absolute song time for rhythm events, never chained relative delays.

## Change discipline

- Inspect the repository before editing.
- Modify only files required by the current task.
- Do not change `Packages/manifest.json`, `ProjectSettings`, scenes, prefabs, art, or audio unless the task explicitly requests it.
- Do not add third-party packages without explicit approval.
- Do not generate binary assets.
- Preserve existing naming and folder conventions.

## Testing

- Add focused tests for every domain behavior.
- Cover boundary values and invalid input, not only the happy path.
- Never claim that tests passed unless they were actually executed.
- If Unity tests cannot be executed in the environment, state this clearly and provide exact manual test instructions.

## Reporting

At the end of every task, report:

1. Files created or changed.
2. Key design decisions.
3. Tests added.
4. Tests actually run and their results.
5. Any assumptions, limitations, or follow-up risks.

## Current scope restriction

Until explicitly requested, do not implement audio analysis, Python integration, UI, animation, input handling, scoring, beatmap generation, or scene setup.
