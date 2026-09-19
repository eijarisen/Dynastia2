using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

internal static partial class FamilyRelationActions
{
    private static GameActionDefinition CreateAskJobHelp(
        IFamilyRelationService relations,
        IHouseholdService households,
        IEconomyService economy,
        ICareerService career,
        IGameRandom random,
        IGameEventBus events,
        IFamilyService family,
        IGameState gameState) => new()
    {
        Id = "family_relations.ask_job_help",
        Label = "Use Family Connections",
        Description = "Ask this relative's household to use its strongest current career connection. Acceptance depends on Familiarity and Sympathy. Placements are normally two levels below that connection, with a small chance of one level better.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        IsAvailable = c => IsRelationsContext(c)
            && IsValidRelation(c, relations)
            && ResolveTargetHead(c.Target, households) is { } targetHead
            && !targetHead.Tags.Has("control.playable")
            && GetHighestCareerLevel(targetHead, economy, career, gameState) is var connectionLevel
            && FamilyCareerConnectionRules.CanProvideHelp(connectionLevel)
            && GetCareerHelpCandidates(c.Actor, connectionLevel, economy, career, gameState).Count > 0,
        Execute = c =>
        {
            var targetHead = ResolveTargetHead(c.Target, households);
            if (targetHead is null
                || targetHead.Tags.Has("control.playable"))
            {
                return new(false);
            }

            var connectionLevel =
                GetHighestCareerLevel(
                    targetHead,
                    economy,
                    career,
                    gameState);

            if (!FamilyCareerConnectionRules.CanProvideHelp(connectionLevel))
                return new(false);

            var candidates =
                GetCareerHelpCandidates(
                    c.Actor,
                    connectionLevel,
                    economy,
                    career,
                    gameState);

            if (candidates.Count == 0)
                return new(false);

            if (random.NextDouble() >= relations.EvaluateRequestWillingness(c.Actor, c.Target))
            {
                relations.RecordInteraction(c.Actor, c.Target, 2, -8);
                Publish(
                    events,
                    c,
                    "family_relations.job_help_refused",
                    family,
                    $"{family.GetDisplayName(c.Target)} declined to use family connections for {family.GetDisplayName(c.Actor)}'s household.");
                return new(true);
            }

            var helped =
                ApplyCareerHelp(
                    candidates,
                    connectionLevel,
                    career,
                    random);

            relations.RecordInteraction(c.Actor, c.Target, 5, 5);
            Publish(
                events,
                c,
                "family_relations.job_help_received",
                family,
                FormatCareerHelpSuccess(
                    family.GetDisplayName(c.Target),
                    helped,
                    family));
            return new(true);
        }
    };

    private static GameActionDefinition CreateGiveJobHelp(
        IFamilyRelationService relations,
        IHouseholdService households,
        IEconomyService economy,
        ICareerService career,
        IGameRandom random,
        IGameEventBus events,
        IFamilyService family,
        IGameState gameState) => new()
    {
        Id = "family_relations.give_job_help",
        Label = "Help with Careers",
        Description = "Offer the active household's strongest current career connection. Placements are normally two levels below that connection, with a small chance of one level better. Very hostile relatives may refuse the help.",
        Mode = ActionExecutionMode.Queued,
        QueuePhase = YearPhase.FamilyRelationActions,
        IsAvailable = c => IsRelationsContext(c)
            && IsValidRelation(c, relations)
            && ResolveTargetHead(c.Target, households) is { } targetHead
            && GetHighestCareerLevel(c.Actor, economy, career, gameState) is var connectionLevel
            && FamilyCareerConnectionRules.CanProvideHelp(connectionLevel)
            && GetCareerHelpCandidates(targetHead, connectionLevel, economy, career, gameState).Count > 0,
        Execute = c =>
        {
            var targetHead = ResolveTargetHead(c.Target, households);
            if (targetHead is null)
                return new(false);

            var connectionLevel =
                GetHighestCareerLevel(
                    c.Actor,
                    economy,
                    career,
                    gameState);

            if (!FamilyCareerConnectionRules.CanProvideHelp(connectionLevel))
                return new(false);

            var candidates =
                GetCareerHelpCandidates(
                    targetHead,
                    connectionLevel,
                    economy,
                    career,
                    gameState);

            if (candidates.Count == 0)
                return new(false);

            if (random.NextDouble() >= relations.EvaluateOfferWillingness(c.Actor, c.Target))
            {
                relations.RecordInteraction(c.Actor, c.Target, 1, -2);
                Publish(
                    events,
                    c,
                    "family_relations.job_help_offer_refused",
                    family,
                    $"{family.GetDisplayName(c.Target)} declined career help from {family.GetDisplayName(c.Actor)}'s household.");
                return new(true);
            }

            var helped =
                ApplyCareerHelp(
                    candidates,
                    connectionLevel,
                    career,
                    random);

            relations.RecordInteraction(c.Actor, c.Target, 5, 6);
            Publish(
                events,
                c,
                "family_relations.job_help_given",
                family,
                FormatCareerHelpSuccess(
                    family.GetDisplayName(c.Actor),
                    helped,
                    family));
            return new(true);
        }
    };

    private static IReadOnlyList<IPerson> GetCareerHelpCandidates(
        IPerson householdRepresentative,
        int connectionLevel,
        IEconomyService economy,
        ICareerService career,
        IGameState gameState)
    {
        var ids = economy.GetHouseholdMemberIds(householdRepresentative).ToHashSet();
        return gameState.People
            .Where(p => ids.Contains(p.Id)
                && p.Tags.Has("state.alive")
                && p.Age >= 18
                && !p.Tags.Has("state.imprisoned")
                && !p.Tags.Has("role.nanny")
                && !p.Tags.Has("role.family_nanny"))
            .Where(p =>
            {
                var snapshot = career.GetCareer(p);
                return !snapshot.IsRetired
                    && !snapshot.IsSelfEmployed
                    && snapshot.JobLevel
                        < FamilyCareerConnectionRules.GetStandardPlacementLevel(
                            connectionLevel);
            })
            .OrderByDescending(p => p.Id == householdRepresentative.Id)
            .ThenBy(p => p.Age)
            .ThenBy(p => p.Id)
            .ToList();
    }

    private static int GetHighestCareerLevel(
        IPerson householdRepresentative,
        IEconomyService economy,
        ICareerService career,
        IGameState gameState)
    {
        var ids = economy.GetHouseholdMemberIds(householdRepresentative).ToHashSet();
        return gameState.People
            .Where(p => ids.Contains(p.Id)
                && p.Tags.Has("state.alive")
                && p.Age >= 18
                && !p.Tags.Has("state.imprisoned")
                && !p.Tags.Has("role.nanny")
                && !p.Tags.Has("role.family_nanny"))
            .Select(p => career.GetCareer(p))
            .Where(snapshot => !snapshot.IsRetired)
            .Select(snapshot => snapshot.JobLevel)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static IReadOnlyList<IPerson> ApplyCareerHelp(
        IReadOnlyList<IPerson> candidates,
        int connectionLevel,
        ICareerService career,
        IGameRandom random)
    {
        var standardLevel =
            FamilyCareerConnectionRules.GetStandardPlacementLevel(
                connectionLevel);

        var exceptionalLevel =
            FamilyCareerConnectionRules.GetExceptionalPlacementLevel(
                connectionLevel);

        var helped = new List<IPerson>();

        foreach (var person in candidates)
        {
            var current = career.GetCareer(person);
            var targetLevel =
                random.NextDouble()
                    < FamilyCareerConnectionRules.ExceptionalPlacementChance
                    ? exceptionalLevel
                    : standardLevel;

            if (targetLevel <= current.JobLevel)
                continue;

            career.SetJobLevel(person, targetLevel);
            helped.Add(person);
        }

        return helped;
    }

    private static string FormatCareerHelpSuccess(
        string connectorName,
        IReadOnlyList<IPerson> helped,
        IFamilyService family)
    {
        if (helped.Count == 0)
            return $"{connectorName} tried to use family connections, but no suitable placement was found.";

        var names = helped
            .Select(family.GetDisplayName)
            .ToList();

        var people = names.Count == 1
            ? names[0]
            : string.Join(", ", names.Take(names.Count - 1))
                + $" and {names[^1]}";

        return helped.Count == 1
            ? $"{connectorName} used family connections to secure a better position for {people}."
            : $"{connectorName} used family connections to secure better positions for {people}.";
    }
}
