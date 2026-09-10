# Dynastia 4 — Functional Game Specification

## 1. Scope

This document specifies the behavior of the game implemented in `Dynasty 4.html`.

The specification covers:

* game objective and end condition;
* game state and character data;
* initial game creation;
* yearly simulation order;
* households and finances;
* character statistics;
* education and careers;
* relationships, marriage, divorce, affairs, and remarriage;
* fertility, children, genetics, and birth conditions;
* health, illness, aging, and death;
* crimes and imprisonment;
* houses and nannies;
* inheritance;
* controllable dynasty members;
* bloodline/genealogy tracking;
* event history and biographies;
* save/load behavior;
* the purpose of every major informational section and control;
* implementation quirks that affect observable gameplay.

The game begins in **1900** and advances in discrete one-year turns. The player guides a dynasty whose playable succession is restricted to a direct father-to-son male lineage. A broader `bloodline` concept is tracked for genealogy and family display.

---

# 2. Core game concept

The player manages a family dynasty indefinitely.

The central continuity rule is:

**The game ends when no living male character remains whose lineage is `male-lineage`.**

Age does not matter for this test. A surviving male-lineage infant prevents game over even though he cannot yet be controlled.

The player can directly control any character who simultaneously satisfies all of the following:

* alive;
* male;
* `lineage == "male-lineage"`;
* age 18 or older.

Only one household head is active for issuing actions at a time.

Female descendants and descendants through daughters can remain members of the broader family bloodline, appear in family history and genealogy, and continue reproducing, but they do not keep the playable male line alive.

---

# 3. Important terminology

## Bloodline

`isBloodline` represents genealogical descent from the dynasty.

The founder is a bloodline member.

A newborn receives:

`isBloodline = father.isBloodline OR mother.isBloodline`

Therefore the bloodline can continue through sons and daughters.

Bloodline status is primarily informational. It determines which relatives appear in the broader family and genealogy views.

## Male lineage

`lineage == "male-lineage"` represents the playable agnatic line.

When a child is born:

* a son inherits his father's `lineage`;
* a daughter always receives `lineage = "other"`.

Consequently, only sons of a male-lineage father remain male-lineage.

## Controllable character

A controllable character is a living adult male-lineage male.

## Household head

A male-lineage adult male owns the household-level economic fields and acts as the player's controllable family unit.

## Household

A household consists, for economic purposes, of:

* the male-lineage household head;
* his living current spouse;
* his children under 18;
* his adult unmarried daughters.

Adult sons form their own economic households.

Married daughters leave their father's household.

---

# 4. Global game state

The main game state contains:

| Field              | Meaning                                                      |
| ------------------ | ------------------------------------------------------------ |
| `surname`          | Dynasty surname entered/generated at the start.              |
| `year`             | Current simulation year. Starts at 1900.                     |
| `family`           | Array of every `Person` object created during the game.      |
| `selectedPersonId` | Person currently selected for inspection/contextual actions. |
| `nextId`           | Sequential ID counter for newly created people.              |
| `events`           | Year-indexed Family Album events.                            |
| `albumYear`        | Year currently being examined in the Family Album.           |
| `familyTreeView`   | `"living"` or `"deceased"`.                                  |
| `detailsView`      | Current character information subsection.                    |
| `householdTabView` | ID of currently active household head.                       |
| `isDynastiaSave`   | Marker identifying a valid save.                             |
| `isHealthFrozen`   | Debug flag controlling health simulation.                    |

---

# 5. Character data model

Every simulated person is a `Person`.

## Identity and genealogy fields

Every person has:

* `id`
* `name`
* `surname`
* `maidenName`
* `gender`
* `age`
* `birthYear`
* `birthMonth`
* `birthDay`
* `deathYear`
* `deathMonth`
* `deathDay`
* `lineage`
* `isBloodline`
* `generation`
* `parentIds`
* `childrenIds`
* `spouseId`
* `marriageHistory`

Female characters receive their original surname as `maidenName` at construction.

Birth month is randomly 1–12.

Birth day is randomly 1–28.

`birthYear = currentGameYear - startingAge`.

## Gameplay fields

Every person also has:

* `isAlive`
* `isControllable`
* six statistics;
* `health`
* `maxHealth`
* `healthConditions`
* `jobLevel`
* `jobTitle`
* `educationLevel`
* `jobSatisfaction`
* `sexuality`
* `prisonSentence`
* `queuedAction`
* `pendingInheritance`
* `lastIncome`
* `biography`
* `isNanny`

Generated spouses may additionally have text-only family-history fields:

* `fatherName`
* `motherName`
* `siblingsNames`

Male-lineage males also receive household property:

* `householdWealth`
* `housesOwned`
* `pendingHouses`
* `rentedHouses`
* `nannyId`

All male-lineage males receive `householdWealth = 0` when constructed, including minors.

---

# 6. Six character statistics

The game uses six hereditary statistics.

Normal values are **1 through 5**.

Fertility can additionally become **0** through inherited infertility.

## Immunity

Influences illness probability and some health penalties.

Higher is better.

## Longevity

Provides annual natural health recovery and determines expected natural lifespan.

Higher is better.

## Fertility

Controls the probability of producing children.

0 means infertile.

## Appeal

Controls marriage probability for male-lineage characters.

## Strength

Controls success when seeking basic employment.

## Intellect

Controls:

* active education;
* passive childhood education;
* normal career promotion;
* Work Harder promotion bonuses.

The original instructions describe these six statistics as inherited traits that may mutate slightly.

---

# 7. Starting a new game

## Surname

The player supplies a dynasty surname.

Input permits:

* Latin letters;
* Polish characters;
* spaces.

Other characters are removed.

The first character is capitalized and the remainder lowercased.

If the resulting surname is shorter than two characters, a surname is chosen randomly from the built-in surname pool.

## Initial deceased parents

Two deceased parents are generated.

### Father

* random male first name;
* dynasty surname;
* Male;
* age stored as 45;
* dead;
* death year 1899;
* generation 0;
* `isBloodline = true`;
* normal random adult statistics/job data.

### Mother

* random female first name;
* dynasty surname;
* Female;
* age stored as 42;
* dead;
* death year 1899;
* not explicitly marked bloodline.

They are linked as spouses.

Their marriage history is recorded as:

* start year: 1880;
* end year: 1899;
* end reason: death.

## Founder

The founder is created as:

* random male first name;
* dynasty surname;
* Male;
* age 18;
* `lineage = "male-lineage"`;
* `isBloodline = true`;
* controllable;
* generation 1;
* parents = the initial father and mother;
* education level randomly 1 or 2;
* household wealth = $1,000;
* houses owned = 1.

Because he is already 18 at construction, his `jobLevel` is randomly generated between 0 and 3.

His other statistics are random from 1–5.

His initial biography contains:

* his birth;
* becoming an orphan in 1899 at age 17;
* becoming an adult in 1900.

The founder begins selected and is the active household head.

---

# 8. Informational sections and controls

This section describes purpose only, not layout.

## Start section

Contains:

### Surname field

Defines the dynasty surname.

### Found Dynasty

Creates the initial family and starts a new simulation.

### Load Game

Loads an exported save instead of creating a new family.

### Attribution text

The source contains the static attribution:

“Copyright 2025 by Piotr Chojnacki.”

---

## Instructions section

The instructions explain:

### Goal

Guide the dynasty for as long as possible. The game ends when the final male heir in the direct lineage dies.

### Core Gameplay

Time advances one year at a time.

### Households

The dynasty is split into households headed by male heirs.

### Actions

Actions are selected for the active household head and usually execute on the following year.

### Stats

Explains the six inherited statistics.

### Wealth

Explains household budgets, income, expenses, houses, rent, and giving houses to sons.

### Debug: Freeze Health at 100

Toggles `isHealthFrozen`.

The exact implementation has additional side effects described later.

### Close

Closes the instructions section.

---

## Main year controls

### Current Year

Displays `gameState.year`.

### Next Year

Runs one complete yearly simulation.

### Enter key

Performs the same function as Next Year when:

* the game has started;
* the game-over section is not open;
* the instructions section is not open.

### Instructions

Opens the instructions.

### Export

Exports the current game.

### Load

Loads a save during an existing game.

---

## Actions section

Displays actions available to the active controllable household head.

Some actions apply to the head.

Other actions depend on which family member is selected.

Only one queued action may be attached to a household head at a time.

When an action is queued, the action section displays the queued action and offers:

### Cancel Action

Removes the queued action before the next year.

---

## Family Album

Stores notable events by year.

Functions:

* show events from a selected year;
* move one year backward;
* move one year forward, up to the current year.

If a year contains no events, it reports that nothing notable occurred.

---

## Family section

Has two logical views.

### Living

Shows the living bloodline family and spouses.

Also provides the male-lineage household selectors.

### Deceased

Shows deceased bloodline relatives and spouses.

### View Tree

Opens the full genealogy representation.

---

## Household selector

Adult male-lineage heads are grouped by generation.

Selecting a household head:

* makes him the active action actor;
* selects him for character details.

A deceased male-lineage head may remain represented when he has a living underage son, allowing the orphaned household to remain accessible.

---

## Character details

Four subsections exist.

### General

Shows:

* age;
* gender;
* education;
* occupation/status;
* job satisfaction when applicable;
* birth date;
* death date when dead;
* maiden name for women;
* income or pension;
* alive/deceased status.

### About

Generates prose based on:

* parents;
* birth order;
* immunity;
* longevity;
* fertility;
* appeal;
* strength;
* intellect.

### Relationships

Shows:

* father;
* mother;
* siblings;
* spouses;
* children grouped by the other parent.

### Biography

Shows significant recorded events in reverse chronological order.

Character statistics and current health are also presented as character information independent of these tabs.

---

## Genealogy Tree

Provides an informational family tree beginning with the founder.

It follows bloodline descendants through both sexes and associates children with the appropriate spouse/union.

Selecting a person in the genealogy selects that person in the main game.

---

## Game Over section

Appears when no living male-lineage male remains.

It reports:

* dynasty collapse;
* the year in which the male line ended.

### Go Back

Hides the game-over message so the player can inspect the finished game.

It does not re-enable Next Year.

### Start Anew

Reloads the game and begins from the start.

---

# 9. Job system

## Job levels

| Level | Job        |
| ----: | ---------- |
|     0 | Unemployed |
|     1 | Laborer    |
|     2 | Clerk      |
|     3 | Manager    |
|     4 | Director   |
|     5 | Magnate    |

Special titles override normal titles.

The title resolution order is:

1. Nanny, if `isNanny`.
2. Imprisoned, if prison sentence is above 0.
3. Retired, if retirement age is reached.
4. Housewife, if Female, job level 0, and has children.
5. Preschool, if younger than 6.
6. Student, if younger than 18.
7. Otherwise normal level title.

Men retire at 65.

Women retire at 60.

---

# 10. Job satisfaction

New people receive a random job satisfaction from 1–5.

| Value | Description |
| ----: | ----------- |
|     1 | Miserable   |
|     2 | Unhappy     |
|     3 | Content     |
|     4 | Satisfied   |
|     5 | Thriving    |

Job satisfaction does not naturally change every year.

It changes through actions such as:

* Recover;
* Drink;
* Ask to Recover.

An employed person with satisfaction 1 suffers a health penalty each year.

---

# 11. Economy constants

* Base salary per job level: **$500/year**
* Living expense per household member: **$250/year**
* House price: **$10,000**
* Rental income per rented house: **$250/year**
* Rent expense when the household owns no house: **$250/year**
* Nanny expense: **$250/year**
* Pension: **10% of the person's salary immediately before retirement**

Other action costs:

* Education: **$2,000**
* Therapy: **$1,500**
* Healing: **$1,000**
* Healing restores: **25 health**

---

# 12. Household finances

Finances are calculated once each year before health and death.

## Household income

For a living male-lineage adult head:

`income = head income + spouse income + adult unmarried daughter income + rental income`

### Head income

If employed:

`jobLevel × 500`

If retired:

`lastIncome × 0.10`

Otherwise:

`0`

### Spouse income

Calculated with the same employed/pension formula.

### Adult unmarried daughters

Every living daughter who is:

* age 18+;
* unmarried;

contributes:

`jobLevel × 500`

if employed.

### Rental income

`rentedHouses × 250`

---

## Household expenses

Included household members are:

* head;
* living spouse;
* each living child younger than 18;
* each living adult unmarried daughter.

Living cost:

`memberCount × 250`

Additional expenses:

* +250 if the head owns zero houses;
* +250 if `nannyId` is set.

The nanny does not herself add another $250 standard household living-cost unit.

Final yearly update:

`householdWealth += income - expenses`

If this produces negative wealth:

`householdWealth = 0`

Debt is therefore impossible.

---

# 13. Household after the father's death

A deceased male-lineage head remains economically relevant if he has living children under 18.

If his spouse survives:

* spouse income continues;
* surviving spouse and underage children incur standard living costs;
* nanny expense continues if applicable;
* the result is added to `head.pendingInheritance`;
* pending inheritance cannot fall below zero.

If no spouse survives but underage children exist:

* no income is calculated;
* nanny expense is deducted from `pendingInheritance` if `nannyId` exists;
* pending inheritance is clamped to zero.

---

# 14. Exact yearly simulation order

Simulation order must be preserved for high fidelity.

When Next Year is invoked:

## Step 1 — Increment year

`year += 1`

The Family Album is moved to the new current year.

A snapshot array of all currently living people is created.

---

## Step 2 — Aging

Every person in that living snapshot gains exactly one year of age.

---

## Step 3 — Capture queued actions

For each living controllable character with a queued action:

* copy the action into this year's action-modifier collection;
* clear the person's `queuedAction`.

Actions can therefore affect later phases of the same annual turn.

---

## Step 4 — Execute early actions

These actions execute before household finances:

* Heal;
* Recover;
* Get Education;
* Go to Therapy;
* Drink;
* Hire Nanny;
* Fire Nanny;
* Help Seek Employment;
* Ask to Recover;
* Ask to Quit Job.

The effects are specified in the Actions section below.

---

## Step 5 — Calculate finances

All living adult male-lineage household heads process income and expenses.

Then estates of deceased heads with underage children process surviving-spouse finances.

---

## Step 6 — Health/status processing

For every person who was living at the beginning of the year:

* adulthood events;
* pending inheritance/houses at adulthood;
* sexuality roll for male-lineage men reaching adulthood;
* passive education;
* retirement;
* prison countdown;
* annual health change;
* condition effects/expiration;
* possible new illness.

---

## Step 7 — Death processing

For each person from the initial living snapshot:

* accident check;
* age/terminal-condition death probability;
* death if health ≤ 0 or death roll succeeds;
* grief effects on relatives.

---

## Step 8 — Life events

A fresh list of people who are still alive is created.

For each such person, process:

* deferred player actions;
* crime/prison divorce;
* firing/promotion;
* marriage;
* affair;
* childbirth;
* female remarriage;
* job-title refresh.

Deferred actions include:

* Quit Job;
* Divorce Spouse;
* Work Harder modifier;
* Find Spouse modifier;
* Try for Baby modifier;
* Seek Employment;
* Ask Parents for Money;
* Ask Child for Money.

---

## Step 9 — Inheritance

Deaths that occurred this year trigger:

* house inheritance;
* estate settlement.

---

## Step 10 — Update control and game-over state

Recalculate every person's `isControllable`.

If the current household head died, select another controllable male if one exists.

Check whether any living male-lineage male remains.

---

## Step 11 — Refresh displayed game information

All informational sections are regenerated from state.

---

# 15. Player actions

Unless otherwise noted, these actions are **queued** and resolved when Next Year is processed.

If the active household head already has a queued action, other action choices are hidden until the action is cancelled or processed.

An imprisoned head cannot perform actions.

---

## Recover

Availability:

* selected person must be the active household head;
* head must not be retired.

Effects:

* job satisfaction +1, maximum 5;
* health receives +20 during health processing;
* a random recovery-activity biography/event is generated.

The source tooltip claims the character receives no salary during this year.

**Actual implementation still pays normal salary.**

---

## Find a Spouse

Availability:

* active head selected;
* unmarried;
* age 18+.

Effect:

Multiplies that year's normal marriage probability by **5**.

---

## Try for a Baby

Availability:

* active head selected;
* has a current spouse;
* spouse alive;
* spouse not imprisoned;
* spouse Female;
* spouse age ≤45.

Effect:

Multiplies that year's calculated childbirth probability by **5**.

---

## Divorce the Spouse

Availability:

* active head selected;
* has `spouseId`.

Resolved during life-events phase.

Effects:

* event reports a settlement equal to `floor(householdWealth / 2)`;
* actual household wealth is divided by 2;
* actor loses 10 health;
* spouse loses 10 health;
* every child in the actor's `childrenIds` loses 10 health;
* wife's surname returns to maiden name when available;
* active marriage-history entries receive:

  * `endYear = current year`
  * `endReason = "divorce"`
* both `spouseId`s are cleared.

The removed half of wealth is **not actually transferred to the spouse** in this player-initiated divorce.

---

## Quit the Job

Availability:

* active head selected;
* not retired;
* currently job level >0.

Effect:

`jobLevel = 0`

Because this resolves after finances, the character receives that year's salary before quitting.

---

## Work Harder

Availability:

* active head selected;
* age 18+;
* employed;
* job level below 5;
* not retired.

Effects:

* health change −5 during health processing;
* promotion probability receives a large bonus.

Promotion bonus:

`0.15 + intellect × 0.05`

This is added to the normal promotion chance.

---

## Seek Employment

Availability:

* active head selected;
* age 18+;
* job level 0;
* not retired.

Success probability:

`(strength / 5) × 0.5`

Equivalent to:

| Strength | Success |
| -------: | ------: |
|        1 |     10% |
|        2 |     20% |
|        3 |     30% |
|        4 |     40% |
|        5 |     50% |

Success sets job level to 1.

Because this resolves after finances, new salary begins the following year.

Failure creates no specific failure event.

---

## Drink

Availability:

* active head selected;
* currently employed;
* job satisfaction exactly 1.

Immediate effects:

* job satisfaction +2, maximum 5;
* health −10.

Alcoholism roll:

20%.

If successful and the person does not already have Alcoholism, the permanent Alcoholism condition is added.

Because Alcoholism is added before health-condition processing, its −10 annual health effect also applies during that same year's health phase.

---

## Buy a House

This is instantaneous rather than queued.

Availability:

`householdWealth >= 10000`

Effect:

* wealth −10,000;
* houses owned +1.

---

## Sell a House

Instantaneous.

Availability:

`housesOwned > 1`

Effect:

* wealth +10,000;
* houses owned −1.

If the number of rented houses would become impossible after the sale:

`rentedHouses = max(0, housesOwned - 1)`

The household is never allowed to sell its final owned residence.

---

## Rent Out a House

Instantaneous.

Availability:

`housesOwned > rentedHouses + 1`

Effect:

`rentedHouses += 1`

Each rented house produces $250 each year.

At least one owned house is always reserved for the household itself.

---

## Hire a Nanny

Availability:

* no current `nannyId`;
* household wealth ≥250;
* underage child count exceeds:

  * 3 normally;
  * 4 when the current wife is a Housewife.

This action is queued.

On resolution a new nanny is created:

* Female;
* random first name;
* random surname;
* age 25–45;
* `isNanny = true`.

The nanny is stored in the global family array.

No immediate $250 deduction occurs.

The $250 nanny cost is charged during the finance phase of that same year.

---

## Fire Nanny

Availability:

`nannyId` exists.

On resolution:

* nanny is marked `isAlive = false`;
* `deathYear = current year`;
* `nannyId = null`.

This is effectively how a nanny is removed.

It is not processed as a normal death.

---

## Ask Parents for Money

Availability:

* active head selected;
* household wealth ≤0;
* father is alive;
* father is male-lineage and has household wealth.

The action actually asks the **father**, despite the plural label.

At resolution, father's household must have at least $10,000.

Success chance:

50%.

Successful transfer:

* player household +$2,000;
* father's household −$2,000.

Failure transfers nothing.

---

## Ask Child for Money

Availability:

* active head's wealth ≤0;
* at least one living adult child has `householdWealth > 10000`.

The game automatically chooses the wealthiest qualifying child.

Success chance:

50%.

At resolution the child must have at least $10,000.

Successful transfer:

* parent +$2,000;
* child −$2,000.

---

# 16. Contextual actions on selected relatives

## Get Education

Allowed only when the selected person is:

* the active household head; or
* his current spouse.

Requirements:

* education below level 5;
* household can afford $2,000.

Cost is paid whether the attempt succeeds or fails.

Success:

`0.30 + intellect × 0.15`

| Intellect |               Success chance |
| --------: | ---------------------------: |
|         1 |                          45% |
|         2 |                          60% |
|         3 |                          75% |
|         4 |                          90% |
|         5 | 105%, effectively guaranteed |

Success increases education by exactly 1.

Maximum education is 5.

---

## Go to Therapy

Available on any selected living person with at least one of:

* Alcoholism;
* Depression;
* Anxiety.

The active household must have $1,500.

There is no relationship restriction in the actual action condition beyond being able to select the target.

Cost is paid regardless of outcome.

Success probability:

`(6 - intellect) / 10`

Therefore:

| Intellect | Therapy success |
| --------: | --------------: |
|         1 |             50% |
|         2 |             40% |
|         3 |             30% |
|         4 |             20% |
|         5 |             10% |

This means higher intellect actually makes therapy **less** likely to succeed in the source implementation.

On success all three listed conditions are removed simultaneously.

---

## Heal

Target must be:

* current spouse; or
* one of the actor's children.

Target must have health below 90.

Cost:

$1,000.

Effect:

`health += 25`, capped at 100.

The action can be displayed disabled if the player cannot afford it.

---

## Give House to Son

Instantaneous.

Target:

* actor's male child.

Requirement:

`housesOwned > 1`

If son is age 18+:

* father's houses −1;
* son's houses +1.

If son is younger than 18:

* father's houses −1;
* son's `pendingHouses += 1`.

The promised house transfers when the son reaches adulthood.

---

## Help Wife Seek Employment

Target must be:

* current Female spouse;
* adult;
* job level 0;
* not retired.

Success:

`strength / 10`

On success:

`jobLevel = 1`

Because it executes before finances, a successful wife can contribute her new salary immediately during that same year's household calculation.

---

## Help Daughter Seek Employment

Target must be:

* actor's daughter;
* age 18+;
* unmarried;
* job level 0;
* not retired.

Same success probability:

`strength / 10`

A successful adult unmarried daughter can contribute salary immediately that year.

---

## Ask Wife to Recover

Available when wife:

* is employed;
* has job satisfaction 1.

Success probability:

50%.

Success:

* job satisfaction +2, maximum 5;
* health +20.

---

## Ask Daughter to Recover

Same logic as wife, but target must be an adult employed daughter with satisfaction 1.

---

## Ask Wife to Quit Job

Available when wife is employed and has satisfaction 1.

Success probability:

50%.

On success:

`jobLevel = 0`

---

## Ask Daughter to Quit Job

Same behavior for an adult employed daughter with satisfaction 1.

---

# 17. Passive education

Children can gain education automatically.

Eligibility:

* age at least 6;
* younger than 18;
* education below 5.

Annual chance:

`intellect / 25 + health / 600`

At health 100:

| Intellect |  Chance |
| --------: | ------: |
|         1 | ~20.67% |
|         2 | ~24.67% |
|         3 | ~28.67% |
|         4 | ~32.67% |
|         5 | ~36.67% |

Success increases education by one level.

There is no event logged for passive education.

---

# 18. Adulthood

At exactly age 18, an adulthood event is created.

For male-lineage males:

### Household wealth

Ensure household wealth exists.

### Pending cash inheritance

If above zero:

* add to household wealth;
* log inheritance;
* clear pending inheritance.

### Pending houses

If above zero:

* add to `housesOwned`;
* log house transfer;
* clear pending houses.

### Sexuality roll

0.5% chance to set:

`sexuality = "homosexual"`

Otherwise it remains heterosexual.

This roll occurs only when a male-lineage character reaches age 18 through annual processing.

The initial founder starts already age 18 and therefore does not receive this roll.

---

# 19. Retirement

Retirement ages:

* Male: 65
* Female: 60

At the exact retirement age:

`lastIncome = jobLevel × 500`

Then:

`jobLevel = 0`

A retirement event is recorded.

Pension in subsequent finances:

`lastIncome × 0.10`

Because finances occur before retirement processing, the character receives their normal full salary during the year in which they reach retirement age.

---

# 20. Promotion system

Base annual promotion probability:

`(intellect / 5) × 0.02`

Therefore:

| Intellect | Base promotion |
| --------: | -------------: |
|         1 |           0.4% |
|         2 |           0.8% |
|         3 |           1.2% |
|         4 |           1.6% |
|         5 |           2.0% |

Requirements:

* currently employed;
* not retired;
* age greater than 21;
* job level below 5.

### Education penalties

If job level ≥2 and education <3:

`chance /= 5`

If job level ≥3 and education <4:

`chance /= 10`

These penalties can stack.

If job level ≥4 and education <5:

`chance = 0`

Thus education level 5 is required to progress from Director to Magnate.

### Work Harder

Adds:

`0.15 + intellect × 0.05`

before education penalties are applied.

### Firing

Every employed non-retired character has a 1% annual firing chance.

Firing is checked before promotion.

If fired:

`jobLevel = 0`

No promotion roll occurs that year.

---

# 21. Marriage probability

Automatic spouse searching occurs for every living person satisfying:

* Male;
* male-lineage;
* unmarried;
* age 18+.

Base annual probability depends on Appeal:

| Appeal | Marriage chance |
| -----: | --------------: |
|      1 |              5% |
|      2 |              7% |
|      3 |             10% |
|      4 |             12% |
|      5 |             16% |

Find a Spouse multiplies these by 5:

| Appeal | With action |
| -----: | ----------: |
|      1 |         25% |
|      2 |         35% |
|      3 |         50% |
|      4 |         60% |
|      5 |         80% |

---

# 22. Generated spouse

When a male-lineage character successfully finds a partner:

## If heterosexual

Generate a Female spouse.

## If homosexual

Generate a Male partner.

### Partner age

Random inclusive range:

lower bound:

`max(18, playerAge - 20)`

upper bound:

`min(40, playerAge + 10)`

### Other generated properties

* random first name of appropriate gender;
* random surname;
* education randomly 1–3;
* normal random statistics;
* normal random adult job level 0–3;
* random job satisfaction 1–5.

A heterosexual generated wife receives text-only generated family history.

A homosexual male partner does not.

### Heterosexual marriage

Wife's stored surname changes to husband's surname.

Her `maidenName` remains the original surname.

### Homosexual partnership

The event describes a partnership.

The male partner retains his generated surname.

Both relationships populate `spouseId` and `marriageHistory`.

---

# 23. Generated spouse family background

A generated heterosexual spouse receives:

### Father

Random male first name using her maiden surname.

### Mother

Random female first name using the feminine form of her maiden surname.

### Siblings

Random count 0–4.

Each sibling gets random gender and a randomly generated first name.

These relatives exist only as text strings.

They are not `Person` objects and do not participate in the simulation.

---

# 24. Polish surname handling

When a Female surname is displayed:

* ending `ski` becomes `ska`;
* ending `cki` becomes `cka`;
* all other surnames remain unchanged.

This does not necessarily change the underlying stored surname.

Example:

`Kowalski` → `Kowalska`

The dynasty title separately removes Polish diacritics when displaying the family name.

---

# 25. Affairs

Every currently stored spouse relationship has an affair probability check during life events.

Annual chance:

0.5%.

Because each spouse can potentially be processed independently, the effective couple-level chance can be close to 1% when both characters reach their respective checks without the relationship already being broken.

Effects:

* immediate divorce;
* actor health −25;
* spouse health −25;
* mutual children lose 15 health;
* marriage histories end with reason `"divorce"`;
* wife's surname reverts to maiden name;
* both spouse IDs are cleared.

## Financial settlement when male-lineage man is the acting character

If the actor is a male-lineage man with household wealth:

`settlement = floor(householdWealth / 2)`

His wealth decreases by this settlement.

If spouse is Female:

`spouse.pendingInheritance += settlement`

---

# 26. Divorce during imprisonment

An imprisoned married character has a 10% annual chance that the spouse divorces them.

The marriage records are closed.

The wife's surname reverts.

Both spouse IDs are cleared.

This is checked during life-events processing.

---

# 27. Female remarriage

An unmarried Female character can remarry automatically if:

* age ≥18;
* age <50;
* not controllable;
* lineage is not male-lineage.

Annual remarriage chance:

5%.

The new husband is generated with:

* random male first name;
* random surname;
* age randomly from the woman's current age through current age +10;
* default random stats;
* random adult job level;
* default education level 0 unless otherwise inherited from constructor defaults.

The woman's surname becomes the husband's surname.

Marriage history is created for both people.

This rule also lets bloodline daughters establish new branches of the genealogy.

---

# 28. Fertility and childbirth

A childbirth probability is calculated when a living spouse exists and the spouse is not imprisoned.

Base fertility uses the lower fertility value of the two partners:

`baseFertility = min(person.fertility, spouse.fertility)`

Base chances:

| Fertility | Child chance |
| --------: | -----------: |
|         0 |           0% |
|         1 |           5% |
|         2 |           7% |
|         3 |          10% |
|         4 |          12% |
|         5 |          16% |

A birth can occur only when the person currently being processed is:

* Male;
* has living Female spouse;
* spouse age ≥18;
* spouse age ≤45.

The male does not have to be male-lineage. This allows husbands who married bloodline daughters to produce further descendants.

---

# 29. Female age fertility decline

No age penalty through age 30.

For ages above 30:

`ageFactor = max(0.1, 1 - ((femaleAge - 30) / 15))`

Then:

`childChance *= ageFactor`

Examples:

| Female age | Multiplier |
| ---------: | ---------: |
|         30 |      1.000 |
|         31 |      0.933 |
|         35 |      0.667 |
|         40 |      0.333 |
|         44 |      0.100 |
|         45 |      0.100 |

Try for a Baby multiplies the resulting chance by 5.

---

# 30. Birth generation

When a birth succeeds:

### Gender

Approximately 50/50 Male/Female.

### Name

Random from the matching gender pool.

A father's children cannot share the same first name.

The name is rerolled until unique among all of that father's existing children.

### Surname

Father's current surname.

### Lineage

If Male:

`child.lineage = father.lineage`

If Female:

`child.lineage = "other"`

### Bloodline

`child.isBloodline = father.isBloodline OR mother.isBloodline`

### Generation

`child.generation = father.generation + 1`

### Parents

Both parent IDs are stored.

### Birth order

Father's biography uses total number of his children.

Mother's biography uses total number of her children.

This allows differing birth-order descriptions in remarried families.

---

# 31. Genetic stat inheritance

For each statistic independently:

1. choose one parent's value with 50/50 probability;
2. add a random mutation of −1, 0, or +1;
3. clamp to 1–5.

After all stats are generated:

5% chance:

`fertility = 0`

This infertility override ignores the normal 1–5 clamp.

---

# 32. Birth conditions

One random `defectRoll` is made.

The checks occur as an `if / else if / else if` chain.

## Down Syndrome

Condition:

`roll < 0.001`

Actual probability:

0.1%.

Effects:

* condition added;
* intellect −2, minimum 1;
* condition health impact −10.

## Cerebral Palsy

Condition:

`roll < 0.002` after failing Down Syndrome.

Because the same roll is reused, actual probability is the interval 0.001–0.002:

**0.1%, not 0.2%.**

Effects:

* condition added;
* strength −2, minimum 1;
* health impact −10.

## Autism

Condition:

`roll < 0.02` after failing both previous checks.

Actual probability:

1.8%.

Effects:

* intellect +1, maximum 5;
* appeal −1, minimum 1;
* health impact 0.

The constants are 0.1%, 0.2%, and 2%, but the shared-roll `else-if` implementation produces effective probabilities of 0.1%, 0.1%, and 1.8%.

---

# 33. Health system

Every person starts with:

* health = 100;
* max health = 100.

There is no direct generic age-related health loss.

Annual natural health change starts with:

`longevity × 0.5`

Therefore:

| Longevity | Base yearly health |
| --------: | -----------------: |
|         1 |               +0.5 |
|         2 |               +1.0 |
|         3 |               +1.5 |
|         4 |               +2.0 |
|         5 |               +2.5 |

If `jobTitle == "Retired"`:

+5 additional health.

Health is capped at 100 after normal annual health calculation.

There is no corresponding lower clamp.

---

# 34. Large-family health penalty

Determine the household's spouse and number of children under 18.

Base child capacity:

* 4 if wife is currently a Housewife;
* 3 otherwise.

The implementation calculates an additional +2 capacity when a nanny exists.

However the health penalty is only applied when **no nanny exists at all**.

Therefore a nanny actually suppresses the large-family penalty regardless of how many children exist.

Without a nanny, if child count exceeds capacity:

* ordinary household members: −4 health;
* wife: −8 health.

---

# 35. Poverty health penalty

If the household is considered broke:

`healthPenalty = 5 + (10 - immunity × 2)`

Equivalent:

| Immunity | Penalty |
| -------: | ------: |
|        1 |     −13 |
|        2 |     −11 |
|        3 |      −9 |
|        4 |      −7 |
|        5 |      −5 |

A living head is broke at household wealth ≤0.

The code contains a corresponding deceased-estate test using pending inheritance when a household head can be resolved.

---

# 36. Orphan health penalty

An underage person is considered an orphan when both stored parents are not alive.

Penalty:

`3 + (6 - immunity)`

Equivalent:

| Immunity | Penalty |
| -------: | ------: |
|        1 |      −8 |
|        2 |      −7 |
|        3 |      −6 |
|        4 |      −5 |
|        5 |      −4 |

---

# 37. Miserable job penalty

If:

* job satisfaction ==1;
* job level >0;

yearly health:

−3.

---

# 38. Work/recovery health modifiers

### Work Harder

−5 health.

### Recover

+20 health.

### Drinking

−10 health immediately before annual health processing.

### Successful requested recovery

+20 health immediately.

---

# 39. Illness probability

After yearly health changes, a new-condition roll occurs.

Probability:

`(0.15 / immunity) × (1 - health / 150)`

At health 100:

`0.05 / immunity`

At health 0:

`0.15 / immunity`

If the condition selected is already present, no new condition is added and no reroll occurs.

Birth-defect conditions are excluded from random illness generation.

---

# 40. Health-condition table

The `probability` values below are relative weights used only after an illness event has already been triggered.

| Condition       | Type      |              Duration | Health/year | Weight |
| --------------- | --------- | --------------------: | ----------: | -----: |
| Common Cold     | seasonal  |                     1 |          −5 |     30 |
| Stomach Bug     | seasonal  |                     1 |          −8 |     20 |
| Food Poisoning  | seasonal  |                     1 |         −10 |     10 |
| Migraines       | seasonal  |                     1 |          −3 |     10 |
| Influenza       | seasonal  |                   1–2 |         −10 |     15 |
| Pneumonia       | curable   |                   2–4 |         −15 |      8 |
| Broken Arm      | curable   |                   2–3 |          −5 |      5 |
| Tuberculosis    | terminal  |            3–8 stored |         −20 |      2 |
| Heart Condition | terminal  | permanent in practice |          −5 |      3 |
| Cancer          | terminal  |           5–15 stored |         −10 |      4 |
| Chronic Fatigue | permanent |            indefinite |          −3 |      5 |
| Arthritis       | permanent |            indefinite |          −2 |      6 |
| Asthma          | permanent |            indefinite |          −2 |      7 |
| Depression      | permanent |            indefinite |          −4 |      8 |
| Anxiety         | permanent |            indefinite |          −3 |      8 |
| Alcoholism      | permanent |            indefinite |         −10 |      5 |
| Paraplegia      | permanent |            indefinite |          −5 |      1 |

Conditional illness-selection weights total 147.

Birth conditions are stored in the same data collection but are not candidates for normal illness.

---

# 41. Condition expiration

During annual health processing:

* every condition contributes its `healthImpact`;
* conditions with duration have duration decremented;
* permanent conditions are retained;
* terminal conditions are retained;
* Autism is explicitly retained;
* other duration-based conditions are retained only while duration >0.

Important implementation consequence:

Tuberculosis and Cancer have duration fields but are terminal, so the terminal-type rule keeps them indefinitely even when their duration reaches zero.

Down Syndrome and Cerebral Palsy are neither permanent nor terminal and have no duration.

They therefore disappear from the active condition list after contributing their health impact during their first eligible health-processing year.

Their stat modifications remain permanent.

Autism is explicitly exempted and remains indefinitely.

---

# 42. Natural death

Every currently living person receives a death check.

## Terminal conditions

Each terminal condition adds:

+10 percentage points to `ageDeathChance`.

Multiple terminal conditions stack.

## Accidents

Independent chance:

0.2% per year.

Successful accident check sets health to zero.

## Age-based death

Only considered when:

`age > 30`

Expected lifespan basis:

`baseLifespan = 40 + longevity × 12`

| Longevity | Base lifespan |
| --------: | ------------: |
|         1 |            52 |
|         2 |            64 |
|         3 |            76 |
|         4 |            88 |
|         5 |           100 |

Then:

`ageFactor = age / baseLifespan`

Only when:

`ageFactor > 0.6`

add:

`ageFactor^6 × ((6 - immunity) / 5) × 0.25`

to death probability.

Final death occurs if:

* health ≤0; or
* random roll < combined age/terminal death chance.

---

# 43. Death effects

On death:

* `isAlive = false`;
* death year = current year;
* death month random 1–12;
* death day random 1–28.

The death event is recorded.

The deceased person's current spouse link is removed from the surviving spouse.

Health penalties:

* surviving spouse −15;
* every living child −15;
* every living parent −15.

These grief penalties occur immediately during the death-processing loop.

The deceased person's own `spouseId` is not explicitly cleared.

---

# 44. Crime system

Every living character age 18+ who currently has no prison sentence receives:

0.5% annual crime chance.

Crime can therefore affect:

* dynasty heads;
* wives;
* daughters;
* external husbands;
* other simulated adults;
* potentially nannies.

If a crime occurs:

* job level becomes 0;
* a weighted crime is chosen;
* prison sentence is generated;
* crime event recorded.

If a spouse exists:

spouse health −20.

Every child:

health −15.

---

# 45. Crime table

| Crime          | Sentence | Weight |
| -------------- | -------: | -----: |
| vandalism      |   1 year |     30 |
| petty theft    |      1–2 |     25 |
| brawling       |      1–2 |     20 |
| smuggling      |      2–4 |     10 |
| fraud          |      3–6 |      8 |
| arson          |     5–10 |      4 |
| manslaughter   |     8–15 |      3 |
| espionage      |    10–20 |      2 |
| kidnapping     |    15–25 |      1 |
| treason        |    10–25 |      1 |
| murder         |    25–50 |      1 |
| serial killing |       99 |    0.5 |

Selection uses weighted random choice.

---

# 46. Prison sentence processing

At the health/status phase:

If prison sentence >0:

`prisonSentence -= 1`

If this reaches zero:

release event is recorded.

A prisoner cannot use player actions while imprisoned.

The character-detail text labels remaining sentences of **50 or more** as “Life.”

The crime-event message only substitutes “life” when the original generated sentence is **greater than 50**, producing a small discrepancy for exactly 50 years.

---

# 47. Houses

Only male-lineage males receive house-state fields.

## Owning at least one house

Eliminates the $250 annual rent expense.

## Additional houses

Can be:

* retained;
* rented out;
* sold;
* given/promised to sons.

## Rental capacity

At least one owned house must remain unrented for the household residence.

Therefore:

`rentedHouses <= housesOwned - 1`

---

# 48. House inheritance

When a male-lineage man dies with houses:

Find all living sons.

Divide houses evenly:

`housesPerSon = floor(housesOwned / livingSons.length)`

Remainder houses are distributed one at a time in son-array order.

A son can inherit houses regardless of age.

These inherited houses are added directly to `housesOwned`; they do not become `pendingHouses`.

If there are no living sons, no active transfer occurs.

---

# 49. Cash inheritance / estate settlement

Cash estate settlement only proceeds once both members of a recorded marriage are dead.

For a qualifying male-lineage husband:

`estate = householdWealth + pendingInheritance`

Children of that particular male/female union are identified.

Only living children inherit.

Share:

`floor(estate / numberOfLivingHeirs)`

Any remainder from integer division is discarded when the estate is cleared.

## Underage heir

`pendingInheritance += share`

## Adult male-lineage heir

`householdWealth += share`

## Adult married daughter

If her husband:

* is alive;
* has household wealth;

the share is added to the husband's household.

## Adult unmarried daughter

`pendingInheritance += share`

The event describes the money as waiting to be claimed upon marriage.

After settlement:

* deceased male's `householdWealth = 0`;
* deceased male's `pendingInheritance = 0`.

---

# 50. Controllability and succession

At the end of every year:

`isControllable = alive && Male && male-lineage && age >= 18`

If the active household head died:

select the first available controllable person in family-array order.

Game over check:

Does any person satisfy:

`alive && Male && male-lineage`

If no:

* display game-over;
* record year male line ended;
* disable further year advancement.

Underage male heirs count for survival of the dynasty.

---

# 51. Living-family information rules

The general family member pool contains:

1. every bloodline person matching the Living/Deceased state;
2. spouses whose marriage history links them to a bloodline person.

In the Living household view, selectable household heads are:

* Male;
* male-lineage;
* adult;
* alive;

plus deceased male-lineage adult heads who have a living underage male child.

Household heads are sorted by:

1. generation;
2. birth year.

For the selected household:

### Core household

* head;
* current living spouse;
* children under 18;
* adult unmarried daughters;
* nanny.

### Extended family

* adult sons;
* married adult daughters.

Deceased family view sorts by most recent death year first.

---

# 52. Generated About descriptions

The About section translates statistics into qualitative prose.

## Immunity

1–2: weak/delicate constitution.

3: generally healthy.

4–5: robust constitution.

## Longevity

1–2: family not known for longevity.

3: average expected lifespan.

4–5: long-lived family.

## Fertility

0: infertile.

1–2: difficulty having children.

3: normal family prospects.

4–5: highly fertile family.

## Appeal

1: extremely unattractive.

2: below average attractiveness.

3: modest/average appearance.

4: good-looking.

5: very attractive.

## Strength

1–2: weak.

3: average strength.

4–5: strong.

## Intellect

1: extremely unintelligent.

2: below average.

3: average.

4: smart.

5: genius.

These descriptions have no mechanical effect.

---

# 53. Birth order and sibling logic

For simulated people, sibling determination is based on sharing the same father ID.

Therefore paternal half-siblings are treated as siblings.

Birth order in the About section is also calculated from the father's `childrenIds`.

Children are sorted by:

1. birth year;
2. ID.

Generated spouses instead use their text-only generated family history.

---

# 54. Marriage history data

Each relationship creates matching marriage records for both partners.

Typical entry:

`{ spouseId, startYear }`

When ended:

`endYear = current year`

`endReason = "divorce"`

The initial parents' marriage uses `"death"` as end reason.

A person's Relationships section displays all spouses from marriage history, not only the current spouse.

---

# 55. Event log

Events are grouped by game year.

An event record contains:

* formatted message;
* `involvedIds`.

A primary event is added to the Family Album.

The subject's biography also receives the event.

Event types have associated symbols.

Examples include:

* healing;
* education success/failure;
* therapy;
* drinking;
* adulthood;
* inheritance;
* retirement;
* illness;
* death;
* divorce;
* crime;
* promotion;
* marriage;
* partnership;
* affair;
* birth;
* remarriage;
* housing;
* nanny events.

---

# 56. Biography propagation

Certain significant events propagate to living close relatives.

Qualifying event classifications:

* birth;
* death;
* marriage;
* divorce;
* crime;
* serious illness.

Related biography entries may be added to living:

* parents;
* children;
* siblings.

Dead relatives do not receive later propagated entries.

Sibling determination here is again based through the subject's father.

Birth events use custom biography logic instead of the generic propagation system.

---

# 57. Recovery activity content

A Recover event randomly uses one of these narrative activities:

* took time off to travel abroad for a year;
* spent a season writing memoirs;
* embarked on a pilgrimage that lasted several months;
* took an extended leave to manage a family crisis;
* spent a year studying philosophy at a remote monastery;
* dedicated a season to renovating their home;
* took a long sea voyage to distant lands;
* spent months volunteering for a cause they believe in;
* took up an apprenticeship in a new craft for a season;
* went on an extended hunting and trapping expedition;
* managed a relative's estate in the countryside for a year;
* took time off to oversee the construction of a new family building;
* dedicated a year to intensive religious study;
* embarked on a months-long archaeological dig;
* spent a season training for a major athletic competition;
* took an extended retreat for meditation and reflection;
* worked on a political campaign for several months;
* spent a year documenting the family's history;
* took a long break to simply enjoy their wealth and family.

---

# 58. Genealogy model

The genealogy representation starts from:

* generation 1;
* male-lineage founder.

For each bloodline person:

1. gather spouse IDs from marriage history;
2. also include current spouse if not already present;
3. for each spouse, find children who:

   * have that spouse as a parent;
   * are bloodline;
4. sort children by birth year;
5. recursively process each child.

Thus the genealogy follows bloodline descendants through daughters as well as sons.

Spouses themselves do not need to be bloodline.

The genealogy is informational; control and game-over rules still rely on male lineage.

---

# 59. Save format

Export serializes the entire `gameState` as JSON.

The JSON is obscured using:

1. repeating-key XOR;
2. key string:

`dynastia_secret_key`

3. UTF-8 encoding;
4. Base64.

Save MIME type:

plain text.

Filename:

`dynastia_save_[surname]_[year].txt`

---

# 60. Loading

On load:

1. read file as text;
2. decrypt;
3. JSON parse;
4. verify `isDynastiaSave`;
5. restore `Person.prototype` for every member of `family`;
6. replace current `gameState`;
7. refresh game information.

If decryption fails, the loader attempts weaker fallbacks:

* Base64 decode only;
* raw string.

There is no schema migration or comprehensive data validation.

---

# 61. Built-in male first-name pool

Adam, Adrian, Albert, Aleksander, Andrzej, Antoni, Arkadiusz, Artur, Bartłomiej, Bartosz, Błażej, Bogdan, Borys, Cezary, Cyprian, Damian, Daniel, Dariusz, Dawid, Dominik, Emil, Eryk, Fabian, Feliks, Filip, Franciszek, Fryderyk, Gabriel, Gracjan, Grzegorz, Gustaw, Henryk, Hubert, Ignacy, Igor, Ireneusz, Jacek, Jakub, Jan, Janusz, Jarosław, Jeremiasz, Jerzy, Jędrzej, Józef, Julian, Juliusz, Kacper, Kajetan, Kamil, Karol, Kazimierz, Klaudiusz, Konrad, Kornel, Krystian, Krzysztof, Lech, Leon, Leszek, Lucjan, Ludwik, Łukasz, Maciej, Maksymilian, Marcel, Marcin, Marek, Marian, Mariusz, Mateusz, Michał, Mieszko, Mikołaj, Miłosz, Mirosław, Nikodem, Norbert, Olaf, Oliwier, Oskar, Patryk, Paweł, Piotr, Przemysław, Radosław, Rafał, Remigiusz, Robert, Roman, Ryszard, Sebastian, Seweryn, Sławomir, Stanisław, Stefan, Sylwester, Szymon, Tadeusz, Teodor, Tomasz, Tymon, Tymoteusz, Wacław, Waldemar, Wiktor, Witold, Władysław, Włodzimierz, Wojciech, Zbigniew, Zdzisław, Zenon, Zygmunt.

---

# 62. Built-in female first-name pool

Adela, Adrianna, Agata, Agnieszka, Aldona, Aleksandra, Alicja, Alina, Amanda, Amelia, Anastazja, Andżelika, Aneta, Anita, Anna, Antonina, Apolonia, Augustyna, Aurelia, Barbara, Beata, Berenika, Bernadetta, Blanka, Bogumiła, Bogusława, Bożena, Cecylia, Celina, Czesława, Dagmara, Danuta, Daria, Diana, Dominika, Dorota, Edyta, Elena, Eleonora, Eliza, Elwira, Elżbieta, Emilia, Eugenia, Ewa, Ewelina, Felicja, Franciszka, Gabriela, Genowefa, Grażyna, Halina, Hanna, Helena, Henryka, Honorata, Ida, Iga, Inga, Irena, Irmina, Iwona, Izabela, Jadwiga, Jagoda, Janina, Joanna, Jolanta, Jowita, Judyta, Julia, Julita, Justyna, Kamila, Karina, Karolina, Katarzyna, Kinga, Klaudia, Kleopatra, Klementyna, Konstancja, Kornelia, Krystyna, Laura, Lena, Leokadia, Leonora, Lidia, Liliana, Lilianna, Liwia, Lucyna, Ludmiła, Ludwika, Łucja, Magdalena, Maja, Malwina, Małgorzata, Marcelina, Maria, Marianna, Mariola, Marlena, Marta, Martyna, Marzena, Matylda, Melania, Michalina, Milena, Mirosława, Monika, Nadia, Natalia, Natasza, Nikola, Nina, Oliwia, Patrycja, Paulina, Pola, Regina, Renata, Roksana, Róża, Sabina, Sandra, Sara, Sonia, Stefania, Stella, Sylwia, Tamara, Tatiana, Teresa, Urszula, Weronika, Wiesława, Wiktoria, Wioletta, Zofia, Zuzanna, Żaneta.

---

# 63. Built-in surname pool

Nowak, Kowalski, Wiśniewski, Wójcik, Kowalczyk, Kamiński, Lewandowski, Zieliński, Szymański, Woźniak, Dąbrowski, Kozłowski, Jankowski, Mazur, Wojciechowski, Kwiatkowski, Krawczyk, Piotrowski, Grabowski, Nowakowski, Pawłowski, Michalski, Nowicki, Adamczyk, Dudek, Zając, Wieczorek, Jabłoński, Król, Majewski, Olszewski, Jaworski, Wróbel, Malinowski, Stępień, Górski, Pawlak, Sikora, Witkowski, Walczak, Baran, Rutkowski, Michalak, Szewczyk, Ostrowski, Tomaszewski, Zalewski, Wróblewski, Pietrzak, Jasiński, Marciniak, Bąk, Sokołowski, Zawadzki, Wesołowski, Jakubowski, Lis, Czarnecki, Kubiak, Maciejewski, Szczepański, Głowacki, Zakrzewski, Laskowski, Brzeziński, Kołodziej, Kaźmierczak, Szymczak, Ciesielski, Kaczmarczyk, Przybylski, Duda, Urbański, Gajewski, Klimek, Szulc, Wasilewski, Krajewski, Sikorski, Adamski, Błaszczyk, Andrzejewski, Chojnacki, Górecki, Kania, Kucharski, Marek, Skiba, Tomczyk, Janicki, Kopeć, Mroczek, Nowacki, Piątek, Urban, Wolski, Borkowski, Sawicki, Sowiński, Bednarek, Cieślak, Krupa, Leszczyński, Wilk, Ziółkowski, Czajkowski, Kaczmarek, Kowalewski, Mazurek, Sobczak, Wysocki, Chmielewski, Kozak, Łuczak, Mróz, Bednarczyk, Dobrowolski, Madej, Markowski, Milewski, Murawski.

---

# 64. Source-faithful implementation quirks

The following behaviors are present in Dynasty 4 and should be preserved only if exact compatibility is desired.

## 64.1 Freeze Health does much more than freeze health

When `isHealthFrozen` is active, each living person's:

* health is set to 100;
* health conditions are deleted.

But the entire normal health/status branch is skipped.

This also skips:

* adulthood processing;
* pending inheritance reception at age 18;
* pending-house transfer at age 18;
* sexuality roll;
* passive education;
* retirement;
* prison sentence countdown;
* normal health changes;
* illness generation.

Death processing still runs afterward, including age death and accidents.

---

## 64.2 Recover does not actually remove salary

Its explanatory text says the character will not receive salary.

Finance code never checks the Recover action.

The character therefore still receives normal income.

---

## 64.3 Therapy success is reversed with Intellect

Intellect 1 receives 50% success.

Intellect 5 receives 10%.

If faithfully recreating actual code, preserve this.

---

## 64.4 Birth-condition headline constants differ from actual probabilities

Because one roll and chained comparisons are used:

* Down Syndrome: 0.1%;
* Cerebral Palsy: 0.1%;
* Autism: 1.8%.

---

## 64.5 Down Syndrome and Cerebral Palsy disappear from active condition lists

Their stat effects remain.

Their condition entry disappears after its first processed yearly health effect.

Autism remains indefinitely.

---

## 64.6 Terminal disease durations do not terminate the condition

Tuberculosis and Cancer have duration fields, but terminal status makes them remain indefinitely.

---

## 64.7 Affair and divorce processing can use stale spouse data

At the start of each character's life-event processing, the current spouse object is captured in a local variable.

If divorce or another relationship-changing event subsequently clears `spouseId`, that local spouse variable is not refreshed.

As a result, rare same-year combinations are possible, including processing later relationship logic using an ex-spouse.

An exact port should preserve phase ordering and local-reference behavior if deterministic compatibility is required.

---

## 64.8 Newly created people do not receive a full simulation that year

The list of people processed during life events is captured before marriages and births create additional characters.

A spouse or child created during the pass is therefore not independently processed until the next year.

---

## 64.9 Array order can affect cascading deaths

Grief penalties are applied immediately during the death loop.

If a relative loses enough health to reach zero:

* if that relative has not yet received their death check, they can die that same year;
* if their death check already occurred earlier in array order, death waits until a later year.

Thus family-array order can affect outcomes.

---

## 64.10 Accident cause text uses a second random roll

The death phase performs one 0.2% accident roll to cause death.

The event-composition code performs another independent 0.2% roll to decide whether to describe the death as a named accident.

Therefore the narrative cause does not necessarily match the mechanical cause.

---

## 64.11 Adult daughter's pending inheritance is not reliably claimed on marriage

An unmarried adult daughter can receive `pendingInheritance` and an event saying it will be claimed upon marriage.

The automatic female-remarriage code does not transfer that stored pending inheritance into the new husband's household.

It can therefore remain stranded.

---

## 64.12 Married-daughter inheritance fallback can report money without crediting it

If the game cannot identify a valid recipient household for a married daughter, a “received inheritance” event can be produced without actually increasing a balance.

---

## 64.13 Generated-spouse inheritance branch is normally unreachable

Immediately after creating a new spouse, the game checks whether that spouse has `pendingInheritance`.

Normal newly generated spouses start with zero, and their generated text family history does not assign inheritance.

---

## 64.14 House inheritance does not clear the deceased person's stored house count

Inherited houses are added to sons.

The dead father's `housesOwned` value itself is not set to zero.

Because inheritance is only processed during the death year, the houses are not redistributed repeatedly.

---

## 64.15 Nannies are ordinary simulated people in many systems

A nanny lives inside the general `family` array.

Consequently the nanny may undergo:

* health changes;
* illness;
* death;
* crime;
* female remarriage logic.

However `generateJobTitle()` prioritizes `"Nanny"` above prison and retirement, so a nanny's occupation label can hide those states.

---

## 64.16 A naturally dead nanny can leave a stale `nannyId`

Natural death does not clear the household head's `nannyId`.

Because several systems merely test whether the ID exists:

* the household may continue paying nanny expense;
* large-family strain can remain suppressed;
* hiring a replacement remains unavailable.

The player can queue Fire Nanny to clear the stale reference.

---

## 64.17 Nanny completely suppresses large-family strain

The nominal child limit is increased by 2 when a nanny exists.

However the actual health/warning check additionally requires “no nanny.”

Any valid `nannyId` therefore prevents the strain penalty completely.

---

## 64.18 Self-employment changes and assisted employment changes occur at different phases

Self Seek Employment occurs after finances, so salary begins next year.

Help Wife/Daughter Seek Employment occurs before finances, so successful employment can generate salary immediately.

Self Quit Job occurs after finances, so the old salary is still received.

Ask Wife/Daughter to Quit occurs before finances, so successful quitting removes that year's salary.

---

## 64.19 Game over follows male lineage only

The game may contain many living bloodline descendants through daughters and still end immediately if the final male-lineage male dies.

---

## 64.20 Female-line genealogy generations can reset

Children use:

`generation = father.generation + 1`

External husbands generated through female remarriage normally have `generation = null`.

In JavaScript:

`null + 1 == 1`

Children of those husbands can therefore receive generation 1 rather than a genealogically correct deeper generation.

---

## 64.21 Old saves are not migrated

The loader restores the serialized state as-is.

A save made by an older version that lacks fields such as `isBloodline` receives no automatic migration.

A cross-version recreation should add migration separately if desired.

---

# 65. Recommended fidelity contract

For a gameplay-faithful reimplementation, preserve at minimum:

1. all core constants;
2. Person and household state distinctions;
3. male-lineage versus bloodline distinction;
4. yearly phase order;
5. action timing;
6. exact probability formulas;
7. exact household membership rules;
8. birth and inheritance logic;
9. health/death formulas;
10. job/promotion rules;
11. game-over condition;
12. event/biography recording.

If compatibility with observed Dynasty 4 behavior is required rather than merely reproducing intended design, also preserve the quirks in Section 64.

---

# 66. Minimal validation scenarios

A new implementation can be checked against the original using these deterministic rule tests.

## New game

Expected:

* year 1900;
* founder age 18;
* founder male-lineage and bloodline;
* founder wealth $1,000;
* founder owns one house;
* founder education 1–2;
* parents dead in 1899.

## Education

Intellect 1 active course: 45%.

Intellect 5 active course: effectively 100%.

## Employment

Strength 1 basic-job search: 10%.

Strength 5: 50%.

## Marriage

Appeal 1 normal: 5%.

Appeal 5 normal: 16%.

Find Spouse multiplies by 5.

## Fertility

Fertility pair minimum 1: 5%.

Minimum 5: 16%.

Try for Baby multiplies final chance by 5.

## Poverty

At Immunity 1: −13 health/year.

At Immunity 5: −5.

## Orphan

At Immunity 1: −8.

At Immunity 5: −4.

## Natural lifespan basis

Longevity 1: 52.

Longevity 5: 100.

## Basic household

One employed Level-1 single head with one owned house:

income = $500.

expense = $250.

net = +$250/year.

## Rented extra house

Add one rented house:

+$250/year.

## No owned house

Add rent expense:

−$250/year.

## Game over

One living male-lineage infant means game continues.

Zero living male-lineage males means game ends even if female bloodline descendants survive.

---

This specification represents the observable mechanics and stored content of Dynasty 4 rather than a redesigned interpretation of the game.
