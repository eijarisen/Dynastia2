> **Implementation status (D13-003):** The planned `docs/GameDesign/` structure has been created. This file is retained as the original rework plan; use `docs/GameDesign/00-Index.md` as the maintained reference.

# Dynastia — Game Design Documentation Rework Plan

## Purpose

The repository needs a game-design layer that future design and implementation chats can use without reconstructing mechanics from conversation history or reading the whole codebase.

The documentation should separate three different truths:

1. **Implemented behavior** — what the current repository actually does.
2. **Approved design** — decisions that are intended to be implemented but may not exist in code yet.
3. **Implementation ownership** — where the responsible code, data and tests live.

`docs/DevelopmentMap.md` already serves the third purpose. The rework should add a compact, modular game-design reference for the first two.

## Source-of-truth order

Future chats should use this order when resolving conflicts:

1. The user's newest explicit instruction.
2. Approved design entries marked **Approved / Not implemented**.
3. Current repository behavior for entries marked **Implemented**.
4. Older design notes and conversation summaries.

A design document must never silently describe planned behavior as already implemented.

## Proposed structure

Create `docs/GameDesign/` with the following files.

### `00-Index.md`

The entry point for future chats.

Contains:

- one-paragraph game premise;
- current design baseline date/repository identifier;
- terminology: Bloodline, Male Lineage, Household, Controller, Town, Region, Institution, Renown, Reputation;
- source-of-truth rules;
- links to every domain document;
- a compact mechanics index mapping each feature to its owning plugin and UI surface;
- a short list of approved-but-not-yet-implemented changes.

A future chat should be able to read this file first and know where to look next.

### `01-Core-Loop-and-Control.md`

Owns:

- New Game and starting year;
- annual action requirement and Pass;
- selected person versus active controller;
- autonomous bloodline households;
- year-resolution phases at design level;
- succession and game-over states;
- Chronicle, Biography, Thoughts, Genealogy and Map roles;
- save/load expectations.

### `02-People-Family-and-Relationships.md`

Owns:

- Family, Bloodline and Male Lineage;
- households and residence rules;
- marriage, candidate selection, satisfaction, affairs, divorce and remarriage;
- reproduction and nonmarital births;
- inherited stats and appearance;
- childhood Happiness and Raise Child;
- orphan care/adoption;
- Family Relations and move-out rules;
- personality, Morals and sexuality;
- nationality, name culture, birthplaces and surname assimilation.

### `03-Health-Wellbeing-and-Life-Course.md`

Owns:

- Aging;
- Health and conditions;
- Stress and mental health;
- treatment, therapy, recovery and alcohol;
- paid stat improvements;
- work capacity;
- Mortality and death consequences.

### `04-Work-Education-Crafts-and-Status.md`

Owns:

- Education and local school access;
- careers, vacancies, experience, advancement, job loss and retirement;
- Job Satisfaction and Work Harder/Recover interactions;
- Crafts, Mastery, teaching and self-employment;
- artistic work and royalties;
- Farming labour as work;
- Religious Vocation;
- Renown, local Renown and Reputation.

### `05-Households-Economy-and-Property.md`

Owns:

- household income/expenses;
- lifestyle stance;
- houses, rent, extensions and relocation;
- farmland, farm types, livestock and farming income;
- loans, receivables and repayment;
- Family Inventory;
- inheritance of cash, houses, farmland, heirlooms and financial claims;
- heirloom creation, value, sale and income.

### `06-Towns-Institutions-and-Community.md`

Owns:

- historical towns, regions and coordinates;
- Town Affairs;
- institutions and facility quality;
- prosperity and local/regional economic strengths;
- local housing and jobs;
- healthcare and education services;
- Bank, Church and Court;
- Community proposals and temporary policies;
- civic offices;
- community connections;
- relocation consequences.

### `07-Justice-Events-and-History.md`

Owns:

- ordinary crime;
- criminal occupation / Life of Crime;
- prison, bail, escape and criminal records;
- Rare Events;
- Historical Events;
- historical migration and external residence;
- town/polity changes relevant to gameplay.

### `08-Actions-and-UI-Surfaces.md`

A compact action/UI catalogue rather than prose gameplay explanation.

For every player-facing action record:

- action ID;
- current label(s) by era where relevant;
- owning plugin;
- target type;
- queue/immediate behavior;
- main UI entry point;
- design-document section owning the rule.

Also list the primary UI surfaces and their purpose: Main Window, Town Affairs, Family Inventory, Family Relations, partner selection, job opportunities, property selection, loan selection, self-improvement, Map and Genealogy.

### `09-Design-Decisions.md`

An append-only decision log for future design chats.

Each entry should use:

```text
ID: GD-YYYY-NNN
Status: Proposed | Approved | Implemented | Superseded
Date:
Area:
Decision:
Reasoning:
Affected mechanics:
Implementation notes:
Save compatibility:
Documentation to update:
Supersedes:
```

Once implemented, the durable rule belongs in the relevant domain document. The decision log keeps the history of why it changed.

## Standard mechanic section format

Every mechanic in the domain documents should use the same compact structure:

```text
## Mechanic name
Status: Implemented | Approved / Not implemented
Owner: plugin/project
Player-facing purpose:
Authoritative state:
Rules:
Player actions:
Annual/autonomous behavior:
Interactions:
Historical/local variation:
Persistence/save notes:
Primary code/data:
Regression tests:
Open design notes:
```

This format gives future chats enough information to design or implement a change without embedding code dumps in the documentation.

## What belongs in game-design documentation

Include exact rules when they are needed to reproduce gameplay correctly:

- formulas and probability rules;
- eligibility and targeting rules;
- annual phase/timing when it changes outcomes;
- ownership and residence transitions;
- inheritance and persistence behavior;
- historical/local modifiers;
- important cross-system interactions;
- intentional caps, floors and exceptions.

These details are for developers and future design chats. They should **not** all be copied into the player Instructions window.

## What does not belong there

Avoid:

- large source-code excerpts;
- exhaustive narrative/event text pools;
- generated data tables that can be read directly from CSV/JSON;
- visual layout measurements unless they are gameplay-relevant;
- transient implementation bugs unless they are deliberately preserved;
- duplicated rules in several domain files.

Use links to code/data instead.

## Relationship to existing documentation

### Keep `README.md`

Continue using it for the technical project overview and build/run commands. Do not turn it into a game-design document.

### Keep and expand `docs/DevelopmentMap.md`

It remains the implementation responsibility map. Add new systems when ownership changes, but avoid copying gameplay rules into it.

### Replace conversation-dependent design history

Important decisions from Designs/Development chats should be migrated into `09-Design-Decisions.md` only when they still matter. Do not attempt to preserve every conversation message.

### Player Instructions stay separate

`InstructionsWindow.axaml` is a short player orientation guide. It should mention every gameplay system but avoid exact formulas, hidden thresholds and balance values. Detailed current requirements belong in action descriptions, tooltips and the game UI.

## Migration plan

### Pass 1 — inventory

Build the mechanics index from the current plugins, services, action registry and UI windows. Mark each feature as implemented.

### Pass 2 — domain extraction

Populate the seven domain documents from current code/data. Reuse the repository's terminology and preserve exact mechanics where implementation depends on it.

### Pass 3 — reconcile recent design work

Review approved Designs chat decisions against the repository. Anything already implemented becomes the documented current rule. Anything still planned is entered as **Approved / Not implemented** and linked from the index.

### Pass 4 — tests and ownership links

For each mechanic, record its main source files/data and the regression tests that protect it. Missing test coverage becomes visible immediately.

### Pass 5 — maintenance rule

Every future implementation package that changes gameplay should update:

- the owning domain document;
- `09-Design-Decisions.md` when it implements or supersedes a recorded decision;
- `08-Actions-and-UI-Surfaces.md` if an action or player-facing surface changes;
- `InstructionsWindow.axaml` only when the player needs new high-level guidance.

## Future-chat workflow

A future design chat should normally read:

1. `docs/GameDesign/00-Index.md`;
2. the one or two relevant domain documents;
3. `docs/DevelopmentMap.md` for code ownership;
4. current source only where implementation details need verification.

A future implementation chat should additionally inspect the listed source/data/tests for the affected mechanic. This keeps context small while preserving design accuracy.

## Completion criterion

The rework is complete when a new chat can answer all of the following without reconstructing prior conversations:

- What is the intended rule?
- Is it already implemented?
- Which plugin owns it?
- Which other systems interact with it?
- Where are its data and UI entry points?
- What save compatibility matters?
- Which tests protect it?
- Which approved changes are still outstanding?
