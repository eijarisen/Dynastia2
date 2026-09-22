> **Implementation status (D13-003):** The supplied seven-tab Instructions replacement is applied. Developer-facing design documentation is maintained separately under `docs/GameDesign/`.

# Dynastia — Instructions Rework Implementation Notes

## Scope

Replace the current long-form Instructions text with the supplied `src/Dynastia.App/Views/InstructionsWindow.axaml`.

No mechanics, action IDs, persistence, view-model logic or event handling are changed.

## Design rules used

- The Instructions window is an orientation guide, not a strategy guide or formula reference.
- Every major gameplay system is named at least once.
- Tips stay qualitative: no hidden probability formulas, thresholds, balance values or exact eligibility calculations are exposed.
- Important concepts are bolded through `Run FontWeight="Bold"`.
- Each tab is a short list of independent tips rather than several dense paragraphs.
- Current action descriptions/tooltips remain the source for exact costs, requirements and displayed chances.

## New tabs

1. Basics
2. Family
3. People
4. Work & Skills
5. Wealth & Property
6. Town & Society
7. Risks & History

## Mechanics coverage

| Current mechanic/plugin | Instructions coverage |
|---|---|
| Adoption / orphan care | Family — Orphans and adoption |
| Aging | People — Hobbies, Thoughts and aging |
| Appearance | People — Identity and appearance |
| Biography | Basics — Family records |
| Career | Work & Skills — Employment, Career life, Work choices |
| Childhood | Family — Childhood and care |
| Church | Town & Society — Church |
| Community policies | Town & Society — Community policy |
| Community connections | Town & Society — Community connections |
| Civic office | Town & Society — Civic office |
| Crafts | Work & Skills — Crafts |
| Artistic craft output | Work & Skills — Artistic work |
| Economy | Wealth & Property — Household budget |
| Household lifestyle | Wealth & Property — Lifestyle |
| Education | Work & Skills — Education |
| Family / bloodline / nationality | Family — Lineage and bloodline; People — Identity and appearance |
| Family Relations | Family — Family Relations |
| Farming | Work & Skills — Farming work; Wealth & Property — Farmland and livestock |
| Health | People — Health and Stress |
| Stress / mental health | People — Health and Stress; Wellbeing choices |
| Heirlooms | Wealth & Property — Heirlooms |
| Historical events | Risks & History — Historical events |
| Hobbies | People — Hobbies, Thoughts and aging |
| Households | Family — Households; Wealth & Property — Housing |
| House extensions / capacity | Family — Childhood and care; Wealth & Property — Housing |
| Inheritance | Family — Succession and estates |
| Justice | Town & Society — Court and justice; Risks & History — Crime / Prison |
| Criminal occupation | Risks & History — Crime |
| Loans | Wealth & Property — Loans |
| Locations / towns / regions | Basics — Time and place matter; Risks & History — A changing map |
| Mortality | People — Hobbies, Thoughts and aging |
| Personality / Morals | People — Personality and Morals |
| Rare Events | Risks & History — Rare events |
| Relationships | Family — Marriage |
| Religious vocation | Work & Skills — Religious vocation |
| Reproduction / birth conditions / nonmarital births | Family — Children |
| Paid stat improvements | People — Acquired improvement |
| Stats | People — Core traits |
| Status | People — Renown and Reputation; Town & Society — Local Status |
| Succession / game over | Family — Succession and estates; Risks & History — Dynasty end |
| Thoughts | Basics — Family records; People — Hobbies, Thoughts and aging |
| Town Life / institutions / prosperity | Basics — Town Affairs; Town & Society — Institutions and prosperity |
| Annual turn actions | Basics — Annual turn |
| Wellbeing / Recover / Drink / therapy | People — Health and Stress; Wellbeing choices |
| Housing market / rental investment / relocation | Wealth & Property — Housing and Investment property |
| Family Inventory | Wealth & Property — Family Inventory |
| Surname assimilation | People — Identity and appearance |
| Historical migration / external residence | Risks & History — Migration and Dynasty end |

`Dynastia.Mechanics.FamilySupport` is a compatibility shell; its current gameplay is owned by Family Relations and is therefore not presented as a separate system.

## Static validation checklist

After copying the replacement file:

1. XML/XAML remains well formed.
2. `InstructionsWindow.axaml.cs` requires no change; `OnCloseClick` is preserved.
3. The `ui` namespace and `ImageStateButton` are preserved.
4. The existing `instructions_paper.png` and close-button asset paths are unchanged.
5. All seven tab headers fit at the current window width.
6. Each tab scrolls vertically if font rendering or scaling requires more space.
7. Bold lead phrases render through inline `Run` elements.
8. No gameplay action IDs or mechanics code are changed.

## Manual UI verification

Run the game and verify:

- Instructions opens from the same places as before.
- No tab header is clipped.
- Tips read as short independent entries rather than paragraphs.
- Bold lead concepts are visually clear.
- The last tip in every tab remains reachable at normal Windows scaling.
- The Close button works.
- The guide does not expose exact formulas, hidden thresholds, probability tables or balance constants.
