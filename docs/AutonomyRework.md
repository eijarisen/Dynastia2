# Autonomous lineage survival rework — 2026-09-25

This source package updates the repository supplied as `Dynastia(20260925-000515).zip`. Rebuild using the normal `build/build-dev.ps1` workflow. The App project also enables the implicit imports required by its existing source files for a clean build. Existing save fields, action IDs, event rewards and annual system ordering are unchanged; the GameScore plugin adds a read-only preview service.

## Decision policy

| Priority | Purpose |
| --- | --- |
| 1000 | Immediate medical survival and necessary emergency funding |
| 800 | Restore household solvency |
| 700 | Protect and continue the male lineage |
| 650 | Protect and continue the wider bloodline |
| 500 | Preserve family stability and longer-term health |
| 300 | Sustainable development; prefer the greatest estimated incremental score |
| 100 | Optional activity; prefer incremental score when available |

Urgent medical care and a viable household budget support both lineage objectives. For comparable medical needs, male-line carriers and needed reproductive partners take precedence over other bloodline relatives. Acute danger precedes less urgent treatment. Personality and bounded random choice operate within equivalent priority/reward choices and cannot promote score above family survival.

## Implemented behavior

- Two daughters no longer satisfy male-line continuity. The planner seeks a buffer of two viable descendant carriers, checks canonical bloodline/male-line membership and biological parent links, and traverses grandchildren even through deceased children. Children living elsewhere still count. Unrelated/adopted residents still consume care capacity without falsely satisfying biological continuity.
- A living person is not automatically a dependable future carrier: reproductive availability, fertility and serious health risks matter. Adults aged 60+, women over 40, and adults without a viable reproductive path do not satisfy the buffer. Descendants of those people can still satisfy it. The buffer counts people, not independent family branches.
- Resident sons can receive the existing arranged-marriage action. Daughters' marriages preserve wider bloodline continuity. Candidate choice considers acceptance, fertility and remaining reproductive years before wealth/prestige when continuity is needed. It uses the existing annual candidate pool and does not repeatedly generate additional candidates.
- Marriage repair precedes deliberate conception when satisfaction is low. Both parents must be available and share a household. Deliberate attempts stop during serious household illness, poverty, insufficient care capacity, prospective overcrowding or an unsustainable additional living budget. Passive births retain the game's existing rules.
- Housing can support family growth: extend an owned residence when space is needed, or buy a suitable first house and budget for necessary extensions. No pointless extension is selected merely for its repeatable score reward. Offer prices and construction reserves are checked explicitly.
- Medical fertility improvements can address a weak reproductive path; immunity/longevity improvements support survival. Paid education and optional investments retain reserves. Overwork is limited to healthy, unstressed, financially secure households without a continuity need or dependent children, and cannot be repeated in consecutive years.
- Separate-household support requires actual recipient need and protects the donor's budget. Moving a resident branch out requires a local spare home and sufficient income for both households. Remote farmland is explicitly selected before local land when selling; farmland aid requests now evaluate willingness. Lending carries actual generated bank terms, including their interest multiplier.
- A rejected queue submission tries the next candidate. Normal autonomous processing retains already queued actions. Simulate-all/debug processing continues to use the same policy for every household.

## Score estimation

The GameScore plugin processes hypothetical events against a detached copy of its active claims and already-earned outcomes. It applies the same event filtering and deduplication as real scoring without changing the live ledger, event log or random state.

Estimates include attributable education/stat achievements, new craft learning and employment, property purchases, and claim reversals. Known probabilities scale expected rewards. Already-earned stat/education/craft tiers are worth zero. Job switching does not falsely award a new employment bonus. Unknown future promotions, mastery gains and passive family events receive no speculative points. This is a survival-oriented heuristic, not an exhaustive lifetime-score optimizer.

## Validation and balance follow-up

Validation results are recorded in `docs/AutonomyValidation.md`. Focused coverage exercises descendant classification, single-heir risk, marriage/conception gates, medical priorities, score previews, reserves, housing plans and action parameters. Tests verify behavior and regressions; no long-run survival uplift is claimed without a multi-seed simulation benchmark.

`docs/AutonomyBalanceAudit.md` contains code-referenced suggestions for uncapped house-extension scoring, cash-only divorce settlement shielding, guaranteed lending and annual reproduction incentives. Those game-wide balancing changes are proposals. This package changes autonomous decisions and repairs their parameter handling.
