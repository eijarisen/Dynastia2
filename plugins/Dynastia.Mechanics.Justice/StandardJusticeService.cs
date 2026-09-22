using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

public sealed class StandardJusticeService : IJusticeService
{
    private readonly IGameState? _gameState;
    private readonly IFamilyService? _family;
    private readonly ICareerService? _career;
    private readonly CourtJusticeRules? _courtRules;
    private readonly Func<IFamilyRelationService?>? _relationsResolver;

    public StandardJusticeService()
    {
    }

    public StandardJusticeService(
        IGameState gameState,
        IFamilyService family,
        ICareerService career,
        CourtJusticeRules courtRules,
        Func<IFamilyRelationService?> relationsResolver)
    {
        _gameState = gameState;
        _family = family;
        _career = career;
        _courtRules = courtRules;
        _relationsResolver = relationsResolver;
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
        var relations = _relationsResolver?.Invoke();
        if (relations is null
            || _gameState is null
            || _family is null
            || _career is null
            || _courtRules is null)
        {
            return CourtProtectionSnapshot.None;
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

        var tier = _courtRules.ResolveProtection(bestScore);
        return ToSnapshot(tier, bestScore, bestHelper, bestCareer);
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
        CareerSnapshot? helperCareer) =>
        new(
            tier.Id,
            tier.Display,
            score,
            tier.SentenceMultiplier,
            tier.StolenSaleDetectionChance,
            helper?.Id,
            helper is null ? null : _family?.GetDisplayName(helper),
            helperCareer?.CareerName ?? helperCareer?.JobTitle,
            helperCareer?.JobLevel ?? 0);

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
