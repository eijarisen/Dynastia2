using Dynastia.Contracts;

namespace Dynastia.Mechanics.TownLife;

internal sealed class StandardTownProsperityService : ITownProsperityService
{
    private readonly IGameState _gameState;
    private readonly TownProsperityRules _rules;
    private readonly Func<ICommunityPolicyService?> _communityResolver;

    public StandardTownProsperityService(
        IGameState gameState,
        TownProsperityRules rules,
        Func<ICommunityPolicyService?>? communityResolver = null)
    {
        _gameState = gameState;
        _rules = rules;
        _communityResolver = communityResolver ?? (() => null);
    }

    public TownProsperitySnapshot Get(TownInfo town)
    {
        ArgumentNullException.ThrowIfNull(town);
        var state = FindTownState(town.Id)
            ?? CreateTownState(
                town.Id,
                _gameState.Year,
                includeInitialHistory: false);
        var effective = ApplyCommunityModifiers(
            town,
            _gameState.Year,
            GetEffectiveIndex(state, _gameState.Year),
            state);

        return new TownProsperitySnapshot(
            effective,
            _rules.LabelFor(effective),
            state.LastTrend,
            state.Shocks
                .Where(shock => GetShockAmount(shock, _gameState.Year) != 0)
                .Select(shock => shock.SourceId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            state.History
                .OrderBy(point => point.Year)
                .Select(point => new TownProsperityHistoryPoint(point.Year, point.Index))
                .ToArray());
    }

    public TownProsperitySnapshot Get(TownInfo town, int year)
    {
        ArgumentNullException.ThrowIfNull(town);

        var state = FindTownState(town.Id)
            ?? CreateTownState(
                town.Id,
                year,
                includeInitialHistory: false);
        var history = state.History ?? new List<TownProsperityHistoryPointState>();
        var shocks = state.Shocks ?? new List<TownProsperityShockState>();
        var historical = history
            .FirstOrDefault(point => point.Year == year);
        var effective = ApplyCommunityModifiers(
            town,
            year,
            historical?.Index ?? GetEffectiveIndex(state, year),
            state);
        var previous = history
            .Where(point => point.Year < year)
            .OrderByDescending(point => point.Year)
            .FirstOrDefault();
        var trend = previous is null
            ? 0
            : effective - previous.Index;

        return new TownProsperitySnapshot(
            effective,
            _rules.LabelFor(effective),
            trend,
            shocks
                .Where(shock => GetShockAmount(shock, year) != 0)
                .Select(shock => shock.SourceId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            history
                .Where(point => point.Year <= year)
                .OrderBy(point => point.Year)
                .Select(point => new TownProsperityHistoryPoint(point.Year, point.Index))
                .ToArray());
    }

    public decimal GetIncomeMultiplier(
        TownInfo town,
        LocalEconomicStrength strength)
    {
        var prosperity = Get(town).Index;
        var deviation = (prosperity - _rules.BaseIndex) * _rules.DeviationScalePerPoint;
        var sensitivity = _rules.Sensitivities[strength];
        var directional = deviation < 0 ? sensitivity.Downturn : sensitivity.Boom;

        return Math.Clamp(
            1m + deviation * directional,
            _rules.HardMultiplierMin,
            _rules.HardMultiplierMax);
    }

    public void ApplyHistoricalShock(
        IEnumerable<string> placeIds,
        string sourceId,
        int delta,
        int recoveryYears)
    {
        SetHistoricalShock(
            placeIds,
            sourceId,
            delta,
            recoveryYears,
            _gameState.Year,
            replaceExisting: true);
    }

    internal void EnsureHistoricalShock(
        IEnumerable<string> placeIds,
        string sourceId,
        int delta,
        int recoveryYears,
        int appliedYear)
    {
        SetHistoricalShock(
            placeIds,
            sourceId,
            delta,
            recoveryYears,
            appliedYear,
            replaceExisting: false);
    }

    private void SetHistoricalShock(
        IEnumerable<string> placeIds,
        string sourceId,
        int delta,
        int recoveryYears,
        int appliedYear,
        bool replaceExisting)
    {
        ArgumentNullException.ThrowIfNull(placeIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        if (recoveryYears <= 0)
            throw new ArgumentOutOfRangeException(nameof(recoveryYears));

        var candidate = new TownProsperityShockState
        {
            SourceId = sourceId,
            Amount = delta,
            RecoveryYears = recoveryYears,
            AppliedYear = appliedYear
        };

        // A migrated/newly installed Batch 2 can encounter an event whose
        // recovery period is already over. Do not create inert tracking state.
        if (GetShockAmount(candidate, _gameState.Year) == 0)
            return;

        foreach (var placeId in placeIds
                     .Where(id => !string.IsNullOrWhiteSpace(id))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var state = GetOrCreateTownState(placeId);
            var existing = state.Shocks.FirstOrDefault(shock =>
                shock.SourceId.Equals(sourceId, StringComparison.OrdinalIgnoreCase));
            if (existing is not null
                && !replaceExisting
                && existing.Amount == delta
                && existing.RecoveryYears == recoveryYears
                && existing.AppliedYear == appliedYear)
            {
                continue;
            }

            var before = GetEffectiveIndex(state, _gameState.Year);
            if (existing is not null)
                state.Shocks.Remove(existing);

            state.Shocks.Add(new TownProsperityShockState
            {
                SourceId = sourceId,
                Amount = delta,
                RecoveryYears = recoveryYears,
                AppliedYear = appliedYear
            });
            var after = GetEffectiveIndex(state, _gameState.Year);
            state.LastTrend += after - before;
            EnsureHistoryPoint(state, _gameState.Year, after);
        }
    }

    internal void Track(TownInfo town)
    {
        ArgumentNullException.ThrowIfNull(town);
        var state = GetOrCreateTownState(town.Id);
        EnsureHistoryPoint(
            state,
            _gameState.Year,
            GetEffectiveIndex(state, _gameState.Year));
    }

    internal void TrackActiveHouseholdTowns(IGamePluginContext context)
    {
        var households = context.GetService<IHouseholdService>();
        var economy = context.GetService<IEconomyService>();
        if (households is null || economy is null)
            return;

        var people = _gameState.People.ToDictionary(person => person.Id);
        foreach (var household in households.GetActiveHouseholds())
        {
            if (!people.TryGetValue(household.HeadId, out var head))
                continue;

            Track(economy.GetResidenceTown(head));
        }
    }

    internal void AdvanceTrackedTowns()
    {
        var component = GetStateComponent(create: false);
        if (component is null)
            return;

        foreach (var town in component.Towns)
        {
            if (town.LastAdvancedYear >= _gameState.Year)
                continue;

            // Normal play advances one year at a time, but looping keeps old or
            // manually edited saves deterministic if a gap is encountered.
            for (var year = town.LastAdvancedYear + 1; year <= _gameState.Year; year++)
            {
                var before = GetEffectiveIndex(town, year - 1);
                var originalBase = town.BaseIndex;
                var drift = _rules.DrawOrdinaryDrift(
                    StableUnitRoll(town.PlaceId, year, "prosperity.drift"));
                var candidate = originalBase + drift;

                if (candidate != _rules.MeanReversionTarget
                    && _rules.MeanReversionMaxAdditionalStep > 0
                    && StableUnitRoll(
                        town.PlaceId,
                        year,
                        "prosperity.mean_reversion")
                        < _rules.MeanReversionProbability(candidate))
                {
                    candidate += Math.Sign(_rules.MeanReversionTarget - candidate)
                        * _rules.MeanReversionMaxAdditionalStep;
                }

                var ordinaryDelta = Math.Clamp(
                    candidate - originalBase,
                    -_rules.MaximumOrdinaryAbsoluteChangePerYear,
                    _rules.MaximumOrdinaryAbsoluteChangePerYear);
                town.BaseIndex = Math.Clamp(
                    originalBase + ordinaryDelta,
                    _rules.MinimumIndex,
                    _rules.MaximumIndex);
                town.LastAdvancedYear = year;
                town.Shocks.RemoveAll(shock => GetShockAmount(shock, year) == 0);
                var effective = GetEffectiveIndex(town, year);
                town.LastTrend = effective - before;
                EnsureHistoryPoint(town, year, effective);
            }
        }
    }

    internal IReadOnlyList<TownProsperityTownState> GetTrackedStates()
    {
        var state = GetStateComponent(create: false);
        return state is null
            ? Array.Empty<TownProsperityTownState>()
            : state.Towns;
    }

    private TownProsperityTownState? FindTownState(string placeId)
    {
        var component = GetStateComponent(create: false);
        return component?.Towns.FirstOrDefault(
            town => town.PlaceId.Equals(placeId, StringComparison.OrdinalIgnoreCase));
    }

    private TownProsperityTownState GetOrCreateTownState(string placeId)
    {
        var component = GetStateComponent(create: true);
        if (component is null)
        {
            // This can only occur before a game has any people. Town / City Affairs is
            // not gameplay-relevant at that point, so keep the result ephemeral.
            return CreateTownState(placeId, _gameState.Year);
        }

        var existing = component.Towns.FirstOrDefault(
            town => town.PlaceId.Equals(placeId, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing;

        var created = CreateTownState(placeId, _gameState.Year);
        component.Towns.Add(created);
        return created;
    }

    private TownProsperityTownState CreateTownState(
        string placeId,
        int firstRelevantYear,
        bool includeInitialHistory = true)
    {
        var minimum = _rules.InitialDeviationMin;
        var maximum = _rules.InitialDeviationMax;
        var width = maximum - minimum + 1;
        var deviation = minimum;
        if (width > 1)
        {
            // The first persisted person ID is a stable per-save world identity.
            // It distinguishes separate dynasties without consuming IGameRandom,
            // while firstRelevantYear keeps lazy initialization deterministic.
            var anchor = _gameState.People.FirstOrDefault();
            var worldIdentity = anchor is null
                ? $"{_gameState.DynastySurname}|{_gameState.StartYear}"
                : anchor.Id.ToString("N");
            var key = $"{worldIdentity}|{placeId}|{firstRelevantYear}";
            deviation = minimum + (int)(StableHash(key) % (uint)width);
        }

        var initialIndex = Math.Clamp(
            _rules.BaseIndex + deviation,
            _rules.MinimumIndex,
            _rules.MaximumIndex);

        return new TownProsperityTownState
        {
            PlaceId = placeId,
            BaseIndex = initialIndex,
            FirstRelevantYear = firstRelevantYear,
            LastAdvancedYear = firstRelevantYear,
            LastTrend = 0,
            History = includeInitialHistory
                ?
                [
                    new TownProsperityHistoryPointState
                    {
                        Year = firstRelevantYear,
                        Index = initialIndex
                    }
                ]
                : []
        };
    }

    private TownProsperityStateComponent? GetStateComponent(bool create)
    {
        var anchor = _gameState.People.FirstOrDefault();
        if (anchor is null)
            return null;

        var state = anchor.Components.Get<TownProsperityStateComponent>();
        if (state is null && create)
        {
            state = new TownProsperityStateComponent();
            anchor.Components.Set(state);
        }

        return state;
    }

    private static void EnsureHistoryPoint(
        TownProsperityTownState town,
        int year,
        int index)
    {
        town.History ??= [];

        var existing = town.History.FirstOrDefault(point => point.Year == year);
        if (existing is not null)
        {
            existing.Index = index;
            return;
        }

        town.History.Add(
            new TownProsperityHistoryPointState
            {
                Year = year,
                Index = index
            });
    }


    private int ApplyCommunityModifiers(
        TownInfo town,
        int year,
        int baseIndex,
        TownProsperityTownState state)
    {
        var modifiers = _communityResolver()?.GetModifiers(town, year);
        if (modifiers is null)
            return baseIndex;

        var recoveryBonus = state.Shocks.Any(shock => GetShockAmount(shock, year) < 0)
            ? modifiers.ProsperityRecoveryBonus
            : 0;
        return Math.Clamp(
            baseIndex + modifiers.ProsperityFlat + recoveryBonus,
            _rules.MinimumIndex,
            _rules.MaximumIndex);
    }

    private int GetEffectiveIndex(TownProsperityTownState town, int year)
    {
        var shocks = town.Shocks.Sum(shock => GetShockAmount(shock, year));
        return Math.Clamp(town.BaseIndex + shocks, _rules.MinimumIndex, _rules.MaximumIndex);
    }

    private static int GetShockAmount(TownProsperityShockState shock, int year)
    {
        var elapsed = Math.Max(0, year - shock.AppliedYear);
        if (elapsed >= shock.RecoveryYears)
            return 0;

        var fraction = (shock.RecoveryYears - elapsed) / (double)shock.RecoveryYears;
        return (int)Math.Round(shock.Amount * fraction, MidpointRounding.AwayFromZero);
    }

    private double StableUnitRoll(
        string placeId,
        int year,
        string salt)
    {
        var anchor = _gameState.People.FirstOrDefault();
        var worldIdentity = anchor is null
            ? $"{_gameState.DynastySurname}|{_gameState.StartYear}"
            : anchor.Id.ToString("N");
        var hash = StableHash(
            $"{worldIdentity}|{placeId}|{year}|{salt}");

        return hash / ((double)uint.MaxValue + 1.0);
    }

    private static uint StableHash(string value)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;
        foreach (var character in value)
        {
            hash ^= character;
            hash *= prime;
        }
        return hash;
    }
}
