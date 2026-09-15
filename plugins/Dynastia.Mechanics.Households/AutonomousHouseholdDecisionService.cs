using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

internal sealed class AutonomousHouseholdDecisionService :
    IAutonomousHouseholdDecisionService
{
    private readonly IGameState _gameState;
    private readonly IHouseholdService _households;
    private readonly IActionRegistry _actions;
    private readonly IEconomyService _economy;
    private readonly IHealthService _health;
    private readonly ICareerService _career;
    private readonly IGameRandom _random;

    public AutonomousHouseholdDecisionService(
        IGameState gameState,
        IHouseholdService households,
        IActionRegistry actions,
        IEconomyService economy,
        IHealthService health,
        ICareerService career,
        IGameRandom random)
    {
        _gameState = gameState;
        _households = households;
        _actions = actions;
        _economy = economy;
        _health = health;
        _career = career;
        _random = random;
    }

    public int QueueActionsForAllHouseholds()
    {
        return QueueActions(
            includeLineage: true,
            replaceExisting: true);
    }

    public int QueueActionsForAutonomousHouseholds()
    {
        return QueueActions(
            includeLineage: false,
            replaceExisting: false);
    }

    private int QueueActions(
        bool includeLineage,
        bool replaceExisting)
    {
        _households.ReconcileHouseholds();

        var households =
            _households
                .GetActiveHouseholds()
                .Where(
                    household =>
                        includeLineage
                        || household.Class
                            == HouseholdClass.Bloodline)
                .ToList();

        if (replaceExisting)
        {
            foreach (var household in households)
            {
                var head =
                    FindPerson(
                        household.HeadId);

                if (head is not null)
                {
                    _actions.CancelQueuedActions(
                        head);
                }
            }
        }

        var queuedCount = 0;

        foreach (var household in households)
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

            var selected =
                ChooseOption(
                    top);

            var result =
                _actions.ExecuteAutonomous(
                    selected.Action.Id,
                    head,
                    selected.Target);

            if (result.Success)
                queuedCount++;
        }

        return queuedCount;
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

        var headHealth =
            _health.GetHealth(
                head);

        var headCareer =
            _career.GetCareer(
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
            var targetHealth =
                target.Id == head.Id
                    ? headHealth
                    : _health.GetHealth(
                        target);

            var targetCareer =
                target.Id == head.Id
                    ? headCareer
                    : target.Age >= 18
                        ? _career.GetCareer(
                            target)
                        : null;

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
                        status,
                        head,
                        target,
                        headHealth,
                        targetHealth,
                        headCareer,
                        targetCareer);

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
        HouseholdStatusSnapshot? status,
        IPerson head,
        IPerson target,
        HealthSnapshot headHealth,
        HealthSnapshot targetHealth,
        CareerSnapshot headCareer,
        CareerSnapshot? targetCareer)
    {
        var broke =
            status?.IsBroke
            == true;

        var strained =
            status?.IsLargeFamilyStrained
            == true;

        var id =
            actionId.ToLowerInvariant();

        var baseScore =
            id switch
            {
                "turn.pass" =>
                    broke
                    || strained
                    || headHealth.Percentage < 75
                        ? 1
                        : head.Tags.Has("personality.phlegmatic")
                            ? 45
                            : 5,

                "relationship.divorce_spouse" =>
                    0,

                "household.fire_nanny" =>
                    0,

                "career.seek_employment" =>
                    broke
                        ? 100
                        : 82,

                "career.find_another_job" =>
                    broke
                        ? 68
                        : 46,

                "personality.religious_study" =>
                    head.Tags.Has("morals.evil")
                        ? 42
                        : head.Tags.Has("morals.neutral")
                            ? 34
                            : 20,

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
                    95
                    + HealthUrgency(
                        targetHealth.Percentage)
                    + (target.Age < 18
                        ? 25
                        : 0),

                "wellbeing.therapy" =>
                    92 + MentalHealthUrgency(
                        targetHealth),

                "wellbeing.recover" =>
                    72
                    + HealthUrgency(
                        headHealth.Percentage)
                    + (broke ? 18 : 0)
                    + (headCareer.JobSatisfaction == 1
                        ? 18
                        : 0),

                "wellbeing.drink" =>
                    headHealth.Percentage <= 55
                        ? 0
                        : headHealth.Percentage <= 70
                            ? 22
                            : 58,

                "career.ask_to_recover" =>
                    76
                    + HealthUrgency(
                        targetHealth.Percentage)
                    + (targetCareer?.JobSatisfaction == 1
                        ? 12
                        : 0),

                "relationship.repair_marriage" =>
                    80,

                "household.ask_daughter_nanny" =>
                    strained
                        ? 84
                        : 50,

                // Property selection requires an explicit asset choice.
                // Autonomous households do not buy/sell/move property.
                "household.sell_house" =>
                    0,

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

                "childhood.raise_child" =>
                    56,

                "household.buy_house" =>
                    0,

                "relationship.marry_off_daughter" =>
                    44,

                "career.work_harder" =>
                    headHealth.Percentage <= 50
                        ? 0
                        : headHealth.Percentage <= 70
                            ? 18
                            : 38,

                "career.quit_job" =>
                    headCareer.JobSatisfaction == 1
                        ? 72
                        : 18,

                "career.ask_to_quit" =>
                    targetCareer?.JobSatisfaction == 1
                        ? 48
                        : 20,

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

        if (broke
            && (id is "family_support.ask_parents"
                or "family_support.ask_child"
                or "career.seek_employment"
                or "career.help_seek_employment"))
        {
            baseScore +=
                headHealth.Percentage < 55
                    ? 25
                    : 12;
        }

        if (strained
            && id == "household.ask_daughter_nanny")
        {
            baseScore += 22;
        }

        if (baseScore <= 0)
            return baseScore;

        var melancholic = 0.0;
        var phlegmatic = 0.0;
        var sanguine = 0.0;
        var choleric = 0.0;
        var good = 0.0;
        var evil = 0.0;

        if (id is "wellbeing.recover"
            or "wellbeing.therapy"
            or "relationship.repair_marriage")
        {
            melancholic += 0.20;
        }

        if (id is "career.seek_employment"
            or "career.help_seek_employment"
            or "career.work_harder"
            or "relationship.find_spouse"
            or "reproduction.try_for_baby"
            or "education.get_education")
        {
            sanguine += 0.15;
        }

        if (id is "career.work_harder"
            or "career.quit_job"
            or "wellbeing.drink")
        {
            choleric += 0.20;
        }

        if (id is "career.work_harder"
            or "career.quit_job"
            or "career.ask_to_quit")
        {
            phlegmatic -= 0.15;
        }

        var mainlyHelpsAnother =
            target.Id != head.Id
            && (id is "wellbeing.heal_relative"
                or "education.help_learning"
                or "career.help_seek_employment"
                or "career.ask_to_recover"
                or "household.ask_daughter_nanny");

        if (mainlyHelpsAnother)
        {
            good +=
                id == "wellbeing.heal_relative"
                    && target.Age < 18
                        ? 0.20
                        : 0.10;

            evil -= 0.10;
        }

        if (id == "wellbeing.drink")
        {
            good -= 0.10;
            evil += 0.10;
        }

        var multiplier =
            PersonalityInfluence.Multiplier(
                head,
                melancholic,
                phlegmatic,
                sanguine,
                choleric,
                good: good,
                evil: evil);

        return (int)Math.Round(
            baseScore * multiplier,
            MidpointRounding.AwayFromZero);
    }

    private DecisionOption ChooseOption(
        IReadOnlyList<DecisionOption> options)
    {
        if (options.Count == 1)
            return options[0];

        var highest =
            options[0];

        // A clearly dominant emergency response should not be lost to an
        // arbitrary routine choice. When several urgent responses compete,
        // preserve variation with the same weighted choice as normal AI.
        if (highest.Score >= 150
            && (options.Count == 1
                || highest.Score
                    - options[1].Score >= 30))
        {
            return highest;
        }

        var weights =
            options
                .Select(
                    option =>
                        Math.Max(
                            1L,
                            (long)option.Score
                            * option.Score))
                .ToArray();

        var total =
            weights.Sum();

        var roll =
            _random.NextDouble()
            * total;

        for (var index = 0;
             index < options.Count;
             index++)
        {
            if (roll < weights[index])
                return options[index];

            roll -=
                weights[index];
        }

        return options[^1];
    }

    private static int HealthUrgency(
        double percentage)
    {
        return percentage switch
        {
            <= 25 => 135,
            <= 40 => 100,
            <= 55 => 70,
            <= 70 => 42,
            <= 80 => 20,
            _ => 0
        };
    }

    private static int MentalHealthUrgency(
        HealthSnapshot health)
    {
        var seriousMentalCondition =
            health.Conditions.Any(
                condition =>
                    condition.Id.Equals(
                        "alcoholism",
                        StringComparison.OrdinalIgnoreCase)
                    || condition.Id.Equals(
                        "depression",
                        StringComparison.OrdinalIgnoreCase)
                    || condition.Id.Equals(
                        "anxiety",
                        StringComparison.OrdinalIgnoreCase));

        if (!seriousMentalCondition)
            return 0;

        return health.Percentage switch
        {
            <= 40 => 70,
            <= 60 => 45,
            <= 75 => 25,
            _ => 12
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
