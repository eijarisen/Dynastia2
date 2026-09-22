# Design Decisions

This is an append-only decision log. Durable implemented rules belong in the relevant domain document; this file keeps the reason/history and points to the final rule.

## Entry format

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

---

ID: **GD-2026-001**  
Status: **Implemented**  
Date: **2026-09-22**  
Area: **Documentation / Instructions**  
Decision: Maintain a compact seven-tab player Instructions guide separately from a modular developer-facing `docs/GameDesign/` reference.  
Reasoning: Player help should orient without exposing hidden formulas; future design/implementation chats need exact current rules, ownership, interactions, persistence and test pointers without reconstructing conversation history.  
Affected mechanics: all, documentation only.  
Implementation notes: player guide is `src/Dynastia.App/Views/InstructionsWindow.axaml`; developer index starts at `docs/GameDesign/00-Index.md`; technical ownership remains in `docs/DevelopmentMap.md`.  
Save compatibility: none.  
Documentation to update: the owning domain file for every future durable gameplay change; `08-Actions-and-UI-Surfaces.md` when actions/surfaces change.  
Supersedes: conversation-dependent documentation as the normal starting point for future work.

---

ID: **GD-2026-002**  
Status: **Implemented**  
Date: **2026-09-22**  
Area: **Community / Performance**  
Decision: Full annual Community policy resolution is only required for towns containing playable lineage households and towns with an outstanding player lobby; irrelevant towns are skipped.  
Reasoning: Simulating every historical town provided no player-facing benefit and made `community.policy_resolution` disproportionately slow.  
Affected mechanics: `dynastia.community`, Town Affairs Community.  
Implementation notes: `CommunityPolicyService` builds the set of relevant town IDs before proposal/policy resolution.  
Save compatibility: none.  
Documentation to update: `06-Towns-Institutions-and-Community.md`.  
Supersedes: implicit full-world policy iteration.

---

ID: **GD-2026-003**  
Status: **Implemented**  
Date: **2026-09-22**  
Area: **Community connections**  
Decision: Accepting a requested spare house/farmland establishes a Warm connection, and Warm/Close acquaintances are not automatically discarded merely because the donor became Poor after helping.  
Reasoning: A major accepted favor should strengthen, not terminate, the social connection.  
Affected mechanics: `dynastia.community`.  
Implementation notes: connection action/result and annual retention rules share the revised relationship behavior.  
Save compatibility: no schema migration.  
Documentation to update: `06-Towns-Institutions-and-Community.md`.  
Supersedes: prior post-gift relation penalties/poor-contact cleanup behavior.

---

ID: **GD-2026-004**
Status: **Implemented**
Date: **2026-09-22**
Area: **Economy / Deprivation**
Decision: Household deprivation is determined by current-year basic-needs funding rather than final cash balance; late Finance receipts cover any basic-needs shortfall before becoming spendable cash.
Reasoning: A household that spends its available resources exactly on ordinary necessities should not receive poverty penalties merely for ending at zero, while late receivables should not hide an otherwise unfunded year.
Affected mechanics: Economy, Loans, Households, Health, Stress, Childhood, Relationships, Thoughts, Justice, Personality, Rare Events.
Implementation notes: `HouseholdEconomyComponent` persists the annual funding record. `ApplyAnnualFinanceReceipt` reconciles late Finance income and `LoanPaymentYearSystem` resolves creditor allocations before borrower debits.
Save compatibility: additive fields only; stale/older saves use a conservative presentation fallback until the next Economy pass creates an authoritative funding record.
Documentation to update: `03-Health-Wellbeing-and-Life-Course.md`, `05-Households-Economy-and-Property.md`.
Supersedes: deprivation inferred solely from `Wealth <= 0`.

---

ID: **GD-2026-005**
Status: **Implemented**
Date: **2026-09-22**
Area: **Community / Civic office**
Decision: Civic-office historical profile lookup freezes at the shared technology horizon, while office simulation continues using the actual year.
Reasoning: Post-horizon games need stable historical content without freezing ages, appointment dates, approval processing or salary eligibility at 2026.
Affected mechanics: Community, Career/Status, Town Affairs.
Implementation notes: profile lookup clamps to `GameCalendarConfiguration.TechnologyFreezeYear`; incumbent validity is reconciled consistently across tags, office queries and salary.
Save compatibility: valid post-horizon incumbents retain approval and actual appointment year; invalid stale incumbents are reconciled.
Documentation to update: `04-Work-Education-Crafts-and-Status.md`, `06-Towns-Institutions-and-Community.md`.
Supersedes: civic profile lookup failing beyond the data horizon.

---

ID: **GD-2026-006**
Status: **Implemented**
Date: **2026-09-22**
Area: **Reproduction**
Decision: Passive marital conception and `Try for Baby` share one symmetric eligibility rule for both prospective parents.
Reasoning: Conception availability should not depend on which spouse happened to own a legacy eligibility check; imprisonment, vocation, simulation/residence state, household membership and newly formed marriage apply to both parents consistently.
Affected mechanics: Reproduction, Family, Economy.
Implementation notes: both active and passive paths call `ReproductionEligibilityRules.CanAttemptMaritalConception`; the active action revalidates at execution. Nonmarital births retain their separate rules with inactive/external-mother guards.
Save compatibility: none.
Documentation to update: `02-People-Family-and-Relationships.md`.
Supersedes: mother-focused marital conception availability checks.

---

ID: **GD-2026-007**
Status: **Implemented**
Date: **2026-09-22**
Area: **Work / Artistic production**
Decision: Crafts, Farming and artistic-work production share one deterministic annual productive-effort calculation combining work capacity with the already-rolled Recover reduction.
Reasoning: Health incapacity and Recover should reduce productive output consistently, without rerolling Recover or consuming RNG separately in each mechanic.
Affected mechanics: Contracts, Health, Wellbeing, Crafts, Farming, Heirlooms.
Implementation notes: `AnnualProductiveEffortRules` is RNG-free. Zero productive effort blocks art without consuming a due Master guarantee; positive effort scales ordinary production chance. Royalties are unchanged.
Save compatibility: none.
Documentation to update: `04-Work-Education-Crafts-and-Status.md`.
Supersedes: duplicated Recover/output calculations and art production ignoring work capacity.

---

ID: **GD-2026-008**
Status: **Implemented**
Date: **2026-09-22**
Area: **Farming / Household labor**
Decision: Farm contribution is age-scaled and scarce worker slots prefer the highest deterministic expected productive contribution.
Reasoning: Children may plausibly help family farms without producing adult-level output or displacing healthy adults merely because of household list order.
Affected mechanics: Farming, productive effort.
Implementation notes: `worker_age_contribution.json` defines 25% for ages 10–13, 50% for ages 14–17 and 100% for adults. Forecast and realized output use the same age × work-capacity × Recover contribution.
Save compatibility: derived from current age; no migration.
Supersedes: every eligible farm worker contributing full adult output.

---

ID: **GD-2026-009**
Status: **Implemented**
Date: **2026-09-22**
Area: **Community connections**
Decision: Meaningful acquaintance interactions grant two full years of passive-decay grace, and Improve Relations grants +6 Familiarity/+3 Sympathy.
Reasoning: Acquaintance-building should reward occasional deliberate contact rather than require annual maintenance actions.
Affected mechanics: Community connections.
Implementation notes: successful improvements/gifts/accepted requests/lobby-created contacts stamp `LastMeaningfulInteractionYear`; refusals do not. Old active saves receive a one-time transition grace.
Save compatibility: additive nullable timestamp.
Supersedes: unconditional annual relation decay after every interaction.

---

ID: **GD-2026-010**
Status: **Implemented**
Date: **2026-09-22**
Area: **Childhood / Happiness**
Decision: Stable care provides passive mean reversion toward Content for unhappy children, with trauma recovery blocks and co-resident scope for parent work/recovery side effects.
Reasoning: Healthy, adequately cared-for children need a passive route out of long-term Misery without erasing temperament or major adversity.
Affected mechanics: Childhood, Health, Economy, Households, Family.
Implementation notes: recovery requires Health ≥75, no current basic-needs shortfall, no crowding/large-family strain and an available resident caregiver; serious illness, parental divorce/affair and parent/caregiver death block recovery through event year +2.
Save compatibility: additive recovery-block field; old saves retain Happiness and start unblocked.
Supersedes: no general stable-care recovery path.

---

ID: **GD-2026-011**
Status: **Implemented**
Date: **2026-09-22**
Area: **Occupational income volatility**
Decision: Craft and criminal-occupation long-tail income use one mean-preserving cap-and-rescale curve with a shared tail cap of 20×.
Reasoning: High mastery should remain profitable without a single ordinary annual roll dominating decades of household finances.
Affected mechanics: Contracts, Crafts, Justice.
Implementation notes: the same original income roll is remapped with no additional RNG. Novice is unchanged; the Master maximum is about 26.66× while its pre-rounding expected multiplier remains about 5.40668×.
Save compatibility: no persisted-state migration; future payouts change for the same roll while RNG call order is preserved.
Supersedes: uncapped reciprocal outcomes up to 100× at Master.
