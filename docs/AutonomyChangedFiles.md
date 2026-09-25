# Autonomy package changes

Modified source files:

- `docs/DevelopmentMap.md`
- `docs/GameDesign/01-Core-Loop-and-Control.md`
- `plugins/Dynastia.Mechanics.GameScore/GameScorePlugin.cs`
- `plugins/Dynastia.Mechanics.GameScore/StandardGameScoreService.cs`
- `plugins/Dynastia.Mechanics.Households/AdvancedAutonomousHouseholdStrategy.cs`
- `plugins/Dynastia.Mechanics.Households/AutonomousHouseholdDecisionService.cs`
- `plugins/Dynastia.Mechanics.Households/AutonomousStrategyModels.cs`
- `plugins/Dynastia.Mechanics.Households/AutonomousStrategyRules.cs`
- `plugins/Dynastia.Mechanics.Households/Autonomy/AutonomousActionCandidateBuilder.cs`
- `plugins/Dynastia.Mechanics.Households/Autonomy/AutonomousCareerEducationScorer.cs`
- `plugins/Dynastia.Mechanics.Households/Autonomy/AutonomousFamilyContinuityScorer.cs`
- `plugins/Dynastia.Mechanics.Households/Autonomy/AutonomousFamilyRelationsScorer.cs`
- `plugins/Dynastia.Mechanics.Households/Autonomy/AutonomousFinancePropertyScorer.cs`
- `plugins/Dynastia.Mechanics.Households/Autonomy/AutonomousHealthScorer.cs`
- `plugins/Dynastia.Mechanics.Households/Autonomy/AutonomousPersonalDevelopmentScorer.cs`
- `plugins/Dynastia.Mechanics.Households/Autonomy/AutonomousReproductiveEligibility.cs`
- `plugins/Dynastia.Mechanics.Households/Autonomy/AutonomousSnapshotBuilder.cs`
- `plugins/Dynastia.Mechanics.Households/HouseholdsPlugin.cs`
- `src/Dynastia.App/Dynastia.App.csproj`
- `tests/Dynastia.Core.Tests/AutonomousStrategyCharacterizationTests.cs`
- `tests/Dynastia.Core.Tests/AutonomousStrategyDecompositionTests.cs`
- `tests/Dynastia.Core.Tests/AutonomousStrategyRulesTests.cs`

Added source files:

- `docs/AutonomyBalanceAudit.md`
- `docs/AutonomyRework.md`
- `docs/AutonomyValidation.md`
- `plugins/Dynastia.Mechanics.Households/Autonomy/AutonomousGameScoreEstimator.cs`
- `plugins/Dynastia.Mechanics.Households/Autonomy/AutonomousPartnerChoiceRules.cs`
- `src/Dynastia.Contracts/IGameScorePreviewService.cs`
- `tests/Dynastia.Core.Tests/AutonomousDecisionPriorityTests.cs`
- `tests/Dynastia.Core.Tests/AutonomousGameScoreEstimatorTests.cs`
- `tests/Dynastia.Core.Tests/AutonomousLineageContinuityTests.cs`
- `tests/Dynastia.Core.Tests/AutonomousSustainableDevelopmentTests.cs`

The source archive excludes generated artifacts using `build/repository-artifact-policy.ps1`.
