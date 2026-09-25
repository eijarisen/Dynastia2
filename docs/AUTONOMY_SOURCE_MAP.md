# Autonomy source map — D15-003

| Responsibility | Current owner |
| --- | --- |
| Autonomous household enumeration, queue preservation/replacement, one queued choice | `plugins/Dynastia.Mechanics.Households/AutonomousHouseholdDecisionService.cs` |
| Snapshot of health, finance, children, succession diagnostics, care/residence and reproduction | `Autonomy/AutonomousSnapshotBuilder.cs`, `AutonomousStrategyModels.cs`, `AutonomousStrategyRules.cs` |
| Mechanical action availability and concrete parameters | `Autonomy/AutonomousActionCandidateBuilder.cs`; normal `IActionRegistry` definitions remain authoritative |
| Urgency/utility selection, personality, bounded randomness, final succession tie | `AdvancedAutonomousHouseholdStrategy.cs` |
| Health/care policy | `Autonomy/AutonomousHealthScorer.cs` |
| Conception, spouse search, child raising and arranged marriage | `Autonomy/AutonomousFamilyContinuityScorer.cs` |
| Employment/education/craft prerequisites | `Autonomy/AutonomousCareerEducationScorer.cs` |
| Housing, loans, farmland and independent rented branches | `Autonomy/AutonomousFinancePropertyScorer.cs` |
| Support between related households | `Autonomy/AutonomousFamilyRelationsScorer.cs` |
| Optional/self-development spending | `Autonomy/AutonomousPersonalDevelopmentScorer.cs` |
| Canonical annual household forecast and local living/rent costs | `Dynastia.Mechanics.Economy.StandardEconomyService` through `IEconomyService` |
| Ordinary rented branch execution | `Dynastia.Mechanics.FamilyRelations/FamilyRelationActions.MoveOut.cs` and `StandardHouseholdService.ResidentBranch.cs` |
| Arranged marriage execution and daughter household seeding | `Dynastia.Mechanics.Relationships/StandardPartnerSearchService.cs` |
| Queue execution/revalidation | `Dynastia.Core.Actions.ActionRegistry` / queued action year systems |

The planner does not duplicate the household transfer executor. A property-less move-out intentionally uses the existing rented-independent-household path. D15-003 exposes Economy's default residence capacity through `IEconomyService.GetDefaultResidenceCapacity` so the planner does not hard-code the house-capacity rule.

Normal mode leaves player-owned queues untouched and runs autonomy only for households designated autonomous by `AutonomousHouseholdDecisionService`. Simulate-all clears eligible queues first but then invokes the same strategy and action system.
