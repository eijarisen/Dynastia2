# Dynastia development map

This map points maintenance work toward the smallest relevant files. It describes the current repository rather than the original Dynasty 4 implementation.

## Application coordinator

`MainWindowViewModel` remains one runtime type but is split by responsibility:

- `MainWindowViewModel.cs` — injected services, constructor wiring, top-level game/menu/year state and commands.
- `MainWindowViewModel.Persistence.cs` — save/load UI integration.
- `MainWindowViewModel.GameFlow.cs` — New Game, year advancement, game-over/menu state and year-summary orchestration.
- `MainWindowViewModel.Family.cs` — family lists, household cards and household selection.
- `MainWindowViewModel.Selection.cs` — selected-person refresh/projection behavior.
- `MainWindowViewModel.SelectionPresentation.cs` — selected-person bindable state.
- `MainWindowViewModel.HouseholdPresentation.cs` — budget, income, expenses, property and household-warning projections.
- `MainWindowViewModel.Collections.cs` — observable collections exposed to the UI.
- `MainWindowViewModel.Actions.cs` — action refresh/filter execution.
- `MainWindowViewModel.SelfImprovement.cs` — Self Improvement selector integration.
- `MainWindowViewModel.Relations.cs` — Family Relations window projections/interactions.
- `ActionPresentationPolicy.cs` — action categories and display ordering.
- `MainWindowViewModel.Events.cs` — album refresh and application event callbacks.
- `MainWindowViewModel.Formatting.cs` — display-name and relationship-history formatting.

When adding a mechanic action, prefer changing the owning mechanic plugin. Only touch `ActionPresentationPolicy` when its UI category/order needs explicit presentation metadata.

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

Development build:

```powershell
powershell -ExecutionPolicy Bypass -File build\build-dev.ps1
```

Repository cleanup:

```powershell
powershell -ExecutionPolicy Bypass -File build\cleanup-repo.ps1
```

Source-only handoff:

```powershell
powershell -ExecutionPolicy Bypass -File build\pack-source.ps1
```

The repository should not track `bin`, `obj`, IDE caches, test-result directories, logs, local saves or generated archives.

## Refactoring rule

Prefer moving existing behavior into smaller responsibility files before changing that behavior. Do not combine a broad structural refactor with balance changes, save-schema changes, action-ID changes or year-phase reordering unless the functional change explicitly requires it.
