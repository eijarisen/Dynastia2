# Dynastia development map

This map points maintenance work toward the smallest relevant files. It describes the current repository rather than the original Dynasty 4 implementation.

For gameplay intent and current implemented rules, begin with `docs/GameDesign/00-Index.md`; use this map after that to find the owning code/data/tests.

## Application coordinator

`MainWindowViewModel` remains one runtime type but is split by responsibility:

- `MainWindowViewModel.cs` — injected services, constructor wiring, top-level game/menu/year state and commands.
- `MainWindowViewModel.Persistence.cs` — save/load UI integration.
- `MainWindowViewModel.GameFlow.cs` — New Game, year advancement, game-over/menu state and year-summary orchestration.
- `MainWindowViewModel.Family.cs` — family lists, household cards and household selection.
- `MainWindowViewModel.Selection.cs` — selected-person refresh/projection behavior.
- `MainWindowViewModel.SelectionPresentation.cs` — selected-person bindable state.
- `MainWindowViewModel.HouseholdPresentation.cs` — budget, income, expenses, property and household-warning projections.
- `MainWindowViewModel.Collections.cs` — non-action observable collections exposed to the UI.
- `MainWindowViewModel.Actions.cs` — action binding/selection forwarding, status feedback and the single broad post-action refresh.
- `Actions/ActionPanelCoordinator.cs` — available actions, category state, Pass/shortcuts, queued summaries, contextual labels and dispatch requests/results.
- `Actions/ActionSelectionOptionService.cs` — property/town options, loan presentation and selected-parameter submissions.
- `Actions/ActionSurfaceDefinitions.cs` — App-only navigation definitions, aggregation, secondary-selection routing and Town Affairs access/labels.
- `MainWindowViewModel.TownLife.cs` — Town Affairs routing for Medical Improvements, Therapy and Religious Study.
- `MainWindowViewModel.Relations.cs` — Family Relations window projections/interactions.
- `ActionPresentationPolicy.cs` — metadata-first category/visibility/group resolver with legacy ID fallback.
- `MainWindowViewModel.Events.cs` — album refresh and application event callbacks.
- `MainWindowViewModel.Formatting.cs` — display-name and relationship-history formatting.

Mechanics own action presentation through `GameActionDefinition.Presentation` in Contracts.
Use `ActionPresentationCategories` and `ActionPresentationGroups` (or plugin-local group IDs)
for category filters, adjacency order, anchors and primary-list visibility. Related groups
stay at their first source occurrence; `PlaceLast` items move last stably. Family Connections
uses `PlaceAfterAnchor` to follow the first employment search without breaking its work group.
An explicitly empty category list is unfiltered; unknown category IDs fall back to Personal.

New metadata-enabled actions do not need edits to `ActionPresentationPolicy` or `ActionEmojiMap`.
Their ID/prefix tables remain only for legacy definitions; queued displays with only an ID
continue to use the emoji fallback. Copy `Presentation` when wrapping a definition for historical
labels. Town Affairs, Manage Properties, Manage Finances and Work in a Profession remain
App-owned pseudo-actions with explicit metadata. Presentation is not serialized into queued saves.

`App.axaml.cs` composes the action option and surface collaborators. MainWindow wires its action
coordinator to the live selected-person resolver; the existing public constructor builds the same
collaborators for other callers. The coordinator publishes selection requests without submitting
an action, or one `ActionUiExecutionResult` after a registry submission. It never refreshes the
whole UI. MainWindow handles that result once, preserving the ordinary-action versus selection
failure-message policy. Property and loan submissions still target the active household; Move Out
keeps the selected resident. Town Affairs, Inventory and Craft/Education window content stays in
its existing feature partials. Household-card queue details reuse the coordinator's formatter.

`Dynastia.App.Tests` covers coordinator dispatch/result counts, MainWindow refresh forwarding,
Move Out's zero/one/many-property paths, exact parameter summaries, filter unions, legacy queue
fallbacks, contextual sibling labels and navigation guards. The older XAML/layout assertions remain;
source-location assertions for moved navigation behavior have been replaced by these behavior tests.


## Career and historical availability

Career behavior is concentrated in `plugins/Dynastia.Mechanics.Career/`:

- `StandardCareerService.cs` — assignment state and common validation.
- `StandardCareerService.Employment.cs` — entry, job changes, relocation and family-connection employment.
- `StandardCareerService.Compensation.cs` — retirement, salary and title resolution.
- `CareerCatalog.cs` / `CareerDefinition.cs` — career-data loading and definitions.
- `RetirementRuleCatalog.cs` — year-based retirement/pension rules.
- `CareerAdvancementYearSystem.cs` — annual firing/promotion logic.

Historical career data lives in `data/Career/`. Time-aware local opportunities live in `data/Towns/` and `Dynastia.Mechanics.Locations`.

Historical Education access lives in `data/Education/education_eras.csv` and is loaded by `EducationEraCatalog`. Historical player-action availability/text lives in `data/Common/historical_action_variants.json` and is exposed through `Dynastia.Mechanics.Historical`. Rare Events use fixed pool gates in `data/RareEvents/rare_event_pool_rules.csv` and data-driven availability/eligibility in `data/RareEvents/rare_events.csv`; adding event rows must not change aggregate Household/Personal gate frequency.

`GameCalendarConfiguration` owns the selectable 1700–1900 New Game range. `HistoricalEraConfiguration` owns the gameplay-era labels. Do not add new fixed 1900 assumptions.

## Household mechanics

`StandardHouseholdService` is split into:

- base service — public household queries and reconciliation entry point;
- `.FamilyNews.cs` — Chronicle/news visibility ledger;
- `.LegacyReconciliation.cs` — migration/cleanup of old household shapes;
- `.MembershipReconciliation.cs` — adult bloodline households, spouses and dependents;
- `.RelationshipReconciliation.cs` — surviving parents/former partners;
- `.Succession.cs` — household-head succession and shared helpers.

`HouseholdsPlugin` keeps initialization separate from property and nanny actions.

Autonomous decisions use `AdvancedAutonomousHouseholdStrategy` as a small facade. It
owns the single-scorer dispatch check, personality adjustment, score clamp, weighted
choice and queue submission. `AutonomousHouseholdDecisionService` still owns which
households are processed and whether existing queues are retained or replaced.

`Autonomy/AutonomousSnapshotBuilder` preserves live-member and service-read order;
`AutonomousActionCandidateBuilder` owns mechanical availability, parameter selection
and first-occurrence deduplication. The six named domain scorers consume candidates
without rediscovering availability. Shared pure calculations live in
`AutonomousScoringHelpers`; the narrow `AutonomousReproductiveEligibility` query is
shared by snapshot construction and continuity scoring. Optional plugin services
are still resolved at the original point of use, not cached at construction.

`HouseholdsPlugin` explicitly constructs these internal collaborators in a fixed
order. Every supported ID/prefix must have exactly one owner; overlapping owners
throw before either scorer runs, and unsupported actions remain unscored. These
collaborators add no Contracts services or persistent state.

## Economy, property and loans

`StandardEconomyService` separates household finance, assets, hosted dependents and synchronization/migration.

Property actions live in `Dynastia.Mechanics.Households`; town economics/location resolution live in `Dynastia.Mechanics.Locations`.

`Dynastia.Mechanics.Loans` owns loan contracts, repayment, debt inheritance and receivable inheritance. UI projection of loan income should not become a second source of finance rules.

## Relationships and Family Relations

Marriage mechanics remain in `Dynastia.Mechanics.Relationships`.

- `RelationshipsPlugin.cs` — plugin wiring and action/system registration.
- `RelationshipsPlugin.ArrangedMarriage.cs` — arranged-partner eligibility/generation helpers.
- `RelationshipBreakupService.cs` — shared divorce/breakup consequences.

Cross-household family relations live in `Dynastia.Mechanics.FamilyRelations`:

- `StandardFamilyRelationService.cs` — persistent relation state/query logic.
- `FamilyRelationActions.cs` — registration and common helpers.
- `FamilyRelationActions.Money.cs` — Ask/Give Money.
- `FamilyRelationActions.Property.cs` — Ask/Give House and relocation consequences.
- `FamilyRelationActions.Career.cs` — Ask/Give Job Help.
- `FamilyRelationEventBridge.cs` — meaningful event-to-relation updates.

Do not reintroduce Morals-based family-support acceptance in the legacy Family Support plugin.

## Nationality and names

Nationality identity and name cultures are owned by `Dynastia.Mechanics.Family`:

- `StandardNationalityService.cs` — persisted nationality identity, historical RegionId distributions and deterministic generation.
- `StandardHistoricalNameService.cs` — Polish historical first-name eras plus the shared 25-culture name/surname catalogues.
- `data/Nationalities/` — nationality registry, generation rules and regional historical weights.
- `data/Names/name_cultures.json` / `data/Names/Nationalities/` — culture registry and non-Polish weighted name pools.

Nationality is identity/flavor state only. Do not add nationality-based acceptance, economy, health, crime, fertility or personality modifiers. Polish surname display morphology applies only to the Polish name culture.

## Wellbeing

`WellbeingPlugin` is one plugin split by action domain:

- `WellbeingPlugin.cs` — constants and registration wiring.
- `WellbeingPlugin.RecoveryActions.cs` — Recover and Drink.
- `WellbeingPlugin.TreatmentActions.cs` — therapy/health-treatment actions and target checks.

## Biography and narrative

`StandardBiographyService` is split into:

- base service — state, public API, restore/seed and entry storage;
- `.EventHandling.cs` — event propagation to subjects/relatives;
- `.EventFormatting.cs` — event classification, emoji/text helpers.

Thought generation remains under `Dynastia.Mechanics.Thoughts`. The large phrase catalogue is content rather than simulation logic; avoid adding mechanic decisions to it.

## Persistence

`GameSaveService` separates public save/load entry points from capture, preparation, apply/restore, validation/cipher helpers and serialized record types.

Preserve save field names and `CurrentFormatVersion` unless a deliberate migration is implemented. Plugin-owned components should continue to use the existing component persistence path where possible.

## Build and repository hygiene

Routine incremental development:

```powershell
powershell -ExecutionPolicy Bypass -File build\build-dev.ps1
```

Only installed runtime plugin copies are removed. Project `bin/obj` outputs remain available for MSBuild incrementality and project-reference tracking. A missing plugin output or a plugin absent from the solution triggers a direct incremental project build. The manifest's assembly is verified before and after installation; plugin-local `Dynastia.Contracts.dll` is not installed.

Authoritative clean verification:

```powershell
powershell -ExecutionPolicy Bypass -File build\build-dev.ps1 -Clean
```

`-Clean` removes the shared policy's build-output directories before running the normal build/test/install path. Source, data, saves and Git metadata are preserved. Unless `-SkipTests` is supplied, repository-tooling checks run and every source `*Tests.csproj` under `tests/` is tested in full-path order, with incremental build/restore enabled for projects not yet in the solution.

Source-only handoff (no cleanup prerequisite):

```powershell
powershell -ExecutionPolicy Bypass -File build\pack-source.ps1
```

`build/repository-artifact-policy.ps1` owns separate build-output directories, source-package-only exclusions, generated-file patterns and archive exclusions. Packaging prunes excluded directories, validates the unfiltered stage before compression, and reports its file count and byte size. Required stage items are `Dynastia.slnx`, `global.json`, `src/`, `plugins/`, `data/`, `tests/` and `build/`; docs and legitimate hidden source files are also retained. Keep `.gitignore` categories synchronized; it is never parsed as production policy.

Optional local cleanup:

```powershell
powershell -ExecutionPolicy Bypass -File build\cleanup-repo.ps1
```

Cleanup reports removed directory/file counts and preserves Git metadata and saves, including generated-looking files inside them. `-IncludeArchives` opts into local ZIP deletion. Cleanup is not required for routine builds or source packaging.

`build/test-repository-tooling.ps1` runs dependency-free temporary-fixture checks for policy/.gitignore coverage, packaging validation, save preservation and mocked build orchestration. It runs automatically in development builds unless tests are skipped, and can be invoked independently. Mocked tooling checks do not establish C# compilation or gameplay correctness; the real `-Clean` build and full test run remain the verification gate. Both Windows PowerShell 5.1 and PowerShell 7 are supported.


## Regression tests and refactoring

`tests/Dynastia.TestSupport` supplies cached repository paths (`RepositoryFiles`) and
strict scripted randomness (`SequenceGameRandom`). Use it for source-artifact reads
and deterministic fixtures instead of adding another root locator or permissive RNG.

`tests/Dynastia.App.Tests` references the application and tests action categories,
ordering, emoji fallbacks, selector output, queued labels and routing without opening
a window. App internals are visible only to this test assembly.
`ActionPresentationMetadataTests` covers metadata precedence, stable groups/anchors and primary-list
filtering. `ProductionActionPresentationTests` invokes the compiled registration/factory boundaries
without evaluating gameplay delegates and compares first-party/dynamic metadata against legacy
presentation, including historical loan/nanny wrappers and catalog-driven craft/stat actions.

Core characterization coverage lives in `AutonomousStrategyCharacterizationTests`,
`EstateInheritanceCharacterizationTests` and `TurnStartActionCharacterizationTests`.
These execute the existing implementations and protect priority/RNG behavior, signed
inheritance, asset identities, event output and pre-aging execution. Household internals
are visible to `Dynastia.Core.Tests`; no gameplay implementation is duplicated in tests.

REF-01 migrates logic assertions tied to the action presentation, autonomous strategy
and estate source files into these suites. XAML/layout, composition and data-artifact
checks remain in the existing Core test project. New refactors should update fixture
construction as necessary while preserving these observable behavior assertions.
`AutonomousStrategyDecompositionTests.cs` extends the same characterization fixture
with exact ID/prefix ownership, overlap rejection, personality/clamp, cross-domain
choice/queue, snapshot read order, conservative forecast and candidate parameter
checks. Its read-only service probes throw on unexpected calls. Score comparisons
are exact; scripted randomness must be fully consumed without extra draws.

Both `*Tests.csproj` projects are included in the solution and automatically discovered
by `build-dev.ps1`; the shared support library is not a test project.
