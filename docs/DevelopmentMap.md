# Dynastia development map

This file is a maintenance map for the current repository. It is intentionally short and points development work toward the smallest relevant source files.

## Application coordinator

`src/Dynastia.App/ViewModels/MainWindowViewModel.cs` now contains only shared state, constructor wiring, public bindable properties and commands.

The behavior is split by responsibility:

- `MainWindowViewModel.Persistence.cs` — save/load UI state integration.
- `MainWindowViewModel.GameFlow.cs` — new game, year advancement, game-over/menu state, family-view switching and year-summary orchestration.
- `MainWindowViewModel.Family.cs` — family lists, household cards, active/inspected household selection.
- `MainWindowViewModel.Selection.cs` — selected-person projections for stats, health, economy, education, career, justice, narrative and relationships.
- `MainWindowViewModel.Actions.cs` — action refresh/filter execution only.
- `ActionPresentationPolicy.cs` — action categories and display ordering.
- `MainWindowViewModel.Events.cs` — album refresh and application event callbacks.
- `MainWindowViewModel.Formatting.cs` — display-name and relationship-history formatting.

When adding a mechanic action, prefer changing the mechanic plugin and `ActionPresentationPolicy` only when its UI category/order needs special handling. Avoid adding mechanic rules to `MainWindowViewModel`.

## Household mechanics

`StandardHouseholdService` is split into:

- base service — public household queries and reconciliation entry point;
- `.FamilyNews.cs` — chronicle/news visibility ledger;
- `.LegacyReconciliation.cs` — migration/cleanup of old household shapes;
- `.MembershipReconciliation.cs` — adult bloodline households, spouses and dependents;
- `.RelationshipReconciliation.cs` — surviving parents/former partners;
- `.Succession.cs` — household head succession and shared helpers.

`HouseholdsPlugin` keeps initialization in the base file and separates property actions, nanny actions and helpers.

## Economy and locations

`StandardEconomyService` separates household identity/membership, assets/inheritance claims, hosted dependents and internal synchronization/migration.

`StandardLocationService` separates public location operations, event-driven initialization, town selection and legacy canonicalization.

## Persistence

`GameSaveService` separates the stable public save/load entry points from capture, component preparation, restore/application, validation/cipher helpers and serialized record types. Preserve `CurrentFormatVersion` and serialized field names unless a migration is deliberately implemented.

## Large content/rendering modules

- `GenealogyCanvas` is split into base state/public operations, rendering, tooltips, geometry and input handling.
- `RareEventYearSystem` is split into processing, household events, personal events, helpers and private event types.
- `AboutTextBuilder` is split by narrative/stat domain.
- `ThoughtPhraseRenderer` keeps voice selection in the base file while phrase catalogs and domain helpers live separately.
- `CareerPlugin` and `HouseholdsPlugin` keep startup wiring separate from action registration.

These remain single runtime types; the split is structural only and does not alter plugin registration or save data.

## Build and handoff

Use:

```powershell
powershell -ExecutionPolicy Bypass -File build\build-dev.ps1
```

The script builds the solution once, runs the core tests, then installs already-built plugin outputs into the app directory. Use `-SkipTests` only for a deliberately quick local compile.

For a future development-chat handoff, create a source-only archive with:

```powershell
powershell -ExecutionPolicy Bypass -File build\pack-source.ps1
```

The archive excludes generated `bin`/`obj`, Git metadata, logs, saves and existing ZIP archives.

## Refactoring rule

Prefer extracting responsibility without changing behavior in the same patch. Gameplay changes should then be made against the smaller responsibility file. This keeps diffs reviewable and makes regression causes much easier to isolate.
