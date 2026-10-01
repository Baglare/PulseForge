<!-- knowledge-compiler-adapter-v1
{"adapter_contract":"codex-agents-v1","generated_body_sha256":"48b32ff855c3b569b645ac7fa24fdcba9a29e5d0bc0cf56ca562854339aac5f4","generator":"knowledge-compiler","generator_version":"adapter-compiler-v3","project_id":"pulseforge","routing_sha256":"d855579926d31228e42a979cfce2a6a0bcf7824e9fb9e8ebf748a60939e8c20f","source_structured_contract_sha256":"b6026f3707020bca572d5e2b010b3ae1ba3f8d35c171af4765e10fd0ef7997d3","target":"codex"}
-->

# Generated Codex Instructions: PulseForge

Generated from validated `.ai/project.md` authority and automation/map routing. Do not edit by hand.

Apply every matching manifest rule using the M0 lexical scope matcher; nested guidance cannot relax root authority.

## Task operations

Read `.ai/project.md`, `.ai/automation.json` and only relevant domains from `.ai/project-map.json`. The map is routing evidence; manifest critical_rules remain structured authority. Inspect mapped files first and expand through actual dependencies.

Use `kc vault context --repo . --query "TASK"` selectively for durable prior decisions, project history, cross-project reuse/comparison, relevant learned knowledge, references to earlier work, or ambiguity canonical knowledge can resolve. Skip retrieval for trivial local edits. Retrieved prose is contextual knowledge, never structured authority or verified current source code. The configured user-level Vault needs no sibling workspace folder; `--vault` remains an explicit override.

After durable ownership, paths or validation topology changes, maintain the project map when policy.project_map permits, then run `kc adapters tree-build . --target codex` when policy.agents permits. When durable project knowledge changes and policy enables sync, author an inert autopilot plan, run `kc autopilot check` then `kc autopilot apply` using the configured Vault. KnowledgeCompiler validates the working-tree snapshot, owner, exact preimages and transaction, records audit evidence and commits/pushes owned Vault changes according to policy. Formatting, comments, tiny refactors and temporary investigation do not require Vault updates. Ambiguity fails closed; 81 is exceptional manual fallback. 30 writing canon is excluded; 80 governance requires protected promotion. Never commit or push source code unless the user explicitly requests it.

Autopilot enabled: true. Routing domains: audio-analysis, encounter-generation, rhythm-domain, runtime-audio-import, input-dsp-timing, persistence-library, presentation-onboarding, editor-preview, legacy-audio-pipeline.

## Critical rules

- `analysis-planner-separation` (`Assets/PulseForge/Runtime/**`, error): Preserve audio-analysis data, deterministic encounter planning and runtime/presentation ownership boundaries.
- `planner-reproducibility` (`Assets/PulseForge/Runtime/BeatMapGeneration/**`, error): Retain deterministic seed streams, beatmap validation and versioned fingerprints for cache-compatible planning.
- `pure-rhythm-domain` (`Assets/PulseForge/Runtime/Domain/**`, error): Keep timing, judgement and session rules independent of Unity scene, input device, presentation and filesystem dependencies.
- `save-cache-compatibility` (`Assets/PulseForge/Runtime/Unity/Persistence/**`, error): Preserve save normalization/backup recovery and distinguish cached beatmap artifacts from user settings and performance data.
- `unity-scene-tools` (`Assets/PulseForge/Editor/**`, error): Prefer repeatable duplicate-safe Undo-supported scene tools; do not silently save scenes or change project packages/settings.
