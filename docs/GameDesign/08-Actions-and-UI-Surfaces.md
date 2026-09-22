# Actions and UI Surfaces

This is a compact catalogue of player-facing action IDs and major application surfaces. It is not the source of action eligibility formulas; those remain in the owning mechanic/domain document and implementation.

## Action execution model

`Immediate` actions execute as soon as the UI invokes them. `Queued` actions are stored for the actor/target and resolve in the declared `YearPhase` when the next year advances. Unless explicitly bypassed, common action guards enforce living/controllable/prison/queue constraints.

The standard Actions panel deliberately does not expose every action. Some mechanics belong only in contextual windows such as Town Affairs Church/Bank/Court, Family Relations, Family Inventory, job/partner/property selection or Self Improvement.

## Core and family actions

| Action ID | Label / pattern | Owner | Target | Mode / phase | Primary surface |
|---|---|---|---|---|---|
| `turn.pass` | Pass | Turn Actions | controller | Queued / Early | Actions |
| `childhood.raise_child` | Raise Child / Support Brother/Sister by relationship | Childhood | resident child | Queued / Early | Actions / selected child |
| `relationship.find_spouse` | Find a Spouse | Relationships | controller | Queued / LifeEvents | Actions → partner selection |
| `relationship.marry_off_daughter` | arrange daughter marriage | Relationships | resident daughter | Queued / LifeEvents | Actions → partner selection |
| `relationship.marry_off_son` | arrange son marriage | Relationships | resident son | Queued / LifeEvents | Actions → partner selection |
| `relationship.repair_marriage` | Reconcile with Spouse | Relationships | married person/couple | Queued / MarriageRepair | Actions |
| `relationship.divorce_spouse` | Divorce the Spouse | Relationships | spouse/couple | Queued / LifeEvents | Actions |
| `reproduction.try_for_baby` | Try for a Baby | Reproduction | controller/spouse | Queued / LifeEvents | Actions |
| `family.adopt_polish_surname` | Adopt a Polish Surname | Households/Family | eligible household person | Queued / Early | Actions |

## Family Relations actions

| Action ID | Label | Target | Mode / phase | Surface |
|---|---|---|---|---|
| `family_relations.improve` | Improve Relations | relative in another household | Queued / FamilyRelationActions | Family Relations |
| `family_relations.ask_money` | Request Money | relative household | Queued / FamilyRelationActions | Family Relations |
| `family_relations.give_money` | Send Money | relative household | Queued / FamilyRelationActions | Family Relations |
| `family_relations.ask_house` | Request a House | relative household | Queued / FamilyRelationActions | Family Relations |
| `family_relations.give_house` | Transfer a House | relative household | Queued / FamilyRelationActions | Family Relations |
| `family_relations.ask_farmland` | Ask for Farmland | relative household | Queued / FamilyRelationActions | Family Relations |
| `family_relations.give_farmland` | Give Farmland | relative household | Queued / FamilyRelationActions | Family Relations |
| `family_relations.ask_job_help` | Use Family Connections | relative/helper | Queued / FamilyRelationActions | Family Relations |
| `family_relations.give_job_help` | Help with Careers | relative | Queued / FamilyRelationActions | Family Relations |
| `household.ask_move_out` | Ask to Move Out | adult resident | Queued / Early | Family Relations / household |

Legacy `family_support.*` actions remain compatibility-owned by `dynastia.family_support`; new cross-household support design belongs in Family Relations.

## Career and education actions

| Action ID | Label | Target | Mode / phase | Surface |
|---|---|---|---|---|
| `career.seek_employment` | Seek Employment | self | Queued / LifeEvents | Actions → Job Opportunities |
| `career.find_another_job` | Find a Better Job | self | Queued / LifeEvents | Actions → Job Opportunities |
| `career.quit_job` | Quit the Job | self | Queued / LifeEvents | Actions |
| `career.work_harder` | Work Harder | self | Queued / Early | Actions |
| `career.help_seek_employment` | Help to Seek Employment | resident relative | Queued / Early | Actions / Jobs |
| `career.help_find_better_job` | Find a Better Job | resident relative | Queued / Early | Actions / Jobs |
| `career.ask_to_recover` | Ask to Recover | working resident relative | Queued / Early | Actions |
| `career.ask_to_quit` | Ask to Quit Job | working resident relative | Queued / Early | Actions |
| `education.get_education` | Get Education | eligible household member in a town with a School | Queued / Early | Education / selected person |
| `education.help_learning` | Help in Learning | child/learner | Queued / Early | Education / selected child |

## Wellbeing and medical actions

| Action ID | Label | Target | Mode / phase | Surface |
|---|---|---|---|---|
| `wellbeing.recover` | Recover | self | Queued / Early | Actions |
| `wellbeing.drink` | Drink | self | Queued / Early | Actions |
| `wellbeing.therapy` | Therapy (historical/local label may vary) | eligible household person | Queued / Early | Town Affairs → Health |
| `wellbeing.heal_relative` | medical treatment (historical/local label varies) | eligible household relative | Queued / Early | Town Affairs → Health |
| `stats.improve_strength` | medical Strength improvement | adult household person | Queued / Early | Town Affairs → Health / Self Improvement |
| `stats.improve_intellect` | medical Intellect improvement | adult household person | Queued / Early | same |
| `stats.improve_immunity` | medical Immunity improvement | adult household person | Queued / Early | same |
| `stats.improve_appeal` | medical Appeal improvement | adult household person | Queued / Early | same |
| `stats.improve_longevity` | medical Longevity improvement | adult household person | Queued / Early | same |
| `stats.improve_fertility` | medical Fertility improvement | adult household person | Queued / Early | same |

## Household, property, farming and inventory actions

| Action ID | Label | Target | Mode / phase | Surface |
|---|---|---|---|---|
| `economy.lifestyle.lavish` | Lavish Lifestyle | household | Queued / Early | Family Inventory |
| `economy.lifestyle.balanced` | Balanced Lifestyle | household | Queued / Early | Family Inventory |
| `economy.lifestyle.thrifty` | Thrifty Lifestyle | household | Queued / Early | Family Inventory |
| `household.buy_house` | Buy a House | household/property option | Queued / Early | Town Affairs Housing / property selection |
| `household.extend_house` | Extend House | owned house | Queued / Early | Family Inventory |
| `household.sell_house` | Sell a House | owned house | Queued / Early | Family Inventory / property selection |
| `household.give_house_to_son` | Give House to Son | son + house | **Immediate** | Family Inventory / household |
| `household.hire_nanny` | Hire Nanny | household | Queued / Early | Actions/household |
| `household.ask_daughter_nanny` | Ask to Help with Children | eligible resident relative | Queued / Early | Actions/household |
| `household.fire_nanny` | Fire Nanny | household | Queued / Early | Actions/household |
| `farming.buy_farmland` | Buy Farmland | household/parcel | Queued / Early | Town Affairs / Family Inventory |
| `farming.sell_farmland` | Sell Farmland | owned parcel | Queued / Early | Family Inventory |
| `farming.add_livestock` | Add Livestock | owned farmland | Queued / Early | Family Inventory |
| `heirloom.sell` | Sell Heirloom | owned heirloom | Queued / Early | Family Inventory |
| `loan.take` | Take a Loan | household + offer | Queued / Early | Town Affairs → Bank |
| `loan.give` | Give a Loan | household + offer/borrower | Queued / Early | Town Affairs → Bank |

## Crafts and criminal occupation

Craft IDs are data-driven, so some action IDs are patterns rather than a finite fixed list:

| Action ID/pattern | Label | Target | Mode / phase | Surface |
|---|---|---|---|---|
| `craft.start.<craftId>` | Work in a Profession | eligible adult resident/self | Queued / LifeEvents | Crafts/person details |
| `craft.teach.<craftId>` | Teach Craft | young household relative | Queued / LifeEvents | Crafts/person details |
| `craft.stop_occupation` | Quit Profession | craft self-employed person | Queued / LifeEvents | Actions/person details |
| `justice.commit_crime` | Commit a Crime | eligible Evil adult | Queued / LifeEvents | Actions/person details |
| `justice.leave_life_of_crime` | Leave Life of Crime | active criminal | Queued / LifeEvents | Actions/person details |

## Town, Church, Court and Community actions

| Action ID | Label | Target | Mode / phase | Surface |
|---|---|---|---|---|
| `church.attend` | Attend Church | self | Queued / Early | **Town Affairs → Church only** |
| `church.donate` | Donate to Church | household | Queued / Early | Town Affairs → Church |
| `church.aid_poor_family` | Aid a Poor Family | household | Queued / Early | Town Affairs → Church |
| `church.ask_welfare` | Ask for Welfare | household | Queued / Early | Town Affairs → Church |
| `personality.religious_study` | Religious Study | self | Queued / MoralsReflection | **Town Affairs → Church only** |
| `justice.bail_out` | Bail Out | imprisoned household member | Queued / Early | Town Affairs → Court |
| `justice.attempt_escape` | Attempt Escape | imprisoned household member | Queued / Early | Town Affairs → Court |
| `community.lobby_policy` | Lobby for Policy | controller/town | Queued / Early | Town Affairs → Community |
| `community.perform_office_duties` | Perform Office Duties | civic-office holder | Queued / Early | Town Affairs → Community |
| `community.connection.improve` | Improve Relations | acquaintance | Queued / FamilyRelationActions | Town Affairs → Community |
| `community.connection.send_money` | Send Money | acquaintance | Queued / FamilyRelationActions | Town Affairs → Community |
| `community.connection.request_money` | Request Money | acquaintance | Queued / FamilyRelationActions | Town Affairs → Community |
| `community.connection.give_house` | Give House | acquaintance | Queued / FamilyRelationActions | Town Affairs → Community |
| `community.connection.request_house` | Request House | acquaintance | Queued / FamilyRelationActions | Town Affairs → Community |
| `community.connection.give_farmland` | Give Farmland | acquaintance | Queued / FamilyRelationActions | Town Affairs → Community |
| `community.connection.request_farmland` | Request Farmland | acquaintance | Queued / FamilyRelationActions | Town Affairs → Community |

`Attend Church` and `Religious Study` are intentionally filtered out of the standard Actions panel. Their definitions remain registered so Town Affairs can evaluate and queue them normally.

## Major UI surfaces

### Main Window
Purpose: year/current era, playable household switching, selected-person details, standard annual Actions, Family/Chronicle navigation, Map/Genealogy entry points and save/load.

Primary files: `src/Dynastia.App/Views/MainWindow.axaml*`, `MainWindowViewModel*.cs`.

### Instructions
Purpose: short qualitative orientation. It should name all major systems but avoid exact formulas/hidden thresholds.

Primary file: `src/Dynastia.App/Views/InstructionsWindow.axaml`.

### Town Affairs
Purpose: local settlement and institution hub. Current tab order is Institutions, Community, Housing, Jobs, Health, Church, Education, Bank, Court.

Primary files: `TownLifeWindow.axaml*`, `TownAffairsViewModel.cs`, `MainWindowViewModel.TownLife.cs`.

### Family Inventory
Purpose: consolidated household money/property/debt/assets/lifestyle management. Houses, Farmland and Heirlooms are separate asset tabs.

Primary files: `FamilyInventoryWindow.axaml*`, `MainWindowViewModel.Inventory.cs`.

### Family Relations
Purpose: inspect persistent relations between households and perform support/property/career/move-out interactions.

Primary files: `FamilyRelationsWindow.axaml*`, `MainWindowViewModel.Relations.cs`.

### Potential Partners
Purpose: generated spouse/candidate selection for self or arranged marriage.

Primary files: `PotentialPartnersWindow.axaml*`, Relationship partner-search service.

### Job Opportunities
Purpose: select a locally available career opportunity for self/relative job actions.

Primary files: `JobOpportunitiesWindow.axaml*`, `MainWindowViewModel.Opportunities.cs`.

### Property Selection
Purpose: choose a concrete house/property for buy/sell/move/gift workflows.

Primary files: `PropertySelectionWindow.axaml*`.

### Loan Selection
Purpose: choose a concrete bank borrowing/lending offer, showing qualitative favorability.

Primary files: `LoanSelectionWindow.axaml*`, `LoanSelectionModels.cs`.

### Self Improvement
Purpose: present targeted improvement options that are better handled as a choice list than as many standard action buttons.

Primary files: `SelfImprovementWindow.axaml*`, `MainWindowViewModel.SelfImprovement.cs`.

### Map
Purpose: inspect towns, historical/local context, dynasty presence and owned assets; overlapping co-located settlements are selectable rather than hidden behind one map point.

### Genealogy
Purpose: inspect the wider bloodline and family unions. It is informational; Succession still uses Male Lineage rules.
