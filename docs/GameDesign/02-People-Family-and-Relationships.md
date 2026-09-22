# People, Family and Relationships

## Family, Bloodline and Male Lineage
Status: **Implemented**  
Owner: `dynastia.family`

Player-facing purpose: distinguish the entire simulated dynasty from the narrower playable succession branch.

Authoritative state:
- `family.bloodline` — wider dynasty descent.
- `lineage.male` — direct male-line succession.
- `FamilyComponent` — parents, children, spouse and relationship history.

Rules:
- Bloodline membership follows dynasty descent through both sexes.
- Male Lineage is the playable agnatic branch and is used by Succession.
- Family relationships are stored as IDs/history rather than inferred only from household co-residence.
- Paternal/maternal relationships, half-siblings and former spouses remain inspectable after household changes.

Primary code: `plugins/Dynastia.Mechanics.Family/StandardFamilyService.cs`, `FamilyComponent` contracts.

## Identity, nationality, names and appearance
Status: **Implemented**  
Owner: `dynastia.family`, `dynastia.appearance`, `dynastia.locations`

Rules:
- Nationality is persisted identity/flavor state and does not itself modify economy, health, crime, fertility or personality.
- Historical first names and surnames are selected from year-aware/culture-aware pools.
- Foreigners can receive foreign birthplaces from the world-city catalogue.
- Polish surname morphology is culture-specific; non-Polish cultures do not use Polish feminine transformations.
- Household assimilation can produce Polish-surname adoption through explicit annual/action rules; this is not a blanket nationality replacement.
- Appearance is inherited/generated and presented through the portrait/emoji system without becoming a substitute for mechanical stats.

Primary data:
- `data/Names/`
- `data/Nationalities/`
- `data/Locations/foreign_cities.csv`
- `data/Locations/foreign_birthplace_rules.json`

Regression tests: `AppearancePortraitTests.cs`, `TownsNationalitiesBatch3Tests.cs`, `TownsNationalitiesBatch4Tests.cs`, `Development10LifestyleAssimilationTests.cs`, `Development11LocalityRecoverForeignBirthplacesTests.cs`.

## Core inherited stats
Status: **Implemented**  
Owner: `dynastia.stats`

The six core stats are Immunity, Longevity, Fertility, Appeal, Strength and Intellect. They are persisted person state and are read by multiple mechanics. Genetic inheritance and acquired improvement are separate concepts; paid improvements modify acquired/effective values without rewriting genetic ancestry.

Primary code: `plugins/Dynastia.Mechanics.Stats/`, `plugins/Dynastia.Mechanics.Reproduction/ReproductionYearSystem.cs`.

## Personality, Morals and sexuality
Status: **Implemented**  
Owner: `dynastia.personality`, `dynastia.relationships`

Rules:
- People receive a temperament/personality state and Morals.
- Personality/Morals can affect probability or eligibility only where a mechanic explicitly calls the shared influence/rule helpers.
- Morals can deteriorate/improve through relevant yearly events/actions; Religious Study resolves in the late `MoralsReflection` phase.
- Male-line sexuality is assigned by the Relationships system; the current base homosexuality roll is 0.5% where that assignment applies.
- Sexuality influences partner sex/relationship formation; it is not a global skill modifier.

Primary data: `data/LocalSociety/religious_study_church_rules.json` and personality source classes.

## Households and residence
Status: **Implemented**  
Owner: `dynastia.households`, `dynastia.economy`

Rules:
- A household is a persisted residential/economic unit, not simply “husband + wife + minor children.”
- Several generations/kin can live under one roof.
- Adult sons do not automatically leave at 18. They can remain residents until moving out, marriage/household transition or succession changes residence.
- Spouses normally co-reside; divorce, remarriage, orphaning, death, explicit move-out actions and historical migration can reorganize membership.
- The household head anchors assets/budget/controller logic, while other adult residents can remain full simulated people with careers and relationships.
- Overcrowding/childcare rules use current household membership and housing capacity.

Primary code: `plugins/Dynastia.Mechanics.Households/StandardHouseholdService*.cs`, `plugins/Dynastia.Mechanics.Economy/StandardEconomyService*.cs`.

Regression tests: `HouseholdKinshipRulesTests.cs`, `AdultSonsHouseholdReworkTests.cs`, `Development10HouseholdFamilyBalanceTests.cs`.

## Marriage and partner selection
Status: **Implemented**  
Owner: `dynastia.relationships`

Player actions include finding a spouse for the controller, arranging marriage for resident sons/daughters, reconciling a damaged marriage and divorcing.

Rules:
- Partner selection is a generated candidate flow rather than a single blind random spouse.
- Candidate identity draws on year, location/region, nationality/name culture and personality/age compatibility rules.
- Female spouses move to the male partner's location/household in the standard marriage flow; household reconciliation handles resulting residence changes.
- Marriage history persists after divorce/death/remarriage.
- Same-sex partnership behavior follows sexuality and partner-search rules where eligible.

Primary code/data:
- `plugins/Dynastia.Mechanics.Relationships/StandardPartnerSearchService.cs`
- `RelationshipsPlugin*.cs`
- `data/Relationships/relationship_eras.csv`
- `data/Relationships/relationship_event_variants.json`

Regression tests: `PartnerSearchRulesTests.cs`, `MarriageCareerEducationBalanceTests.cs`, `DivorceCustodyRulesTests.cs`.

## Marriage Satisfaction, affairs and divorce
Status: **Implemented**  
Owner: `dynastia.relationships`

Rules:
- Marriages carry persistent satisfaction rather than treating divorce/affairs as isolated dice rolls.
- Ordinary yearly repair is small (`+1` before penalties). Personality incompatibility, unemployment, imprisonment, illness, low attraction/fertility/intellect, household strain, financial pressure and poor family relations can reduce satisfaction.
- Affairs occur at a base 0.5% annual chance before personality adjustment. An affair applies a large satisfaction shock (`-25`) but does not force an immediate divorce by itself.
- Automatic divorce chance depends on low satisfaction: below 10 = 20%, below 20 = 10%, below 30 = 1.5%, below 40 = 0.5%, otherwise 0.
- Reconcile/repair is the active route to recover badly damaged marriages.
- Divorce uses shared breakup/custody/household consequence logic so children and former partners are moved/related consistently.

Primary code: `MarriageSatisfactionYearSystem.cs`, `MarriageBalanceRules.cs`, `AffairYearSystem.cs`, `MarriageDivorceYearSystem.cs`, `RelationshipBreakupService.cs`.

## Reproduction and births
Status: **Implemented**  
Owner: `dynastia.reproduction`

Rules:
- Standard childbirth is processed from a living male + living female spouse pair; the mother must be age 18–45 and not imprisoned/in active religious vocation.
- Base fertility chance uses the lower parental fertility: 0/5/7/10/12/16% for Fertility 0–5.
- Female fertility declines after 30 and receives an additional late-age reduction from 40 onward.
- `Try for Baby` multiplies the final chance by 5. A failed active attempt reduces Marriage Satisfaction by 2.
- Genetic stats are inherited with variation; newborn fertility has a 5% infertility override chance.
- Multiple birth chances are 4% twins and 1% triplets after conception in the implemented system.
- Birth conditions are data-driven and filtered/weighted by year, age and context rather than fixed hard-coded legacy thresholds.
- Newborn nationality, naming, appearance, birthplace/location and lineage/bloodline state are assigned through their owning services.

Primary code/data: `ReproductionYearSystem.cs`, `BirthConditionContextCatalog.cs`, `data/Common/birth_conditions.json`, `data/Health/birth_condition_context_weights.csv`.

Regression tests: `HealthContentReworkBatch1Tests.cs`, `LocalSocietyNonmaritalBirthBatch8Tests.cs` and reproduction-related integration tests.

## Nonmarital births
Status: **Implemented**  
Owner: `dynastia.reproduction` with Local Society rules

Nonmarital births are a separate annual system for eligible people and use data-driven rules in `data/LocalSociety/nonmarital_birth_rules.json`. Resulting children are normal simulated people and participate in family, household, inheritance and status systems according to recorded parentage/residence.

## Childhood, Happiness and Raise Child
Status: **Implemented**  
Owner: `dynastia.childhood`

Rules:
- Children have persistent Happiness affected by health, household circumstances and personality/context.
- `childhood.raise_child` is an annual queued action targeting an eligible child.
- Childhood outcomes can feed adult family-relation foundations when a child later establishes an independent household.
- UI intentionally hides adult-only details such as education/crafts/hobbies for very young children where those concepts do not yet apply.

Regression tests: childhood assertions across `Development10*`, `Development11*` and `FamilyRelationsRulesTests.cs`.

## Orphan care and adoption
Status: **Implemented**  
Owner: `dynastia.adoption`, households/family

Rules:
- Parent death can create orphan state/placement needs.
- Children may remain with surviving family, be placed with another suitable bloodline household or enter orphanage/orphan-care state depending on household availability.
- Adoption/orphan placement is resolved around inheritance/household reconciliation so residence and estate state remain coherent.

Primary code: `plugins/Dynastia.Mechanics.Adoption/AdoptionYearSystem.cs`.

## Family Relations
Status: **Implemented**  
Owner: `dynastia.family_relations`

Rules:
- Cross-household relatives have persistent familiarity/sympathy rather than a one-off support roll.
- Relations can be improved deliberately and react to family events such as crime, affairs/divorce, childhood handoff, weddings and support/refusal.
- Requests/transfers cover money, houses, farmland and career help.
- Family career connections require a sufficiently strong relation and are capped below the helper's own career level; they are not unlimited promotion shortcuts.
- Property requests consider real surplus/household need; households preserve essential residence/land rather than donating their last core asset.
- Successful major help such as receiving a spare house/farmland establishes a **Warm** relationship; a donor becoming Poor from generosity does not automatically end an already Warm/Close connection.
- Any eligible adult resident can be asked to move out. A spare-house route can guarantee relocation; a rental request can be refused and harm relations.

Primary code: `StandardFamilyRelationService.cs`, `FamilyRelationActions*.cs`, `FamilyRelationEventBridge.cs`.

Regression tests: `FamilyRelationsRulesTests.cs`, `Development10WorkCapacityRelationsHousingTests.cs`, `Development13ContinuationTests.cs`.

## Thoughts, hobbies and family narrative
Status: **Implemented**  
Owner: `dynastia.thoughts`, `dynastia.hobbies`, `dynastia.biography`

- Hobbies are personality/context flavor with some interaction hooks and heirloom/thought content.
- Thoughts are generated after the yearly simulation and drive the current thought/person emoji presentation.
- Biography records important structured events rather than being the authoritative state for mechanics.

Primary data: `data/Hobbies/`, event templates owned by their mechanics.
