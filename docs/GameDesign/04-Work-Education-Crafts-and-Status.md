# Work, Education, Crafts and Status

## Education
Status: **Implemented**  
Owner: `dynastia.education`

Rules:
- Education is persisted per person and participates in careers/crafts/status.
- Children can gain passive education during the yearly `Status` phase.
- `education.get_education` is an active queued action available only where the person’s town has an actual School institution; `education.help_learning` lets a controller support an eligible child/household learner even when local schooling is limited.
- `education.private_tutor` is a 3,000 zł queued childhood action for eligible resident relatives age 6–17. It ignores local School availability/ceiling and parent Education, but still respects the historical tutoring ceiling; success depends only on the child’s Intellect.
- Historical era and local school/institution access affect which education opportunities are available.
- Craft education can also report regional industry support where relevant.

Primary code/data: `EducationPlugin.cs`, `PassiveEducationYearSystem.cs`, `data/Education/education_eras.csv`, `data/TownLife/education_locality_rules.json`.

Regression tests: `MarriageCareerEducationBalanceTests.cs`, `Development11EmploymentEducationInventoryTests.cs`.

## Careers and employment
Status: **Implemented**  
Owner: `dynastia.career`

Authoritative state includes career ID, level, experience, satisfaction, retirement/pension state and lifetime earnings.

Rules:
- Local job opportunities come from the current town/region and can depend on institutions, regional strengths, historical availability and career family.
- Employment aptitude is career-specific. Composite aptitude uses 75% primary stat + 25% secondary stat when a secondary stat exists.
- Base search chance by aptitude 1–5 is 35/50/65/80/90%; Regional opportunity adds 5 points, Town opportunity adds 8 points, and final chance caps at 95%.
- Salary uses career base salary multiplied by job-level multipliers 1/2/3/5/10 for levels 1–5.
- Career experience is recorded during `Aging`; lifetime earnings during `Finances`.
- Job loss and advancement are separate yearly systems and can be modified by personality, education, Work Harder and context.
- Level 5 is intentionally exceptional: promotion requires Education 5 and has a strong target-level probability reduction.
- Retirement is historical-rule driven. Pension availability/age comes from `retirement_rules.csv`; annual pension is based on lifetime career earnings when a pension system is available.
- Civic-office careers continue to simulate after the technology horizon. Historical office/profile content freezes at the shared technology-freeze year, while incumbent aging, appointment years, approval processing and salary eligibility continue in the actual simulation year.

Primary code/data:
- `StandardCareerService*.cs`
- `CareerAdvancementYearSystem.cs`, `CareerJobLossYearSystem.cs`, `CareerRetirementYearSystem.cs`
- `CareerBalanceRules.cs`
- `data/Career/`
- local opportunity data in `data/Towns/` and `data/TownLife/`

Regression tests: `ContentReworkBatch2CareerCrimeTests.cs`, `MarriageCareerEducationBalanceTests.cs`, `Development11EmploymentEducationInventoryTests.cs`.

## Career actions
Status: **Implemented**

Stable action IDs:
- `career.seek_employment`
- `career.find_another_job`
- `career.quit_job`
- `career.work_harder`
- `career.help_seek_employment`
- `career.help_find_better_job`
- `career.ask_to_recover`
- `career.ask_to_quit`

Timing matters:
- self job search/quit/find-another-job resolve in `LifeEvents`;
- Work Harder and resident-relative assistance resolve in `QueuedActionsEarly`.

`Work Harder` adds promotion pressure but also contributes health/stress/marriage costs through registered providers. `Recover` is owned by Wellbeing and trades output for Health/Satisfaction.

## Job Satisfaction
Status: **Implemented**  
Owner: Career, influenced by lifestyle/wellbeing/household events

Job Satisfaction is persistent career state. Very low satisfaction contributes to Stress and unlocks/reframes some career/wellbeing options. It is not simply rerolled every year.

## Crafts and Mastery
Status: **Implemented**  
Owner: `dynastia.crafts`

Rules:
- Crafts are data-driven by historical era, regional/local context and education/career links.
- People can learn crafts, gain mastery through experience/passive learning, teach relatives and become self-employed where the craft/local economy allows it.
- Craft self-employment is treated as real work/income and is mutually exclusive with incompatible occupation states.
- The mastery income curve keeps the same expected multiplier at each mastery tier while compressing extreme annual windfalls; the same shared curve is also used by criminal occupations.
- `craft.stop_occupation` leaves craft self-employment.
- Craft education/mastery/income rules live in `data/Crafts/`, not in UI.

Primary code/data: `CraftsPlugin.cs`, `StandardCraftService*`, `data/Crafts/`.

Regression tests: `CraftRulesTests.cs`, `ContentReworkBatch3HobbiesCraftsTests.cs`.

## Shared annual productive effort
Status: **Implemented**
Owners: Health + Wellbeing + Crafts/Farming/Heirlooms

Productive work uses `AnnualProductiveEffortRules`, which combines the current `IWorkCapacityService` output multiplier with the already-rolled Recover reduction (`modifier.salary.recover.X`). The helper is deterministic and does not reroll Recover or consume RNG. No productive output is generated when work capacity is unavailable or effectively zero. Crafts and Farming use this shared rule while preserving their established rounding/output behavior.

Primary code: `src/Dynastia.Contracts/AnnualProductiveEffortRules.cs`.

## Artistic work
Status: **Implemented**  
Owner: `dynastia.heirlooms` + Crafts

Rules:
- Artistic crafts can generate notable works during yearly simulation.
- Ordinary artistic production chance is scaled by shared annual productive effort. Dead, imprisoned, simulation-inactive or externally resident artists cannot create works.
- A due Master production guarantee is deferred rather than consumed when productive effort is zero; once the artist has positive eligible effort, the guarantee resolves exactly once under the existing guarantee rules.
- Works are represented as heirloom/family assets, can affect Status and can carry royalty/income behavior.
- Generation, quality/availability and royalty rules are data driven; royalties remain independent of the author's current work capacity.

Primary data: `data/Crafts/artistic_*`, `data/Heirlooms/artistic_*`.

Regression tests: `LocalSocietyArtisticCraftsBatch10Tests.cs`, `LocalSocietyArtisticWorksBatch11Tests.cs`.

## Farming work
Status: **Implemented**  
Owner: `dynastia.farming`

Rules:
- Farm labor is an economic work state separate from ordinary Career employment, but career/marriage logic recognizes it as economically employed where appropriate.
- Eligible household members can work family farmland.
- Output depends on farmland, labor, farm/livestock flavor and historical/local multipliers.
- Recover and health work capacity reduce farming output through the same shared annual productive-effort rule used by Crafts/artistic production.
- Farm help is age-scaled: ages 10–13 contribute 25% of adult output, ages 14–17 contribute 50%, and adults contribute 100%. When farm-worker capacity is scarce, higher expected contributors are selected first with deterministic age/ID tie-breaks.
- Realized farm output uses one persisted global four-season weather result per game year, shared by every farming household. Livestock diversification can soften each household’s exposure to the same crop-weather result; expected income assumes neutral weather.

Primary data: `data/Farming/`.

Regression tests: `FarmingRulesTests.cs`, `TownAffairsHousingFarmingTa*Tests.cs`.

## Religious vocation
Status: **Implemented**  
Owner: `dynastia.religious_vocation`, Career/Church/Status integrations

Rules:
- Eligible unmarried young adults can enter religious vocation where required Church/institution conditions exist.
- Vocation replaces ordinary employment paths and blocks normal marriage/reproduction routes where the active vocation tag applies.
- A male-line man in active religious vocation is excluded from controllability and the living male-line succession pool.
- Career IDs include dedicated priest/nun vocation careers.

Primary code/data: `plugins/Dynastia.Mechanics.ReligiousVocation/`, `data/LocalSociety/religious_calling_rules.json`, TownLife institution requirement data.

Regression tests: `LocalSocietyReligiousCallingsBatch12Tests.cs`.

## Renown and Reputation
Status: **Implemented**  
Owner: `dynastia.status`

Player-facing purpose: persistent social visibility/standing that reacts to careers, wealth, education, crafts, farming, loans, heirlooms, crime, historical events, Church/community activity and other explicit status effects.

Rules:
- Status is event/component driven. Other systems should publish/register explicit effects rather than directly duplicating Status math.
- Renown represents prominence; Reputation represents approval/trust.
- Local/household/person presentation may differ depending on source, but durable effect ownership remains in Status.
- Church donations, poor relief, crime, civic office, artistic works and similar local-society mechanics can produce status deltas from data.

Primary code/data: `plugins/Dynastia.Mechanics.Status/`, `data/LocalSociety/status*.csv/json`.

Regression tests: `LocalSocietyStatusBatch1Tests.cs` and later LocalSociety batch tests.

### Life of Crime productive effort
Life of Crime is productive work for annual-effort purposes. Recover and Health Work Capacity proportionally reduce expected and realized Heist proceeds; zero productive capacity produces no Heist or Mastery year.
