# Health, Wellbeing and Life Course

## Aging
Status: **Implemented**  
Owner: `dynastia.aging`

All living/active simulated people age during the `Aging` phase. Age-dependent tags/state are reconciled by the relevant services; reaching adulthood can unlock household, succession, relationship, career, education and inheritance behavior later in the same annual pipeline.

Primary code: `plugins/Dynastia.Mechanics.Aging/AgingSystem.cs`.

## Health model
Status: **Implemented**  
Owner: `dynastia.health`

Authoritative state: current/max Health plus persisted condition states.

Annual Health phase:
- base natural change begins at `Longevity × 0.5`;
- household Lifestyle modifiers apply;
- registered mechanic modifiers apply (poverty/overcrowding/work/recovery/etc.);
- active conditions apply their annual effects;
- recovery/new-condition checks follow.

Mild-condition base chance by Immunity before global scaling is 28/20/14/9/5% for Immunity 1–5. The current mild-frequency scale is `0.45`.

Serious-condition base chance by age before Longevity/global scaling is:
- under 18: 0.2%;
- 18–39: 0.4%;
- 40–59: 1.0%;
- 60–74: 2.0%;
- 75+: 3.5%.

Longevity multiplies serious incidence by 1.80 / 1.40 / 1.00 / 0.70 / 0.45 for Longevity 1–5, then the current serious-frequency scale `0.35` applies. Condition selection itself is weighted by historical/local context and genetic/condition predispositions.

Immunity 5 has a small natural-recovery pathway for eligible serious non-chronic conditions.

Primary code/data:
- `HealthYearSystem.cs`
- `HealthIncidenceRules.cs`
- `StandardHealthService.cs`
- `data/Common/health_conditions.json`
- `data/Health/health_condition_*.csv`

Regression tests: `HealthContentReworkBatch1Tests.cs`, `HealthMortalityStabilizationTests.cs`.

## Stress and mental health
Status: **Implemented**  
Owner: `dynastia.health` plus stress providers from other mechanics

Rules:
- Stress is accumulated from registered sources instead of one monolithic formula.
- Career strain, relationships, justice/prison, households and other systems can contribute.
- The late `health.life_stress` system may create eligible mental-health conditions based on total Stress, existing conditions, personality and context weights.
- Mental-health conditions then behave like normal Health conditions and may be treatable through Therapy when local historical medical rules permit.

Primary code/data: `MentalHealthYearSystem.cs`, `StressModifierRegistry.cs`, `data/Health/health_stress_outcomes.csv`.

## Recover
Status: **Implemented**  
Owner: `dynastia.wellbeing`

Action ID: `wellbeing.recover`.

Rules:
- Queued in `QueuedActionsEarly`.
- Adds **+15** to that year's Health calculation.
- Improves Job Satisfaction by 2.
- Reduces that year's regular job/craft/farm work income/output by a randomly selected **10–50%** through the recover salary modifier.
- Recovery activity text is historical/data-driven.
- Recover can have small related childhood effects through the relevant provider.

Primary code/data: `WellbeingPlugin.RecoveryActions.cs`, `WellbeingHealthModifierProvider.cs`, `data/Common/recovery_activities_time_based.json`.

## Drink
Status: **Implemented**  
Owner: `dynastia.wellbeing`

Action ID: `wellbeing.drink`.

Rules:
- Adult self-action available when current Stress is above zero.
- Immediately costs 10 Health and reduces current-year Stress pressure by 2.
- Drinking can create Alcoholism; personality changes the chance (approximately 25% for Sanguine/Phlegmatic and 33% for Choleric/Melancholic in the current action description/rules).

## Treatment and Therapy
Status: **Implemented**  
Owner: `dynastia.wellbeing`, `dynastia.townlife`

Rules:
- Medical treatment and Therapy are local-service actions. Historical era and the current town's Medical facility determine availability, presentation and cost/effectiveness.
- Family treatment can target eligible household relatives.
- Base medical-treatment recovery is historical: **20 Health (1700–1849), 25 (1850–1945), 30 (1946+)**, before the local treatment-quality adjustment.
- Town Affairs Health filters out improvements/treatments whose facility requirements are not met, while unaffordable but otherwise valid options remain visible and disabled.
- Empty Therapy/Improvement sections are hidden; exact local-care multipliers are not shown to the player.

Primary code/data: `WellbeingPlugin.TreatmentActions.cs`, `HealthcareEraCatalog.cs`, `data/Health/healthcare_eras.csv`, `data/TownLife/medical_quality.csv`.

Regression tests: `Development11TownAffairsPresentationTests.cs`, `Development13ContinuationTests.cs`.

## Paid stat improvements
Status: **Implemented**  
Owner: `dynastia.stat_improvements`

Player-facing purpose: expensive adult self/family improvement through sufficiently advanced local medicine.

Current stable action IDs:
- `stats.improve_strength` — Medical Tier 1+
- `stats.improve_intellect` — Tier 2+
- `stats.improve_immunity` — Tier 2+
- `stats.improve_appeal` — Tier 2+
- `stats.improve_longevity` — Tier 3+
- `stats.improve_fertility` — Tier 3+

Rules:
- adult, living, controlled-household targets only;
- historical action variant must exist for the current year;
- local Medical tier must meet the definition;
- base rule cost is 20,000 zł before local treatment-cost scaling;
- the action raises the acquired/effective stat by one up to its cap;
- Fertility improvement can restore effective fertility from 0 to 1 and clears future-compatible infertility tags/condition state.

Primary data: `data/LocalSociety/medical_stat_improvement_rules.csv`, `data/Common/historical_action_variants.json`.

## Work capacity and household health pressure
Status: **Implemented**  
Owners: Health + Career + Households + Farming/Crafts

Health affects whether people can work effectively and other mechanics can register annual Health modifiers. Poverty, crowding, childcare load, unemployment/overwork and historical events are applied by their owning systems rather than hard-coded inside Health.

## Mortality
Status: **Implemented**  
Owner: `dynastia.mortality`

Rules:
- Health at or below zero is deterministic mortality unless a mechanic-specific rescue such as Second Wind resolves it.
- Generic accident chance is **0.08% per year** before personality adjustment.
- Terminal conditions add 10 percentage points each before the global random-mortality scale.
- Random illness mortality is scaled by `0.40`.
- Natural old-age death begins only above age 30.
- Longevity profile age = `40 + 10 × Longevity`, producing profile ages 50/60/70/80/90.
- At the profile age, base annual natural death chance is 8%; it grows/declines exponentially with an 8-year curve and receives a small Immunity adjustment. Natural chance is capped at 95%.
- Death publishes structured events and triggers family, household, inheritance and relationship consequences in their later phases.

Primary code: `MortalityRules.cs`, `MortalityYearSystem.cs`, `MortalityDeathService.cs`, `ZeroHealthResolutionYearSystem.cs`.

Regression tests: `HealthMortalityStabilizationTests.cs`, `LateMortalityTests.cs`.

## Life-course interactions
Status: **Implemented**

Important cross-system timing:
- Aging occurs before annual early actions.
- Education/retirement/personality/prison/child state can update in `Status` before Health.
- Natural mortality runs after Health, while late zero-health resolution follows marriage phases.
- Inheritance and succession occur only after mortality resolution.
- Religious vocation can remove a male-line person from controllable/succession eligibility even while alive.
