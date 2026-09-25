# Autonomy rework validation — 2026-09-25

Environment: .NET SDK 10.0.100, Linux x64. Builds used one MSBuild worker and disabled shared compilation. No Windows desktop UI session was launched.

| Check | Result |
| --- | --- |
| Complete Core test suite | 1,031 passed; 0 failed; 0 skipped |
| App build and App test suite | Build succeeded; 196 passed; 0 failed; 0 skipped |
| Solution build after App configuration fix | Succeeded; 0 warnings; 0 errors |
| Combined automated tests | 1,227 passed |
| Save compatibility | No persisted field/schema change; score preview service is additive |

The supplied App project omitted `ImplicitUsings` while multiple source files relied on the generated imports. Enabling that standard project setting fixes clean compilation. This is the only App project change; no App UI or gameplay source was edited.

Focused tests cover male-line versus wider bloodline descendants; daughters-only families; single-heir redundancy; grandchildren through deceased children; biological ancestry versus adoption; health, fertility, prison, household and age eligibility; prospective overcrowding; additional-child living budgets; marriage repair/arrangement; reproductive candidate selection; emergency priorities; score precedence; read-only score preview and duplicate/reversed claims; sustainable gifts, education, fertility improvements, housing and separation; exact property/farmland/lending parameters; and fallback after queue rejection.

Existing characterization tests were updated where the requested policy deliberately changed behavior. Personality application, deterministic tie ordering, bounded weighted choice, autonomous execution context and preservation of existing queues remain covered. Current analyzer warnings concern existing xUnit assertion style.

These historical checks establish implementation behavior and regression coverage for the pre-D15-003 autonomy build. They do not establish a measured survival-rate gain or mathematically optimal lifetime score. No multi-seed, multi-century benchmark or interactive Windows playthrough was performed. That predecessor retained a two-carrier continuity buffer and conservative estimates for uncertain future achievements; D15-003 supersedes that policy below.

The source package is checked against the supplied archive: original source files are retained, unrelated source bytes are unchanged, and generated artifacts are excluded according to the repository packaging policy. ZIP integrity and extracted file bytes are checked before delivery.

## D15-003 survival-policy correction

D15-003 supersedes the earlier two-carrier/ancestry-priority policy described above. Regression coverage was rewritten around sex-neutral care, the two-existing-child deliberate-expansion soft stop, sick/infertile child commitments, adult-child progression, rented establishment, prerequisite employment, chronic-illness non-freeze, canonical forecast use and subordinate score tie-breaking.

The patch authoring environment for D15-003 has no .NET SDK, so the historical 1,227-test result above must **not** be read as a D15-003 execution result. D15-003 received static source/XML/project/reference checks only here; the local clean build and test run remain the definitive verification. No long-run survival benchmark was run in this environment, so no survival-rate uplift is claimed.

Save compatibility for D15-003: the new `households.autonomy_family_plan` component is additive and optional. Old saves deserialize without it and derive plans on the next autonomous assessment. Derived forecasts/candidates are not persisted. No save-format version bump or manual migration is required.
