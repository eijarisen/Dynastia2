# Autonomous play: balance audit

Reviewed the supplied 2026-09-25 repository. These are balancing suggestions, not changes to game rules. Findings are based on code paths; no claim of measured long-run dominance is made. Score rewards should remain subordinate to male-lineage survival and then bloodline survival. Preserve the existing policy of reversing points only to prevent achievement cycling, without adding score penalties for misfortune.

## Confirmed repeatable loopholes

### 1. Unlimited extensions turn one cheap house into a score machine

**Mechanism.** Every extension costs 25% of the property's original purchase price, adds two places, and adds 25 score points. Neither the property service nor the action caps extension count. The score tier key contains the event index, so each further extension qualifies even when the house already has excessive capacity. A bought house costing `P` therefore permits another 100 points for every `P` spent on four extensions, indefinitely while cash and years remain available. Old inexpensive properties retain their inexpensive extension cost. Extensions also add their nominal cost to appraised value and can increase investment rent.

**Limits.** The actor needs an owned house and sufficient cash. The action is queued; `ActionRegistry.EvaluateDefinition` allows only one queued action per actor at a time. Selling the house reverses its purchase and extension score, and sale proceeds are 80% of appraised value. This is continuing construction farming, not a profitable buy/sell score loop.

**Suggestion.** Give each property a plausible extension limit, increase successive construction costs, and score a finite set of capacity milestones. The decision policy should extend for a real household capacity need, rather than repeat extensions merely for points.

**References:** `plugins/Dynastia.Mechanics.Households/HouseholdsPlugin.PropertyActions.cs` (`household.extend_house`); `plugins/Dynastia.Mechanics.Economy/HouseExtensionRules.cs`; `StandardEconomyService.Capacity.cs` (`ExtendHouse`); `StandardEconomyService.Assets.cs` (`GetHouseValue`); `plugins/Dynastia.Mechanics.GameScore/StandardGameScoreService.cs` (`ProcessEvent`, `GrowAssetClaim`).

### 2. Converting cash to assets shields the divorce settlement

**Mechanism.** `PlayerDivorce` and `LowSatisfactionDivorce` halve positive household cash. `ResolvePostDivorceHouseholds` allocates custody, residences, and the cash settlement, but does not divide houses, farmland, heirlooms, or receivables. A household-head husband retaining the original household can invest spare cash before divorcing and retain the investments. A loan receivable is particularly useful because repayments later restore cash without a property sale discount.

**Limits.** Purchases/lending and divorce consume separate queued actions. Property resale loses value; lending locks up principal. Divorce still harms family health, can change child custody, reverses the marriage's 25 points, and can be refused by a Good actor. The tactic reduces settlement rather than eliminating the other consequences.

**Suggestion.** Base settlement on a defined marital estate including investments and liabilities, with a recorded payable claim when cash is insufficient. A short history of recent transfers can prevent transferring cash to relatives immediately before separation. The policy should not plan divorces or asset purchases to exploit cash-only settlement.

**References:** `plugins/Dynastia.Mechanics.Relationships/RelationshipBreakupService.cs` (`PlayerDivorce`, `LowSatisfactionDivorce`, `ResolvePostDivorceHouseholds`); `plugins/Dynastia.Mechanics.Loans/StandardLoanService.cs` (`CreateExternalReceivable`); `plugins/Dynastia.Mechanics.Loans/LoanPaymentYearSystem.cs`.

## Confirmed simplifications with a potential balance risk

### 3. External lending supplies guaranteed investment returns

Outside borrowers have no simulated finances and cannot default. Each active receivable pays its scheduled amount; the lender can open further receivables while funds permit. One-year offers, when available, yield approximately 8.5–11.5% using the current bank-quality multipliers and the 0.50 external-lending scale. Repeated lending is therefore a predictable income strategy that does not carry the losses normally associated with credit risk.

This is bounded by available cash, the 1,000–10,000 zł principal limits, the local bank, generated annual offers, and action time. The actor must be an adult, living, unimprisoned male-lineage man with control. Payments start after the issue year. Offers are deterministic within a year, so reopening the dialog does not reroll them. Borrowing has a separate active self-originated bank-loan limit; no unconditional borrowing/lending arbitrage is established here.

**Suggestion.** Add finite annual borrower demand and modest expected defaults or collection costs; price rates against those risks. Keep an emergency cash reserve in autonomous lending. Measure returns against houses and crafts before changing numbers.

**References:** `plugins/Dynastia.Mechanics.Loans/StandardLoanService.cs` (`GetOffers`, `CreateExternalReceivable`); `LoanTermsCalculator.Calculate`; `LoanPaymentYearSystem.MaterializeDuePayment`; `LoansPlugin.cs` (`loan.give`, `CanInitiateLoan`); `data/TownLife/bank_offer_quality.csv`.

### 4. Annual birth maximization and repeated youthful remarriage

At fertility 5 for both parents and maternal age at most 30, the active attempt changes annual conception chance from 16% to 80%. The reproduction path has no birth-spacing, maternal recovery, parity, or paternal-age modifier. Each child gives 100 score points immediately. A living older man with the Evil personality tag can receive female candidates aged 18–45 regardless of his age, with a younger-age bias. Consequently repeated annual attempts, and replacing an infertile or older spouse, can be mechanically attractive beyond credible family planning.

This is a **strategy risk, not a proven free loop**: each action uses time; children consume household resources; failure reduces marriage satisfaction by exactly two points; conception requires a shared household, an existing different-sex marriage predating that year, and maternal age 18–45. Maternal fertility declines after 30 and sharply after 40. Divorce retains the consequences above. Large, long-lived families are an intended achievement and should remain rewarded.

**Suggestion.** Model maternal recovery and health-sensitive fertility, with a paternal-age effect if desired. Make automatic birth attempts respect household care capacity and the survival prospects of existing children. Do not introduce death or hardship score deductions to solve this incentive.

**References:** `plugins/Dynastia.Mechanics.Reproduction/ReproductionYearSystem.cs` (`Execute`, `CalculateChildChance`, `CreateChildren`); `ReproductionEligibilityRules.CanAttemptMaritalConception`; `ReproductionBalanceRules`; `plugins/Dynastia.Mechanics.Relationships/RelationshipPersonalityRules.cs` (`TryChoosePartnerAge`, `TryGetPartnerAgeRange`); `StandardGameScoreService.ProcessEvent`.

## Cycling already prevented

- Marriage/divorce and employment/quit cycles reverse their active score claims. Career promotion points belong to the employment claim and are also removed when that employment ends. Retirement and death lock personal claims rather than penalize completed lives.
- Property sale reverses that property's purchase/extensions; farmland sale reverses its purchase/livestock claim. Livestock scores once per active parcel claim.
- Education levels, paid stat values, craft learning/mastery tiers, and heirloom identities are deduplicated. Moving or inheriting an existing heirloom does not repeatedly award its acquisition. Paid acquired stat bonuses are excluded from genetic inheritance (`ReproductionYearSystem.InheritStats` reads base stats).
- Partner candidates are deterministic by seeker, year, pool, and slot (`StandardPartnerSearchService.GetCandidatesFor`). Reopening the same search is not a candidate reroll.
- Artistic production is constrained by active self-employment, productive effort, and one production check per active craft/year. Ordinary production probabilities range from 1% to 12%; the guaranteed first Master work is once per person/craft. This is an intended income source, not an unlimited click exploit (`ArtisticWorkYearSystem`; `data/Heirlooms/artistic_work_generation_rules.csv`).

Recommended balance order: close extension farming and cash-only divorce shielding first, then simulate lending and reproduction changes before deciding their numbers.
