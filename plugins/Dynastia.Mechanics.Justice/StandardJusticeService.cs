using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

public sealed class StandardJusticeService : IJusticeService
{
    private readonly IGameState? _gameState;
    private readonly IFamilyService? _family;
    private readonly ICareerService? _career;
    private readonly CourtJusticeRules? _courtRules;
    private readonly Func<IFamilyRelationService?>? _relationsResolver;
    private readonly IEconomyService? _economy;
    private readonly Func<IHouseholdConnectionService?>? _connectionsResolver;

    public StandardJusticeService()
    {
    }

    public StandardJusticeService(
        IGameState gameState,
        IFamilyService family,
        ICareerService career,
        CourtJusticeRules courtRules,
        Func<IFamilyRelationService?> relationsResolver,
        IEconomyService? economy = null,
        Func<IHouseholdConnectionService?>? connectionsResolver = null)
    {
        _gameState = gameState;
        _family = family;
        _career = career;
        _courtRules = courtRules;
        _relationsResolver = relationsResolver;
        _economy = economy;
        _connectionsResolver = connectionsResolver;
    }

    public CourtJusticeRules? CourtRules => _courtRules;

    public void EnsureJustice(IPerson person)
    {
        var component = person.Components.Get<JusticeComponent>();
        if (component is null)
        {
            person.Components.Set(new JusticeComponent());
            return;
        }

        component.CriminalRecord ??= [];
        if (component.PrisonSentence > 0)
        {
            component.CurrentImprisonmentId ??= Guid.NewGuid();
            person.Tags.Add("state.imprisoned");
        }
        else
        {
            component.CurrentImprisonmentId = null;
            component.EscapeAttemptedCurrentImprisonment = false;
            person.Tags.Remove("state.imprisoned");
        }
    }

    public JusticeSnapshot GetStatus(IPerson person)
    {
        var justice = GetMutable(person);
        return new JusticeSnapshot(
            justice.PrisonSentence > 0,
            justice.PrisonSentence,
            justice.PrisonSentence >= 50,
            justice.CrimeId,
            justice.CrimeName,
            justice.CrimeDescription,
            justice.EscapeAttemptedCurrentImprisonment,
            justice.CriminalRecord
                .Select(entry => new CriminalRecordEntryInfo(
                    entry.Year,
                    entry.CrimeId,
                    entry.CrimeName,
                    entry.OriginalSentence,
                    entry.FinalSentence))
                .ToArray());
    }

    public bool IsImprisoned(IPerson person) =>
        GetMutable(person).PrisonSentence > 0;

    public void Imprison(
        IPerson person,
        int sentence,
        string reasonId,
        string reasonName,
        string? reasonDescription = null)
    {
        var justice = GetMutable(person);
        BeginImprisonment(
            person,
            justice,
            sentence,
            reasonId,
            reasonName,
            reasonDescription);
    }

    public CourtProtectionSnapshot GetCourtProtection(IPerson person)
    {
        if (_courtRules is null)
            return CourtProtectionSnapshot.None;

        var (relativeScore, bestHelper, bestCareer) = GetBestRelativeProtection(person);
        var acquaintanceScore = Math.Min(
            _courtRules.Protection.LawyerAcquaintances.CombinedScoreCap,
            GetCourtProtectionAcquaintances(person).Sum(item => item.Contribution));
        var totalScore = relativeScore + acquaintanceScore;
        var tier = _courtRules.ResolveProtection(totalScore);
        return ToSnapshot(
            tier,
            totalScore,
            bestHelper,
            bestCareer,
            relativeScore,
            acquaintanceScore);
    }

    private (double Score, IPerson? Helper, CareerSnapshot? Career) GetBestRelativeProtection(
        IPerson person)
    {
        var relations = _relationsResolver?.Invoke();
        if (relations is null
            || _gameState is null
            || _family is null
            || _career is null
            || _courtRules is null)
        {
            return (0d, null, null);
        }

        IPerson? bestHelper = null;
        CareerSnapshot? bestCareer = null;
        double bestScore = 0;

        foreach (var candidate in _gameState.People)
        {
            if (candidate.Id == person.Id
                || !candidate.Tags.Has("state.alive")
                || SimulationState.IsInactive(candidate)
                || !HouseholdKinshipRules.IsSupportedRelative(person, candidate, _family))
            {
                continue;
            }

            var relation = relations.GetRelation(person, candidate);
            if (relation is null
                || !relations.HasStrongCareerConnectionRelation(person, candidate))
            {
                continue;
            }

            var career = _career.GetCareer(candidate);
            if (!career.IsEmployed
                || career.JobLevel <= 0
                || string.IsNullOrWhiteSpace(career.CareerId)
                || !TryGetCareerWeight(career.CareerId, out var careerWeight))
            {
                continue;
            }

            var relationMultiplier = relation.Familiarity >= 75
                ? GetRelationMultiplier("Close", 1.0)
                : GetRelationMultiplier("Warm", 0.75);
            var score = career.JobLevel * careerWeight * relationMultiplier;
            if (score <= bestScore)
                continue;

            bestScore = score;
            bestHelper = candidate;
            bestCareer = career;
        }

        return (bestScore, bestHelper, bestCareer);
    }

    public IReadOnlyList<CourtProtectionRelativeInfo> GetCourtProtectionRelatives(
        IPerson person)
    {
        var relations = _relationsResolver?.Invoke();
        if (relations is null
            || _gameState is null
            || _family is null
            || _career is null
            || _courtRules is null)
        {
            return Array.Empty<CourtProtectionRelativeInfo>();
        }

        return _gameState.People
            .Where(candidate =>
                candidate.Id != person.Id
                && candidate.Tags.Has("state.alive")
                && !SimulationState.IsInactive(candidate)
                && HouseholdKinshipRules.IsSupportedRelative(person, candidate, _family))
            .Select(candidate =>
            {
                var relation = relations.GetRelation(person, candidate);
                var career = _career.GetCareer(candidate);
                if (relation is null
                    || !career.IsEmployed
                    || career.JobLevel <= 0
                    || string.IsNullOrWhiteSpace(career.CareerId)
                    || !TryGetCareerWeight(career.CareerId, out _))
                {
                    return null;
                }

                return new CourtProtectionRelativeInfo(
                    candidate.Id,
                    _family.GetDisplayName(candidate),
                    career.JobTitle,
                    career.CareerName ?? career.CareerId,
                    career.JobLevel,
                    relation.FamiliarityState,
                    relation.SympathyState,
                    relations.HasStrongCareerConnectionRelation(person, candidate));
            })
            .Where(item => item is not null)
            .Cast<CourtProtectionRelativeInfo>()
            .OrderByDescending(item => item.ProvidesProtection)
            .ThenByDescending(item => item.JobLevel)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<CourtProtectionAcquaintanceInfo> GetCourtProtectionAcquaintances(
        IPerson person)
    {
        var connections = _connectionsResolver?.Invoke();
        if (connections is null || _economy is null || _courtRules is null)
            return Array.Empty<CourtProtectionAcquaintanceInfo>();

        var householdId = _economy.GetHouseholdId(person);
        if (householdId is null)
            return Array.Empty<CourtProtectionAcquaintanceInfo>();

        var townId = _economy.GetResidenceTown(person).Id;
        var rules = _courtRules.Protection.LawyerAcquaintances;
        return connections
            .GetConnections(householdId.Value, activeOnly: true)
            .Where(connection =>
                connection.IsActive
                && connection.DeathYear is null
                && connection.TownId.Equals(townId, StringComparison.OrdinalIgnoreCase)
                && connection.ArchetypeId.Equals(rules.ArchetypeId, StringComparison.OrdinalIgnoreCase)
                && (connection.RelationState.Equals("Warm", StringComparison.OrdinalIgnoreCase)
                    || connection.RelationState.Equals("Close", StringComparison.OrdinalIgnoreCase)))
            .Select(connection => new CourtProtectionAcquaintanceInfo(
                connection.Id,
                connection.Name,
                string.IsNullOrWhiteSpace(connection.OccupationLabel)
                    ? "Lawyer"
                    : connection.OccupationLabel,
                connection.RelationState,
                connection.Renown,
                CalculateLawyerAcquaintanceContribution(
                    connection.RelationState,
                    connection.Renown,
                    rules)))
            .Where(item => item.Contribution > 0)
            .OrderByDescending(item => item.Contribution)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal static double CalculateLawyerAcquaintanceContribution(
        string relationState,
        double renown,
        LawyerAcquaintanceProtectionRules rules)
    {
        double baseScore;
        if (relationState.Equals("Close", StringComparison.OrdinalIgnoreCase))
            baseScore = rules.CloseBaseScore;
        else if (relationState.Equals("Warm", StringComparison.OrdinalIgnoreCase))
            baseScore = rules.WarmBaseScore;
        else
            return 0d;

        var multiplier = renown >= rules.NotableRenownThreshold
            ? rules.NotableRenownMultiplier
            : renown >= rules.ProminentRenownThreshold
                ? rules.ProminentRenownMultiplier
                : 1d;
        return baseScore * multiplier;
    }

    public int ConvictKnownOffense(
        IPerson person,
        int originalSentence,
        string reasonId,
        string reasonName,
        string? reasonDescription = null,
        decimal baseSentenceMultiplier = 1m)
    {
        originalSentence = Math.Max(1, originalSentence);
        var protection = GetCourtProtection(person);
        var combinedMultiplier = Math.Max(
            _courtRules?.Protection.CombinedSentenceMultiplierFloor ?? 0.5m,
            Math.Max(0m, baseSentenceMultiplier) * protection.SentenceMultiplier);
        var finalSentence = Math.Max(
            1,
            (int)Math.Ceiling(originalSentence * combinedMultiplier));

        var justice = GetMutable(person);
        BeginImprisonment(
            person,
            justice,
            finalSentence,
            reasonId,
            reasonName,
            reasonDescription);

        justice.CriminalRecord.Add(new CriminalRecordEntryState
        {
            Year = _gameState?.Year ?? 0,
            CrimeId = reasonId,
            CrimeName = reasonName,
            OriginalSentence = originalSentence,
            FinalSentence = finalSentence
        });
        person.Components.Set(justice);
        return finalSentence;
    }

    public decimal GetBailCost(IPerson person)
    {
        var justice = GetMutable(person);
        return justice.PrisonSentence <= 0
            ? 0m
            : _courtRules?.CalculateBailCost(justice.PrisonSentence)
                ?? Math.Max(20_000m, 20_000m + 7_500m * justice.PrisonSentence);
    }

    public bool ReleaseFromPrison(IPerson person)
    {
        var justice = GetMutable(person);
        if (justice.PrisonSentence <= 0)
            return false;

        ClearImprisonment(person, justice);
        return true;
    }

    public bool HasAttemptedEscapeThisImprisonment(IPerson person)
    {
        var justice = GetMutable(person);
        return justice.PrisonSentence > 0
            && justice.EscapeAttemptedCurrentImprisonment;
    }

    public void MarkEscapeAttempted(IPerson person)
    {
        var justice = GetMutable(person);
        if (justice.PrisonSentence <= 0)
            return;
        justice.EscapeAttemptedCurrentImprisonment = true;
        person.Components.Set(justice);
    }

    public int ExtendSentence(IPerson person, int years)
    {
        var justice = GetMutable(person);
        if (justice.PrisonSentence <= 0 || years <= 0)
            return justice.PrisonSentence;

        justice.PrisonSentence += years;
        person.Components.Set(justice);
        return justice.PrisonSentence;
    }

    public double GetStolenHeirloomSaleDetectionChance(IPerson person) =>
        GetCourtProtection(person).StolenSaleDetectionChance;

    internal bool AdvanceSentence(IPerson person)
    {
        var justice = GetMutable(person);
        if (justice.PrisonSentence <= 0)
            return false;

        justice.PrisonSentence--;
        if (justice.PrisonSentence > 0)
        {
            person.Components.Set(justice);
            return false;
        }

        ClearImprisonment(person, justice);
        return true;
    }

    internal void ReconcileAll(IEnumerable<IPerson> people)
    {
        foreach (var person in people)
            EnsureJustice(person);
    }

    private void BeginImprisonment(
        IPerson person,
        JusticeComponent justice,
        int sentence,
        string reasonId,
        string reasonName,
        string? reasonDescription)
    {
        justice.PrisonSentence = Math.Max(0, sentence);
        justice.CrimeId = reasonId;
        justice.CrimeName = reasonName;
        justice.CrimeDescription = reasonDescription;
        justice.EscapeAttemptedCurrentImprisonment = false;
        justice.CurrentImprisonmentId = justice.PrisonSentence > 0
            ? Guid.NewGuid()
            : null;

        if (justice.PrisonSentence > 0)
            person.Tags.Add("state.imprisoned");
        else
            person.Tags.Remove("state.imprisoned");

        person.Components.Set(justice);
    }

    private static void ClearImprisonment(
        IPerson person,
        JusticeComponent justice)
    {
        justice.PrisonSentence = 0;
        justice.CrimeId = null;
        justice.CrimeName = null;
        justice.CrimeDescription = null;
        justice.CurrentImprisonmentId = null;
        justice.EscapeAttemptedCurrentImprisonment = false;
        person.Tags.Remove("state.imprisoned");
        person.Components.Set(justice);
    }

    private bool TryGetCareerWeight(string careerId, out double weight)
    {
        if (_courtRules is null)
        {
            weight = 0;
            return false;
        }

        foreach (var entry in _courtRules.Protection.QualifyingCareerWeights)
        {
            if (entry.Key.Equals(careerId, StringComparison.OrdinalIgnoreCase))
            {
                weight = entry.Value;
                return true;
            }
        }

        weight = 0;
        return false;
    }

    private double GetRelationMultiplier(string relation, double fallback)
    {
        if (_courtRules is null)
            return fallback;

        foreach (var entry in _courtRules.Protection.RelationMultipliers)
        {
            if (entry.Key.Equals(relation, StringComparison.OrdinalIgnoreCase))
                return entry.Value;
        }
        return fallback;
    }

    private CourtProtectionSnapshot ToSnapshot(
        CourtProtectionTier tier,
        double score,
        IPerson? helper,
        CareerSnapshot? helperCareer,
        double relativeScore,
        double acquaintanceScore) =>
        new(
            tier.Id,
            tier.Display,
            score,
            tier.SentenceMultiplier,
            tier.StolenSaleDetectionChance,
            helper?.Id,
            helper is null ? null : _family?.GetDisplayName(helper),
            helperCareer?.CareerName ?? helperCareer?.JobTitle,
            helperCareer?.JobLevel ?? 0,
            relativeScore,
            acquaintanceScore);

    private static JusticeComponent GetRequired(IPerson person) =>
        person.Components.Get<JusticeComponent>()
        ?? throw new InvalidOperationException(
            "Justice state is missing. Run state reconciliation before reading it.");

    private JusticeComponent GetMutable(IPerson person)
    {
        EnsureJustice(person);
        return GetRequired(person);
    }
}
