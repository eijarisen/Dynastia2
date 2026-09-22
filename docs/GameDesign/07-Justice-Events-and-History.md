# Justice, Events and History

## Ordinary crime
Status: **Implemented**  
Owner: `dynastia.justice`

Rules:
- Eligible adults can commit ordinary random crimes during `LifeEvents` according to year/age/context-weighted crime data.
- Crime aptitude/context uses relevant stats, employment and personality/Morals where defined.
- Conviction can remove employment, create prison state, damage family/Status and feed Marriage/Stress consequences.
- Criminal records persist known authority offenses with year, crime ID/display name and original/final sentence.

Primary code/data: `CrimeYearSystem.cs`, `StandardJusticeService.cs`, `data/Common/crimes.json`, `data/Justice/`.

Regression tests: `ContentReworkBatch2CareerCrimeTests.cs`, `LocalSocietyCourtJusticeBatch7Tests.cs`.

## Life of Crime / criminal occupation
Status: **Implemented**  
Owner: `dynastia.justice`

Stable action IDs are data-defined:
- `justice.commit_crime`
- `justice.leave_life_of_crime`

Rules:
- Minimum age 18 and Evil Morals are required to enter.
- First entry starts the occupation and performs an immediate heist; once the occupation has ever started, the “start” action is not offered again.
- Criminal occupation is exclusive with ordinary career, craft self-employment and farm labor.
- It does not automatically retire and resumes after prison release.
- One heist attempt per active calendar year; imprisonment suppresses heists.
- Mastery comes from active heist years. Dominant Appeal/Strength/Intellect determines archetype; all three at 5 creates Mastermind.
- While criminal occupation is active, ordinary random-crime processing is skipped for that person.
- Criminal heist income uses the same mean-preserving compressed long-tail mastery curve as Craft self-employment: expected income by mastery is retained while extreme single-year payouts are reduced. Detection, confiscation, sentencing and archetype bonuses are unchanged.

Primary data: `data/LocalSociety/criminal_occupation_rules.json`, archetype/mastery CSVs.

Regression tests: `LocalSocietyCriminalOccupationBatch6Tests.cs`.

## Prison, bail, escape and criminal records
Status: **Implemented**  
Owner: `dynastia.justice`

Rules:
- Prison time is persisted and counted down in `Status`.
- Prison guards normal player actions unless an action explicitly bypasses/allows the state.
- Bail action ID `justice.bail_out`: guaranteed if affordable; cost = `max(20,000, 20,000 + 7,500 × remaining sentence years)`. It releases immediately when the queued action resolves and does not erase the criminal record.
- Escape action ID `justice.attempt_escape`: requires Intellect 5, once per current imprisonment; base success 20%, Mastermind 30%; failure extends sentence by 3 years, success releases immediately.
- Legal/law-enforcement family protection can reduce sentences and stolen-heirloom detection, with a combined sentence multiplier floor of 0.5.

Primary data: `data/LocalSociety/court_justice_rules.json`.

## Stolen heirlooms
Status: **Implemented**  
Owners: Justice + Heirlooms

Keeping a stolen heirloom is mechanically harmless. Selling one uses the normal sale value but can be detected according to legal-protection tier. If caught, proceeds are lost, the heirloom is confiscated, a 2–5 year sentence is created and Reputation falls by 8.

## Rare Events
Status: **Implemented**  
Owner: `dynastia.rare_events`

Rules:
- Rare events use fixed aggregate Household/Personal pool gates; adding content rows must not accidentally increase the overall annual gate frequency.
- Within a triggered pool, year, age, context, career/family and supplemental eligibility/weights select the event.
- Effects can change Health, economy, careers, justice, household state, mortality and heirlooms through explicit effect definitions.
- Recent-event tags/state are cleaned during `PreYear`.

Primary data: `data/RareEvents/rare_event_pool_rules.csv`, `rare_events.csv`, variants/context/effect CSVs.

Regression tests: `ContentReworkBatch4RareEventsTests.cs`, `SharedMechanics4ECatalogValidationTests.cs`.

## Historical Events
Status: **Implemented**  
Owner: `dynastia.historical`

Rules:
- Historical events run in `PreYear` and use event scope, target filters, candidate modifiers and effect profiles.
- Events are defined for the historical period supported by content (currently through 2026) and can be local/regional/national or household/person-targeted.
- They may affect wealth, health, career, justice, property/prosperity, migration and mortality where the selected effect profile says so.
- Partition/news presentation describes the current historical change rather than announcing later future partitions.
- Historical action wording/availability for other plugins is supplied through `historical_action_variants.json`.

Primary data: `data/HistoricalEvents/`, `data/Common/historical_action_variants.json`.

Regression tests: `HistoricalEventsReworkTests.cs`, `HistoricalRetouchBatch2Tests.cs` through `HistoricalRetouchBatch6Tests.cs`.

## Historical migration and external residence
Status: **Implemented**  
Owners: Historical + Locations + Succession/Households

Rules:
- Historical events can move a person/household to external residence through configured migration routes/effects.
- External residence is distinct from death. The person can remain alive and male-lineage but is no longer locally controllable.
- If living male-lineage members exist but all are externally resident, Succession reports the terminal state **dynasty left Poland**.
- Location/residence-sensitive mechanics skip or restrict inactive/external people as appropriate.

Primary data: `data/HistoricalEvents/historical_migration_routes.json`.

## Historical towns and polity changes
Status: **Implemented**  
Owner: Locations

Town names, status, polity and population vary by year. Mechanics should query the year-aware town service rather than cache a modern name/polity as permanent identity.

Primary data: `data/Towns/dynastia-towns.json`.

## Chronicle/news rules
Status: **Implemented**  
Owners: event bus + Biography/UI + mechanic-specific news filtering

Mechanics emit structured `GameEvent` objects; Chronicle/Biography formatting should consume those events rather than mechanics duplicating presentation state. Household Family News uses a visibility ledger so only intended important household events reach the Chronicle. The console `[year] Running system.id` trace is diagnostic output, not Chronicle content.
