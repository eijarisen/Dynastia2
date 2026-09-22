# Dynastia — Game Design Index

Status: **Implemented documentation baseline**  
Baseline: **D13-002 / 2026-09-22 / commit `de304ac`**

Dynastia is a historical family-dynasty simulation. The player advances the family one year at a time, directs the currently playable male-line households, develops people and property, and reacts to local institutions, family relationships, health, careers, social status, crime and historical change. The playable line must remain alive, locally resident in Poland and outside an active religious vocation.

## Source-of-truth order

When sources disagree, use this order:

1. The user's newest explicit instruction.
2. A design entry explicitly marked **Approved / Not implemented** in this documentation.
3. Current repository behavior for entries marked **Implemented**.
4. Older design notes, package notes and conversation summaries.

Do not describe a planned rule as implemented unless the repository contains it.

## Core terminology

| Term | Current meaning |
|---|---|
| **Bloodline** | The wider dynasty-descended family, represented by `family.bloodline`. It continues through both sons and daughters and drives family/genealogy inclusion. |
| **Male Lineage** | The direct playable succession line, represented by `lineage.male`. Male-line descendants inherit this line; it is narrower than Bloodline. |
| **Household** | The persisted residential/economic unit used for wealth, property, budget, dependants and household-level actions. Several generations/branches can share one household. |
| **Controller** | The currently active playable person. A controllable person must be alive, male, Male Lineage, age 18+, locally resident, not in active religious vocation and the head of a household. |
| **Town** | A time-aware settlement from the historical town catalogue. Town identity, name/status/polity, population and institutions can change with year. |
| **Region** | The broader economic/geographic grouping used for opportunities, nationality weights and local-context rules. |
| **Institution** | A town service/facility such as medical care, school, bank, Church or Court. Availability/tier affects Town Affairs actions and local mechanics. |
| **Renown** | Visibility/prominence. It is part of the Status system and can be local or household/person-oriented depending on the source of the effect. |
| **Reputation** | Social standing/trust. It can affect welfare, civic/social outcomes and other systems that explicitly query Status. |

## Domain documents

- [01 — Core Loop and Control](01-Core-Loop-and-Control.md)
- [02 — People, Family and Relationships](02-People-Family-and-Relationships.md)
- [03 — Health, Wellbeing and Life Course](03-Health-Wellbeing-and-Life-Course.md)
- [04 — Work, Education, Crafts and Status](04-Work-Education-Crafts-and-Status.md)
- [05 — Households, Economy and Property](05-Households-Economy-and-Property.md)
- [06 — Towns, Institutions and Community](06-Towns-Institutions-and-Community.md)
- [07 — Justice, Events and History](07-Justice-Events-and-History.md)
- [08 — Actions and UI Surfaces](08-Actions-and-UI-Surfaces.md)
- [09 — Design Decisions](09-Design-Decisions.md)

Implementation ownership remains in [`../DevelopmentMap.md`](../DevelopmentMap.md). These files describe gameplay/design; `DevelopmentMap.md` tells maintainers where code is concentrated.

## Mechanics index

| Mechanic/plugin | Domain | Main player surface |
|---|---|---|
| `dynastia.family` | 02 | New Game, Family, details, Genealogy |
| `dynastia.aging` | 03 | Person details, annual turn |
| `dynastia.appearance` | 02 | Family/person portraits |
| `dynastia.stats` | 02 | Person details, About/skills |
| `dynastia.personality` | 02 / 03 | Person details, Church/Town Affairs |
| `dynastia.households` | 05 | Household panel, Family Inventory, property actions |
| `dynastia.economy` | 05 | Household budget, Family Inventory |
| `dynastia.family_relations` | 02 | Family Relations window |
| `dynastia.family_support` | 02 | Compatibility/legacy family-support actions |
| `dynastia.relationships` | 02 | Actions, partner selection, family details |
| `dynastia.reproduction` | 02 | Actions, births, Chronicle |
| `dynastia.childhood` | 02 | Child details/actions |
| `dynastia.adoption` | 02 | Household/orphan transitions |
| `dynastia.health` | 03 | Person Health, Town Affairs Health |
| `dynastia.wellbeing` | 03 | Actions, Town Affairs Health |
| `dynastia.mortality` | 03 | Annual turn, Chronicle |
| `dynastia.career` | 04 | Actions, Jobs, job opportunities |
| `dynastia.education` | 04 | Education details, Town Affairs Education |
| `dynastia.crafts` | 04 | Craft details/actions |
| `dynastia.farming` | 04 / 05 | Household work, Family Inventory, Town Affairs Housing |
| `dynastia.hobbies` | 02 / 04 | Person details, Thoughts |
| `dynastia.status` | 04 / 06 | Person/household status, Town Affairs Community |
| `dynastia.heirlooms` | 05 | Family Inventory, Chronicle |
| `dynastia.loans` | 05 / 06 | Town Affairs Bank, Family Inventory |
| `dynastia.inheritance` | 05 | Annual turn, Family Inventory |
| `dynastia.locations` | 06 | Map, town selection, local-context mechanics |
| `dynastia.townlife` | 06 | Town Affairs |
| `dynastia.church` | 06 | Town Affairs Church |
| `dynastia.community` | 06 | Town Affairs Community |
| `dynastia.religious_vocation` | 04 / 06 | Town Affairs Church, person state |
| `dynastia.stat_improvements` | 03 / 06 | Town Affairs Health / self-improvement |
| `dynastia.justice` | 07 | Town Affairs Court, actions, person justice state |
| `dynastia.rare_events` | 07 | Chronicle, annual simulation |
| `dynastia.historical` | 07 | Chronicle, historical state/effects |
| `dynastia.biography` | 01 / 02 | Biography |
| `dynastia.thoughts` | 01 / 02 | Thoughts/person emoji |
| `dynastia.succession` | 01 | Playable household tabs, game over |
| `dynastia.turn_actions` | 01 | Actions / Pass |

## Approved but not yet implemented

None are carried into this baseline by the documentation-rework package. Future approved work should be recorded in [09-Design-Decisions.md](09-Design-Decisions.md) with `Status: Approved` until implementation lands.

## Maintenance rule

A gameplay patch should update the owning domain document when it changes a durable rule. Update `08-Actions-and-UI-Surfaces.md` when an action ID/label/surface changes. Add or update a decision-log entry when a recorded design decision is implemented, superseded or materially revised. Player-facing `InstructionsWindow.axaml` should remain qualitative and compact; exact formulas and hidden thresholds belong here or in code/data.
