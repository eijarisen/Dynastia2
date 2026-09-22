# Towns, Institutions and Community

## Historical towns, regions and coordinates
Status: **Implemented**
Owner: `dynastia.locations`

Rules:
- Towns use permanent IDs with year-dependent historical identity/status/polity data rather than treating the modern snapshot as timeless.
- Town catalogue entries supply coordinates, county/region, population snapshots and historical availability/ownership/status information.
- Regions carry opportunity/economic tags used by Careers/Crafts/Farming/other contextual systems.
- Co-located/adjacent settlement records inherit usable local services/opportunities from the dominant/largest settlement when appropriate, avoiding “same map point but no institutions” dead zones.
- Foreign-birthplace generation uses a separate city/country catalogue and does not require full foreign-town simulation.

Primary data: `data/Towns/dynastia-towns.json`, `opportunity_tags.csv`, `region_opportunities.csv`, `town_opportunities.csv`, `data/Locations/`.

Regression tests: `HistoricalTownCatalog*Tests.cs`, `TownEconomicIndexTests.cs`, `Development11LocalityRecoverForeignBirthplacesTests.cs`.

## Town Affairs
Status: **Implemented**
Owner: `dynastia.townlife` plus application Town Affairs projection

Purpose: one local hub for settlement information and place-dependent actions.

Current tab order:
1. Institutions
2. Community
3. Housing
4. Jobs
5. Health
6. Church
7. Education
8. Bank
9. Court

Town header presents polity, region, settlement/population context and the current mayor with approval under Population.

Remote/non-home-town views restrict tabs/actions that require the controlled household to be locally present; institution/housing browsing remains available where designed.

Primary UI/code: `TownLifeWindow.axaml*`, `TownAffairsViewModel.cs`, `MainWindowViewModel.TownLife.cs`.

Regression tests: `TownLifeBatch*Tests.cs`, `Development11TownAffairsPresentationTests.cs`, `Development13ContinuationTests.cs`.

## Institutions and facility quality
Status: **Implemented**
Owner: `dynastia.townlife`

Rules:
- Institution type/tier is inferred/overridden from town, population, historical year and explicit data.
- Institution cards are compact, route to their owning Town Affairs tab and expose service quality rather than embedding duplicate mechanics.
- Medical, school, bank, Church, Court and career/craft requirements query institution services.

Primary data: `data/TownLife/institution_types.csv`, `institution_tier_names.csv`, `institution_inference_rules.csv`, `institution_overrides.csv`.

## Prosperity and local economy
Status: **Implemented**
Owner: TownLife/Locations/Economy/Historical

Rules:
- Town prosperity is persisted/derived through TownLife rules and can change annually/historically.
- Prosperity/local/regional economic strengths feed job/property/service contexts rather than directly replacing their owning mechanic's calculations.
- Historical events can modify prosperity through data-driven effects.

Primary data: `data/TownLife/prosperity_rules.json`, `historical_event_prosperity_effects.csv`.

## Housing and local property market
Status: **Implemented**

Town Affairs Housing presents current individual house offers and local property transactions. Purchase values are local-market based. Existing owned property is managed in Family Inventory; relocation/property selection remains a separate flow.

## Jobs
Status: **Implemented**

Town Affairs Jobs shows local opportunities with career title, salary/chance presentation and local/regional support. Job application logic remains owned by Career/Locations and uses the active controller/selected target rules.

## Health
Status: **Implemented**

Town Affairs Health is the entry point for local treatment, Therapy and paid medical stat improvements.

Presentation rules:
- actions whose town/facility eligibility is not met are omitted;
- actions blocked only by insufficient household funds stay visible but disabled;
- empty Therapy/Improvement sections are hidden;
- no duplicate “Current Health / Conditions” header;
- local medical-care cost/effect multipliers are not displayed directly.

## Education
Status: **Implemented**

Town Affairs Education shows local school/education access and routes to Education actions. Craft learning information can report related regional-industry support.

## Bank
Status: **Implemented**

Town Affairs Bank owns loan-offer entry points. Offers come from Loans + local/historical banking rules. Favorability is displayed in bold, color-coded qualitative text instead of raw multiplier/percentage details.

## Church
Status: **Implemented**
Owner: `dynastia.church`, Personality, Religious Vocation

Available Church mechanics include:
- `church.attend`
- `church.donate`
- `church.aid_poor_family`
- `church.ask_welfare`
- `personality.religious_study`
- religious-vocation entry points when eligible.

`Attend Church` and `Religious Study` are deliberately **not** shown in the standard Actions list; they remain accessible from Town Affairs → Church.

Welfare is means/reputation constrained and once-per-household/year; donations/poor relief can affect Morals/Status according to `church_rules.json`.

Regression tests: `LocalSocietyChurchBatch2Tests.cs`, `LocalSocietyReligiousCallingsBatch12Tests.cs`, `Development13ContinuationTests.cs`.

## Court and justice services
Status: **Implemented**
Owner: Justice, surfaced by TownLife

Town Affairs Court exposes local justice state/actions such as bail and escape for eligible imprisoned targets. Family legal/law-enforcement connections can create protection that reduces sentences/detection within the explicit Court rules.

Primary data: `data/LocalSociety/court_justice_rules.json`.

## Community policies
Status: **Implemented**
Owner: `dynastia.community`

Rules:
- Towns can have proposals/temporary policies that affect local mechanics through the Community service.
- `community.lobby_policy` lets eligible controllers lobby.
- Policy resolution is intentionally performance-bounded: full policy simulation runs for towns containing playable lineage households and for towns with an outstanding player lobby; irrelevant towns are skipped rather than fully simulated one by one.
- Policy effects are data-driven and temporary according to their definitions.

Primary data: `data/LocalSociety/community_policies.csv`, `community_policy_rules.json`.

Regression tests: `LocalSocietyCommunityBatch3Tests.cs`, `Development13ContinuationTests.cs`.

## Civic office
Status: **Implemented**
Owner: `dynastia.community`, Career/Status

Rules:
- The current town head/mayor is represented through the civic-office service and public-administration career integration.
- Civic-office appointment is restricted to Polish nationality; generated NPC mayors are Polish as well.
- Town Affairs shows the mayor's name and current approval.
- An eligible controller who holds the simulated town office can use `community.perform_office_duties` from Town Affairs.
- Office appointment/loss, approval and duties affect local/status state through Community rules.

Primary data: `data/LocalSociety/civic_office_rules.json`, `civic_office_profiles.csv`.

Regression tests: `LocalSocietyCivicOfficeBatch4Tests.cs`, `Development13ContinuationTests.cs`.

## Community connections
Status: **Implemented**
Owner: `dynastia.community`

Rules:
- Non-family local acquaintances have persistent connection relation state/archetypes.
- Actions include improving relations, sending/requesting money and giving/requesting house/farmland.
- Major accepted help (house/farmland) moves the relation to Warm rather than terminating the acquaintance after the transfer.
- Warm/Close contacts remain valid even if a generous transfer leaves the donor Poor; only the explicit connection-retention rules should end the relationship.

Primary data: `data/LocalSociety/connection_archetypes.csv`, `connection_rules.json`.

Regression tests: `LocalSocietyConnectionsBatch5Tests.cs`, `Development13ContinuationTests.cs`.
