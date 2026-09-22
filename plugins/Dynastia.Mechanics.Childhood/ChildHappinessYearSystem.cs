using Dynastia.Contracts;

namespace Dynastia.Mechanics.Childhood;

public sealed class ChildHappinessYearSystem : IYearSystem
{
    private readonly IChildHappinessService _happiness;
    private readonly IHealthService _health;
    private readonly IEconomyService _economy;
    private readonly IFamilyService _family;
    private readonly IHouseholdService _households;
    private readonly ChildhoodBalanceRules _rules;
    private readonly IPersonalityService _personality;
    private readonly IGameRandom _random;

    internal ChildHappinessYearSystem(
        IChildHappinessService happiness,
        IHealthService health,
        IEconomyService economy,
        IFamilyService family,
        IHouseholdService households,
        ChildhoodBalanceRules rules,
        IPersonalityService personality,
        IGameRandom random)
    {
        _happiness = happiness;
        _health = health;
        _economy = economy;
        _family = family;
        _households = households;
        _rules = rules;
        _personality = personality;
        _random = random;
    }

    public string Id => "childhood.happiness";
    public YearPhase Phase => YearPhase.Status;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => Array.Empty<string>();

    public void Execute(IGameState gameState)
    {
        foreach (var child in gameState.People.Where(p =>
                     p.Tags.Has("state.alive") && p.Age < 18))
        {
            _happiness.EnsureHappiness(child);
            var health = _health.GetHealth(child).Current;
            var household = _economy.GetHousehold(child);

            if (health < 35)
                _happiness.ChangeHappiness(child, -1);
            else if (health < 60 && _random.NextDouble() < 0.45)
                _happiness.ChangeHappiness(child, -1);
            else if (health >= 90
                     && child.Tags.Has("personality.sanguine")
                     && _random.NextDouble() < 0.30)
                _happiness.ChangeHappiness(child, 1);

            if (household?.HasUnfundedBasicNeeds == true
                && _random.NextDouble() < 0.55)
            {
                _happiness.ChangeHappiness(child, -1);
            }

            var lifestyle = HouseholdLifestyleRules.GetStance(child);
            var lifestyleChance = HouseholdLifestyleRules.GetMoraleShiftChance(child);
            if (lifestyleChance > 0
                && _random.NextDouble() < lifestyleChance)
            {
                _happiness.ChangeHappiness(
                    child,
                    lifestyle == HouseholdLifestyleStance.Lavish ? 1 : -1);
            }

            var temperamentCurrent = _happiness.GetHappiness(child)?.Value ?? 3;

            if ((child.Tags.Has("personality.melancholic")
                 || child.Tags.Has("personality.choleric"))
                && _random.NextDouble() < 0.15)
            {
                _happiness.ChangeHappiness(child, -1);
            }
            else if (child.Tags.Has("personality.phlegmatic")
                     && temperamentCurrent != 3
                     && _random.NextDouble() < 0.35)
            {
                _happiness.ChangeHappiness(child, temperamentCurrent < 3 ? 1 : -1);
            }
            else if (child.Tags.Has("personality.sanguine")
                     && temperamentCurrent < 3
                     && _random.NextDouble() < 0.25)
            {
                _happiness.ChangeHappiness(child, 1);
            }

            TryStableCareRecovery(gameState, child, health, household);

            var current = _happiness.GetHappiness(child)?.Value ?? 3;
            if (child.Age < 5 || current > 2)
                continue;

            var moralsDownChance = current == 1 ? 0.04 : 0.02;
            moralsDownChance = PersonalityInfluence.AdjustProbability(
                moralsDownChance,
                child,
                melancholic: 0.20,
                phlegmatic: -0.25,
                choleric: 0.20);

            if (_random.NextDouble() < moralsDownChance)
                _personality.ShiftMorals(child, -1);
        }
    }
    private void TryStableCareRecovery(
        IGameState gameState,
        IPerson child,
        double health,
        HouseholdFinanceSnapshot? finance)
    {
        var current = _happiness.GetHappiness(child)?.Value ?? 3;
        if (current >= _rules.StableCareTarget
            || health < _rules.StableCareHealthMinimum
            || finance is null
            || finance.FundingYear != gameState.Year
            || finance.BasicNeedsShortfall > 0m)
        {
            return;
        }

        var householdStatus = _households.GetStatus(child);
        if (householdStatus is null
            || householdStatus.IsLargeFamilyStrained
            || householdStatus.IsOvercrowded
            || !ChildhoodCareRules.HasAvailableResidentCaregiver(
                child,
                gameState,
                _family,
                _economy,
                _households))
        {
            return;
        }

        var state = child.Components.Get<ChildHappinessComponent>();
        if (state is null
            || gameState.Year <= state.RecoveryBlockedThroughYear
            || !_random.Chance(_rules.StableCareRecoveryChance))
        {
            return;
        }

        _happiness.ChangeHappiness(child, 1);
    }

}
