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

These checks establish implementation behavior and regression coverage. They do not establish a measured survival-rate gain or mathematically optimal lifetime score. No multi-seed, multi-century benchmark or interactive Windows playthrough was performed. Planning explicitly retains a two-carrier continuity buffer and conservative estimates for uncertain future achievements.

The source package is checked against the supplied archive: original source files are retained, unrelated source bytes are unchanged, and generated artifacts are excluded according to the repository packaging policy. ZIP integrity and extracted file bytes are checked before delivery.
