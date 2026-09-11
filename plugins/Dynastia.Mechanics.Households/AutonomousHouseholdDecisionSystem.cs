using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

internal sealed class AutonomousHouseholdDecisionSystem :
    IYearSystem
{
    private readonly IGameState _gameState;
    private readonly IHouseholdService _households;
    private readonly IActionRegistry _actions;
    private readonly IEconomyService _economy;
    private readonly IGameRandom _random;

    public AutonomousHouseholdDecisionSystem(
        IGameState gameState,
        IHouseholdService households,
        IActionRegistry actions,
        IEconomyService economy,
        IGameRandom random)
    {
        _gameState =
            gameState;

        _households =
            households;

        _actions =
            actions;

        _economy =
            economy;

        _random =
            random;
    }

    public string Id =>
        "households.autonomous_decisions";

    public YearPhase Phase =>
        YearPhase.PreYear;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        Array.Empty<string>();

    public void Execute(
        IGameState gameState)
    {
        _households.ReconcileHouseholds();

        foreach (var household in
            _households
                .GetActiveHouseholds()
                .Where(
                    household =>
                        household.Class
                        == HouseholdClass.Bloodline))
        {
            var head =
                FindPerson(
                    household.HeadId);

            if (head is null
                || !head.Tags.Has(
                    "state.alive"))
            {
                continue;
            }

            var options =
                BuildOptions(
                    household,
                    head);

            if (options.Count == 0)
                continue;

            var top =
                options
                    .OrderByDescending(
                        option =>
                            option.Score)
                    .ThenBy(
                        option =>
                            option.Action.Id,
                        StringComparer.OrdinalIgnoreCase)
                    .Take(
                        3)
                    .ToList();

            var index =
                Math.Min(
                    top.Count - 1,
                    (int)(
                        _random.NextDouble()
                        * top.Count
                    ));

            var selected =
                top[index];

            _actions.ExecuteAutonomous(
                selected.Action.Id,
                head,
                selected.Target);
        }
    }

    private List<DecisionOption> BuildOptions(
        HouseholdInfo household,
        IPerson head)
    {
        var result =
            new List<DecisionOption>();

        var finance =
            _economy.GetHousehold(
                head);

        var status =
            _households.GetStatus(
                head);

        var members =
            household.MemberIds
                .Select(
                    FindPerson)
                .Where(
                    person =>
                        person is not null
                        && person.Tags.Has(
                            "state.alive"))
                .Cast<IPerson>()
                .DistinctBy(
                    person =>
                        person.Id)
                .ToList();

        if (!members.Any(
            member =>
                member.Id
                == head.Id))
        {
            members.Insert(
                0,
                head);
        }

        foreach (var target in
            members)
        {
            foreach (var action in
                _actions
                    .GetMechanicallyAvailableActions(
                        head,
                        target))
            {
                var score =
                    Score(
                        action.Id,
                        finance,
                        status);

                if (score <= 0)
                    continue;

                result.Add(
                    new DecisionOption(
                        action,
                        target,
                        score));
            }
        }

        return result
            .GroupBy(
                option =>
                    (
                        option.Action.Id,
                        option.Target.Id
                    ))
            .Select(
                group =>
                    group.First())
            .ToList();
    }

    private static int Score(
        string actionId,
        HouseholdFinanceSnapshot? finance,
        HouseholdStatusSnapshot? status)
    {
        var broke =
            status?.IsBroke
            == true;

        var strained =
            status?.IsLargeFamilyStrained
            == true;

        return actionId.ToLowerInvariant()
            switch
            {
                "turn.pass" =>
                    0,

                "relationship.divorce_spouse" =>
                    0,

                "household.fire_nanny" =>
                    0,

                "career.seek_employment" =>
                    broke
                        ? 100
                        : 82,

                "career.help_seek_employment" =>
                    broke
                        ? 95
                        : 76,

                "career.use_family_connections" =>
                    94,

                "family_support.ask_parents" =>
                    broke
                        ? 92
                        : 20,

                "family_support.ask_child" =>
                    broke
                        ? 92
                        : 20,

                "wellbeing.heal_relative" =>
                    92,

                "wellbeing.therapy" =>
                    88,

                "wellbeing.recover" =>
                    72,

                "career.ask_to_recover" =>
                    72,

                "relationship.repair_marriage" =>
                    80,

                "household.ask_daughter_nanny" =>
                    strained
                        ? 84
                        : 50,

                "household.sell_house" =>
                    broke
                        ? 86
                        : 22,

                "relationship.find_spouse" =>
                    62,

                "reproduction.try_for_baby" =>
                    broke
                        ? 25
                        : 58,

                "education.get_education" =>
                    broke
                        ? 25
                        : 52,

                "education.help_learning" =>
                    52,

                "household.buy_house" =>
                    finance?.Wealth >= 20000m
                        ? 42
                        : 10,

                "relationship.marry_off_daughter" =>
                    44,

                "career.work_harder" =>
                    38,

                "career.quit_job" =>
                    35,

                "career.ask_to_quit" =>
                    35,

                "stats.improve_strength" or
                "stats.improve_intellect" or
                "stats.improve_immunity" or
                "stats.improve_appeal" or
                "stats.improve_longevity" or
                "stats.improve_fertility" =>
                    finance?.Wealth >= 20000m
                        ? 28
                        : 0,

                _ =>
                    0
            };
    }

    private IPerson? FindPerson(
        Guid id)
    {
        return _gameState.People
            .FirstOrDefault(
                person =>
                    person.Id
                    == id);
    }

    private sealed record DecisionOption(
        GameActionDefinition Action,
        IPerson Target,
        int Score);
}
