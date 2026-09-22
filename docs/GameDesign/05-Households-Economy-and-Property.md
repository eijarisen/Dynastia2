# Households, Economy and Property

## Household budget
Status: **Implemented**  
Owner: `dynastia.economy`, `dynastia.households`

Authoritative state: household wealth, income/expense ledger, assets, lifestyle, member/dependant membership and budget history.

Rules:
- Finances are calculated once per active household in the `Finances` phase.
- Ordinary income is assembled from registered providers (career/pension, craft, farming, rents, royalties and other mechanic-owned streams).
- Farming income uses age-appropriate household labor: children from age 10 may help at reduced contribution while adults retain full output; passive education is not reduced by farm assistance.
- Ordinary expenses are assembled from household living costs, rent/property obligations, childcare/domestic staff and other registered obligations.
- Each current-year household record tracks `BasicNeedsRequired`, `BasicNeedsFunded` and `BasicNeedsShortfall`. Basic needs are funded from starting wealth plus ordinary income after existing debt; deprivation means a positive current-year shortfall, not simply `Wealth <= 0`.
- `IsBroke` remains a literal cash/debt concept for purchases, transfers and settlement. Health, Stress, Childhood, Marriage Satisfaction, poverty Thoughts, ordinary Crime pressure, Morals deterioration and deprivation rare-event predicates use the basic-needs shortfall signal instead.
- Ordinary yearly finance is applied through shared balance rules; UI projections are not a second rules engine.
- Household history stores annual income/expense breakdowns for Family Inventory/budget presentation and is reconciled after late Finance receipts/expenses.

Primary code: `StandardEconomyService*.cs`, `EconomyYearSystem.cs`, provider registries.

Regression tests: `SharedMechanics4DEconomyConsolidationTests.cs`, `Development10HouseholdFamilyBalanceTests.cs`.

## Lifestyle
Status: **Implemented**  
Owner: Economy

Households can choose lifestyle stance (including thrifty/balanced/lavish presentation). Lifestyle changes living-cost pressure and can modify Health, Marriage Satisfaction, career satisfaction or related outcomes through shared `HouseholdLifestyleRules`. It is a household choice rather than a per-person salary modifier.

## Housing
Status: **Implemented**  
Owner: `dynastia.households`, Economy, Locations/TownLife

Rules:
- Houses are individual persisted assets tied to towns, with market value/capacity/extension state.
- `household.buy_house`, `household.sell_house` and `household.extend_house` are queued early actions.
- Extension costs **25% of the current house value** and increases capacity/value according to extension rules.
- Any owned house can be extended when eligible; extension is not restricted to the current residence.
- Households can move/relocate to another owned house through the property-selection flow; owning investment property elsewhere does not automatically relocate residents.
- A household without a suitable owned residence rents and pays the corresponding expense.
- Housing prices/offers are local-market driven rather than one global fixed price. Town Affairs Housing is the purchase/market entry surface.

Primary code/data: `HouseholdsPlugin.PropertyActions.cs`, `HouseExtensionRules.cs`, `data/Housing/house_market_rules.json`, TownLife housing services.

Regression tests: `Development10TownAffairsPropertySurnameTests.cs`, `TownAffairsHousingFarmingTa*Tests.cs`, `Development11EmploymentEducationInventoryTests.cs`.

## Move-out and property gifts
Status: **Implemented**  
Owners: Households + Family Relations

Rules:
- `household.ask_move_out` can target an eligible adult resident.
- Providing a spare house is the strong/guaranteed relocation route where the selection flow supports it; asking a relative to rent may be refused and worsens the relation.
- `household.give_house_to_son` remains an immediate property transfer route where eligible.
- Cross-household relatives use `family_relations.give_house` / `ask_house`; acceptance considers relation and genuine surplus.

## Farmland
Status: **Implemented**  
Owner: `dynastia.farming`

Rules:
- Farmland is a household asset tied to place and carrying farm-type/livestock flavor.
- Standard purchase price is 10,000 zł and standard sale price 8,000 zł before local/specific modifiers that owning services apply.
- Livestock purchase/sale reference prices are 2,500/2,000 zł.
- `farming.buy_farmland`, `farming.sell_farmland`, `farming.add_livestock` are queued early actions.
- Farm type and livestock species are flavor/production context rather than separate playable entities; their historical/regional weighting is data-driven.
- Family Relations can transfer/request farmland; household logic avoids giving away the last essential parcel when rules require a reserve.
- Relocation may trigger farmland sale/handling according to Farming relocation rules.

Primary code/data: `FarmingRules.cs`, `FarmingPlugin.cs`, `data/Farming/`.

Regression tests: `FarmingRulesTests.cs`, `TownAffairsHousingFarmingTa*Tests.cs`.

## Nannies and family caregivers
Status: **Implemented**  
Owner: `dynastia.households`

Rules:
- Nanny need is reconciled before finance and after the year based on household children/capacity/caregivers.
- Hiring/firing are queued early household actions.
- A suitable resident daughter/relative can be asked to help with children through the household family-care action where eligible.
- Domestic care affects household expense and childcare/strain rules; stale dead-nanny legacy behavior is not the design target.

Primary code: `HouseholdsPlugin.NannyActions.cs`, `NannyNeedReconcileYearSystem.cs`.

## Loans and receivables
Status: **Implemented**  
Owner: `dynastia.loans`, Economy/Inheritance

Rules:
- `loan.take` and `loan.give` are queued early actions selected through Town Affairs Bank/loan-selection UI.
- Historical era and local bank availability determine whether/which offers exist.
- Loan terms are generated as contracts; repayment/receivable flows run late in `Finances`, after ordinary household funding has been measured.
- Annual loan receipts first cover any remaining current-year basic-needs shortfall; only the excess becomes spendable Wealth. The entire receipt is still recorded once as annual income/breakdown.
- Due contracts are materialized before money changes; creditor allocations are applied before simulated borrower debits, then contracts progress/finalize once. This makes settlement independent of contract enumeration order while preserving minor-creditor pending-inheritance behavior and existing first-payment timing.
- Loan repayment remains an allowed debt-producing finance path even when ordinary basic needs were fully funded.
- Outstanding debts and receivables are inherited/reassigned in the inheritance phase according to Loans rules rather than disappearing at death.
- Bank UI shows offer favorability qualitatively with bold color-coded text; exact hidden multipliers are not exposed as player-facing percentages.

Primary code/data: `LoansPlugin.cs`, `LoanPaymentYearSystem.cs`, `LoanInheritanceYearSystem.cs`, `data/Loans/loan_eras.csv`, `data/TownLife/bank_offer_quality.csv`.

Regression tests: `LoanAndDebtRulesTests.cs`, `Development13ContinuationTests.cs`.

## Heirlooms and artistic works
Status: **Implemented**  
Owner: `dynastia.heirlooms`

Rules:
- Heirlooms can be created by wealth milestones, careers, crafts/artistic work, hobbies, crime, historical/rare events and death-related rules.
- They are household/family assets with identity, origin, value and inheritance state.
- `heirloom.sell` is a queued early action; stolen-property sale can invoke Justice detection rules.
- Artistic works can produce royalties/income and Status.
- Family Inventory is the main asset-management surface for heirlooms.

Primary data: `data/Heirlooms/`.

Regression tests: `HeirloomsBatch1Tests.cs`, `HeirloomsBatch2Tests.cs`, `HeirloomsBatch3Tests.cs`, artistic-work tests.

## Inheritance
Status: **Implemented**  
Owner: `dynastia.inheritance` plus asset-owning plugins

Rules:
- Estate settlement runs in the `Inheritance` phase after ordinary and late mortality.
- Cash/property/claims are settled from actual persisted household/asset state.
- Houses, farmland, heirlooms, debts and receivables each keep their mechanic-owned inheritance handling; Inheritance coordinates rather than cloning all asset logic.
- Underage/adulthood transitions can defer claims until `DerivedState`/adult reconciliation.
- Household reconciliation runs around inheritance so succession, orphan placement and residence remain coherent.

Primary code: `EstateInheritanceSystem.cs`, `AdulthoodInheritanceSystem.cs`, household inheritance reconciliation, Loans/Heirlooms/Farming inheritance hooks.

Regression tests: `HouseInheritanceAssignmentRulesTests.cs`, inheritance assertions in Heirlooms/Loans/Households suites.

## Family Inventory
Status: **Implemented**  
Owner: application projection over Economy/Households/Farming/Heirlooms/Loans

Current tabs include Houses, Farmland and Heirlooms without duplicate internal headers. Empty “no owned …” states use larger text. The window also exposes household money/budget/debt/lifestyle information and routes banking/property workflows to their dedicated selection/Town Affairs surfaces.

Primary UI: `FamilyInventoryWindow.axaml*`, `MainWindowViewModel.Inventory.cs`, `FamilyInventoryViewModels.cs`.
