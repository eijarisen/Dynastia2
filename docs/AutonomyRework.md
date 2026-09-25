# Autonomous household survival rework — D15-003

The current autonomy policy follows one hierarchy for every household that is under autonomous control. It uses the normal action registry, queue and mechanic executors; it does not create AI-only money, housing, marriages or extra actions.

## Decision policy

| Priority | Purpose |
| --- | --- |
| 1000 | Keep the household alive: acute health danger, existing harmful overcrowding/strain and emergency funding needed to correct it |
| 800 | Restore enough income/cash flow to survive |
| 650 | Sustainable family formation: safe first/second child plans, adult-child establishment and the prerequisites that make those transitions reachable |
| 500 | Protect existing children and family stability |
| 300 | Improve long-term circumstances |
| 100 | Optional activity using genuinely spare capacity |

Male-line and bloodline facts remain available for succession diagnostics. They are not separate urgency bands. Male-line succession may break an exact tie between equally useful, safe adult family-formation actions; it never changes medical, education or conception priority.

## Existing children and deliberate expansion

Every living child belonging to either current partner counts as an existing commitment, including stepchildren and recognized adopted children. Illness, infertility, adulthood, marriage or moving out does not remove that commitment. Two existing children are the initial soft stop for deliberate additional births. Natural/passive reproduction remains governed by the ordinary reproduction mechanic.

A living grandchild or another already functioning descendant family prevents old parents from treating a deceased direct child as a reason to start a replacement family. Biological male-line/bloodline viability is still calculated for the game's real succession rules and diagnostics only.

Active conception additionally requires a real reproductive path, no material unmet dependent need, sufficient care/residence capacity and a two-year essential-budget check with a protected reserve. A resident adult child's unresolved establishment blocks a discretionary second birth until that transition is addressed.

## Adult family progression

The planner now treats ordinary establishment as a reachable sequence instead of requiring a spare second house. `household.ask_move_out` can establish a normal rented branch when the child can support its essential rent/living costs; an owned spare house remains an optional route. Employment, better-employment and viable craft steps for an adult child inherit family-formation urgency when they are the prerequisite for independence.

Arranged marriage and spouse-search candidates are filtered against the actual post-union residence and essential budget. A resident son's incoming spouse cannot be selected if the current household would immediately overcrowd; a daughter's marriage-created household must be financially viable, and the source household must remain viable after her income/living-cost departure.

## Care, reserves and score

Medical triage uses actual health harm, childhood vulnerability and essential earning/reproductive roles without sex/ancestry favoritism. Stable chronic illness no longer freezes unrelated useful actions. Existing dependent danger blocks discretionary births and spending locally rather than through a global chronic-condition veto.

Optional property investment, lending, paid education, religious study and stat improvement yield to live family-formation obligations. Game-score preview is consulted only after ordinary utility has identified near-equivalent safe development/optional actions. It cannot promote a lower-priority or substantially worse action.

D15-003 adds one additive persisted component, `households.autonomy_family_plan`, containing only minimal plan/fairness/reservation intent. Old saves have no such component and therefore begin with no reservations; the planner derives fresh intents on the next autonomous assessment. No save-format version bump or manual migration is required.
