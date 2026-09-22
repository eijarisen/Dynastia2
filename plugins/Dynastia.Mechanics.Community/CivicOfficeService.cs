using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Community;

internal sealed class CivicOfficeService : ICivicOfficeService
{
    private const string TownHeadTag = "civic.office.town_head";
    private const string PublicAdministrationCareerId = "public_administration";
    private const string PolishNationalityId = "polish";

    private readonly IGameState _gameState;
    private readonly ILocationService _locations;
    private readonly IEconomyService _economy;
    private readonly IFamilyService _family;
    private readonly INationalityService _nationalities;
    private readonly IHistoricalNameService _names;
    private readonly IStatusService _status;
    private readonly IEducationService _education;
    private readonly IStatsService _stats;
    private readonly ICareerService _career;
    private readonly ICraftService _crafts;
    private readonly ITownProsperityService _prosperity;
    private readonly ILocalEconomicStrengthService _economicStrength;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;
    private readonly CivicOfficeCatalog _catalog;
    private readonly CivicOfficeRules _rules;
    private readonly Func<ICriminalOccupationService?> _criminalResolver;

    public CivicOfficeService(
        IGameState gameState,
        ILocationService locations,
        IEconomyService economy,
        IFamilyService family,
        INationalityService nationalities,
        IHistoricalNameService names,
        IStatusService status,
        IEducationService education,
        IStatsService stats,
        ICareerService career,
        ICraftService crafts,
        ITownProsperityService prosperity,
        ILocalEconomicStrengthService economicStrength,
        IGameRandom random,
        IGameEventBus events,
        CivicOfficeCatalog catalog,
        CivicOfficeRules rules,
        Func<ICriminalOccupationService?>? criminalResolver = null)
    {
        _gameState = gameState;
        _locations = locations;
        _economy = economy;
        _family = family;
        _nationalities = nationalities;
        _names = names;
        _status = status;
        _education = education;
        _stats = stats;
        _career = career;
        _crafts = crafts;
        _prosperity = prosperity;
        _economicStrength = economicStrength;
        _random = random;
        _events = events;
        _catalog = catalog;
        _rules = rules;
        _criminalResolver = criminalResolver ?? (() => null);
    }

    public CivicOfficeProfileInfo? GetProfile(TownInfo town, int year) =>
        _catalog.Resolve(town.PolityId, year);

    public CivicOfficeHeadInfo? GetTownHead(TownInfo town, int year)
    {
        ArgumentNullException.ThrowIfNull(town);
        var profile = GetProfile(town, year);
        if (profile is null)
            return null;

        var state = GetOrCreateOfficeState(town, year, profile);
        return BuildInfo(state, town, year, profile);
    }

    public CivicOfficeHeadInfo? GetOffice(IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);
        var state = GetWorldState(create: false)?.CivicOffices
            .FirstOrDefault(office => office.HeadPersonId == person.Id);
        if (state is null)
            return null;

        var town = _locations.FindTownAtYear(state.TownId, _gameState.Year)
            ?? _locations.FindTown(state.TownId);
        if (town is null)
            return null;

        var profile = GetProfile(town, _gameState.Year);
        return profile is null
            ? null
            : BuildInfo(state, town, _gameState.Year, profile);
    }

    public bool IsTownHead(IPerson person) =>
        GetWorldState(create: false)?.CivicOffices.Any(
            office => office.HeadPersonId == person.Id) == true;

    public string? GetOfficeTitle(IPerson person)
    {
        var state = FindOfficeByPerson(person.Id);
        if (state is null)
            return null;
        var town = _locations.FindTownAtYear(state.TownId, _gameState.Year)
            ?? _locations.FindTown(state.TownId);
        var profile = town is null ? null : GetProfile(town, _gameState.Year);
        return town is null || profile is null
            ? null
            : profile.ResolveHeadTitle(town.SettlementClass);
    }

    public bool IsEligible(IPerson person, TownInfo town, int year)
    {
        if (!person.Tags.Has("state.alive")
            || person.Tags.Has("state.imprisoned")
            || person.Tags.Has("vocation.religious.active")
            || SimulationState.IsInactive(person))
        {
            return false;
        }

        var profile = GetProfile(town, year);
        var heldOffice = FindOfficeByPerson(person.Id);
        if (heldOffice is not null
            && !heldOffice.TownId.Equals(town.Id, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (profile is null
            || !IsPolish(person)
            || person.Age < profile.MinimumAge
            || !profile.AllowsSex(_family.GetSex(person))
            || _education.GetEducationLevel(person) < profile.MinimumEducation)
        {
            return false;
        }

        var residence = _economy.GetResidenceTown(person);
        if (!residence.Id.Equals(town.Id, StringComparison.OrdinalIgnoreCase))
            return false;

        var social = _status.GetStatus(person);
        if (social.LocalRenown < profile.MinimumRenown
            || social.Reputation < profile.MinimumReputation)
        {
            return false;
        }

        return GetParticipationCount(person) >= profile.MinimumParticipation;
    }

    public decimal GetAnnualSalary(IPerson person)
    {
        var state = FindOfficeByPerson(person.Id);
        if (state is null)
            return 0m;
        var town = _locations.FindTownAtYear(state.TownId, _gameState.Year)
            ?? _locations.FindTown(state.TownId);
        return town is null ? 0m : CalculateSalary(town);
    }

    internal void MarkOfficeAction(IPerson actor) =>
        MarkOfficeAction(actor.Id);

    internal void MarkOfficeAction(Guid actorId)
    {
        var state = GetWorldState(create: false)?.CivicOffices
            .FirstOrDefault(office => office.HeadPersonId == actorId);
        if (state is not null)
            state.LastOfficeActionYear = _gameState.Year;
    }

    internal void RecordPolicyEnacted(Guid actorId, string townId)
    {
        var state = GetWorldState(create: false)?.CivicOffices
            .FirstOrDefault(office => office.HeadPersonId == actorId
                && office.TownId.Equals(townId, StringComparison.OrdinalIgnoreCase));
        if (state is not null)
            state.PendingApprovalAdjustment += _rules.EnactedLobbyBonus;
    }

    internal void PerformDuties(IPerson actor)
    {
        MarkOfficeAction(actor);
        var state = FindOfficeByPerson(actor.Id);
        if (state is not null)
            state.PendingApprovalAdjustment += _rules.DutiesBonus;

        var participation = actor.Components.Get<CommunityParticipationComponent>()
            ?? new CommunityParticipationComponent();
        participation.Count += 1;
        actor.Components.Set(participation);

        _status.ApplyPersistentDelta(
            actor,
            _rules.DutiesRenown,
            _rules.DutiesReputation,
            "community.perform_office_duties");
    }

    internal void ReconcileTags()
    {
        var world = GetWorldState(create: false);
        if (world is not null)
        {
            foreach (var office in world.CivicOffices)
            {
                var town = _locations.FindTownAtYear(office.TownId, _gameState.Year);
                if (town is not null && GetProfile(town, _gameState.Year) is not null)
                    NormalizeOfficeNationality(office, town, _gameState.Year);
            }
        }

        var simulatedHeads = world?.CivicOffices
            .Where(office => office.HeadPersonId.HasValue)
            .Select(office => office.HeadPersonId!.Value)
            .ToHashSet() ?? [];

        foreach (var person in _gameState.People)
        {
            if (simulatedHeads.Contains(person.Id))
                person.Tags.Add(TownHeadTag);
            else
                person.Tags.Remove(TownHeadTag);
        }
    }

    internal void ProcessRelevantTowns()
    {
        var relevantTownIds = GetWorldState(create: false)?.CivicOffices
            .Select(office => office.TownId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var person in _gameState.People)
        {
            if (!person.Tags.Has("state.alive") || SimulationState.IsInactive(person))
                continue;
            if (!_economy.HasHousehold(person))
                continue;
            relevantTownIds.Add(_economy.GetResidenceTown(person).Id);
        }

        foreach (var townId in relevantTownIds)
        {
            var town = _locations.FindTownAtYear(townId, _gameState.Year);
            if (town is not null)
                ProcessTown(town);
        }

        ReconcileTags();
    }

    private void ProcessTown(TownInfo town)
    {
        var profile = GetProfile(town, _gameState.Year);
        if (profile is null)
            return;

        var state = GetOrCreateOfficeState(town, _gameState.Year, profile);
        if (state.LastProcessedYear >= _gameState.Year)
            return;

        var outgoing = state.HeadPersonId is Guid id
            ? _gameState.People.FirstOrDefault(person => person.Id == id)
            : null;

        if (outgoing is not null
            && (!outgoing.Tags.Has("state.alive")
                || outgoing.Tags.Has("state.imprisoned")
                || !_economy.GetResidenceTown(outgoing).Id.Equals(town.Id, StringComparison.OrdinalIgnoreCase)))
        {
            LoseSimulatedOffice(outgoing, state, town, "lost civic office");
            SelectReplacement(state, town, outgoing.Id);
            state.LastProcessedYear = _gameState.Year;
            return;
        }

        UpdateApproval(state, town);

        var age = outgoing?.Age ?? Math.Max(0, _gameState.Year - state.NpcBirthYear);
        var replacementChance = _rules.ReplacementChance(state.Approval, age);
        if (_random.Chance(replacementChance))
        {
            var excluded = outgoing?.Id;
            if (outgoing is not null)
                LoseSimulatedOffice(outgoing, state, town, "was replaced as town head");
            SelectReplacement(state, town, excluded);
        }

        state.LastProcessedYear = _gameState.Year;
    }

    private void UpdateApproval(CivicOfficeTownState state, TownInfo town)
    {
        double target;
        if (state.HeadPersonId is Guid personId
            && _gameState.People.FirstOrDefault(person => person.Id == personId) is { } person)
        {
            var social = _status.GetStatus(person);
            target = ApprovalTarget(social.LocalRenown, social.Reputation);
        }
        else
        {
            target = ApprovalTarget(state.NpcRenown, state.NpcReputation);
        }

        state.Approval += (target - state.Approval) * _rules.ApprovalMoveFraction;
        if (state.LastOfficeActionYear != _gameState.Year)
            state.Approval += _rules.NeglectPenalty;

        var currentProsperity = _prosperity.Get(town, _gameState.Year).Index;
        var previousProsperity = _prosperity.Get(town, _gameState.Year - 1).Index;
        if (currentProsperity > previousProsperity)
            state.Approval += _rules.ProsperityImprovedBonus;
        else if (currentProsperity < previousProsperity)
            state.Approval += _rules.ProsperityDeclinedPenalty;

        state.Approval += state.PendingApprovalAdjustment;
        state.PendingApprovalAdjustment = 0;
        state.Approval = Math.Clamp(state.Approval, 0, 100);
    }

    private void SelectReplacement(
        CivicOfficeTownState state,
        TownInfo town,
        Guid? excludedPersonId)
    {
        var eligible = _gameState.People
            .Where(person => person.Id != excludedPersonId && IsEligible(person, town, _gameState.Year))
            .Select(person => new PlayerCandidate(person, PlayerCandidateWeight(person)))
            .ToArray();

        var npcWeights = Enumerable.Range(0, _rules.NpcCandidateCount)
            .Select(slot => new NpcCandidate(
                slot,
                _rules.NpcCandidateWeightMin
                + _random.NextDouble() * (_rules.NpcCandidateWeightMax - _rules.NpcCandidateWeightMin)))
            .ToArray();

        var total = eligible.Sum(candidate => candidate.Weight)
            + npcWeights.Sum(candidate => candidate.Weight);
        var target = _random.NextDouble() * Math.Max(1, total);
        var cumulative = 0.0;

        foreach (var candidate in eligible)
        {
            cumulative += candidate.Weight;
            if (target < cumulative)
            {
                AppointSimulated(candidate.Person, state, town);
                return;
            }
        }

        var chosenNpc = npcWeights.Length == 0 ? 0 : npcWeights[^1].Slot;
        foreach (var candidate in npcWeights)
        {
            cumulative += candidate.Weight;
            if (target < cumulative)
            {
                chosenNpc = candidate.Slot;
                break;
            }
        }

        AppointNpc(state, town, _gameState.Year, $"replacement:{chosenNpc}");
    }

    private double PlayerCandidateWeight(IPerson person)
    {
        var social = _status.GetStatus(person);
        var education = _education.GetEducationLevel(person);
        var appeal = _stats.GetStats(person)
            .FirstOrDefault(stat => stat.Id.Equals("appeal", StringComparison.OrdinalIgnoreCase))?.Value ?? 3;
        var participation = Math.Min(GetParticipationCount(person), 10);
        return Math.Max(
            1,
            1.5 * social.LocalRenown
            + 0.5 * Math.Max(social.Reputation, 0)
            + 4 * education
            + 3 * appeal
            + 2 * participation);
    }

    private void AppointSimulated(IPerson person, CivicOfficeTownState state, TownInfo town)
    {
        var before = _career.GetCareer(person);
        _career.AssignCareer(person, null, 0, before.JobSatisfaction);
        _crafts.EndOccupation(person, "civic office");
        _criminalResolver()?.EndLifeOfCrime(person, "civic office");
        person.Tags.Add(TownHeadTag);

        state.HeadPersonId = person.Id;
        state.NpcName = string.Empty;
        state.NpcNationalityId = string.Empty;
        state.NpcBirthYear = 0;
        state.NpcRenown = 0;
        state.NpcReputation = 0;
        state.OfficeStartYear = _gameState.Year;
        state.LastOfficeActionYear = int.MinValue;
        state.PendingApprovalAdjustment = 0;

        var social = _status.GetStatus(person);
        state.Approval = ApprovalTarget(social.LocalRenown, social.Reputation);

        var profile = GetProfile(town, _gameState.Year)!;
        _events.Publish(new GameEvent
        {
            Type = "community.civic_office_appointed",
            Year = _gameState.Year,
            SubjectId = person.Id,
            Data = new Dictionary<string, string>
            {
                ["townId"] = town.Id,
                ["town"] = town.Town,
                ["officeTitle"] = profile.ResolveHeadTitle(town.SettlementClass),
                ["text"] = $"{_family.GetDisplayName(person)} was appointed {profile.ResolveHeadTitle(town.SettlementClass)} of {town.Town}."
            }
        });
    }

    private void LoseSimulatedOffice(
        IPerson person,
        CivicOfficeTownState state,
        TownInfo town,
        string reason)
    {
        person.Tags.Remove(TownHeadTag);
        state.HeadPersonId = null;
        _events.Publish(new GameEvent
        {
            Type = "community.civic_office_lost",
            Year = _gameState.Year,
            SubjectId = person.Id,
            Data = new Dictionary<string, string>
            {
                ["townId"] = town.Id,
                ["town"] = town.Town,
                ["text"] = $"{_family.GetDisplayName(person)} {reason} in {town.Town} and is now unoccupied."
            }
        });
    }

    private CivicOfficeTownState GetOrCreateOfficeState(
        TownInfo town,
        int year,
        CivicOfficeProfileInfo profile)
    {
        var world = GetWorldState(create: true)
            ?? throw new InvalidOperationException("Civic office state requires at least one game person.");
        var existing = world.CivicOffices.FirstOrDefault(
            office => office.TownId.Equals(town.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            NormalizeOfficeNationality(existing, town, year);
            return existing;
        }

        var created = new CivicOfficeTownState { TownId = town.Id };
        world.CivicOffices.Add(created);
        AppointNpc(created, town, year, "initial");
        return created;
    }

    private void AppointNpc(CivicOfficeTownState state, TownInfo town, int year, string salt)
    {
        var profile = GetProfile(town, year)
            ?? throw new InvalidOperationException($"No civic office profile for {town.PolityId} in {year}.");
        var random = new StableGameRandom(StableHash64(BuildKey(town.Id, year, salt)));
        var sex = profile.AllowedSex.Equals("Male", StringComparison.OrdinalIgnoreCase)
            ? Sex.Male
            : profile.AllowedSex.Equals("Female", StringComparison.OrdinalIgnoreCase)
                ? Sex.Female
                : random.Chance(0.5) ? Sex.Male : Sex.Female;
        var age = random.NextInt(_rules.NpcAgeMin, _rules.NpcAgeMax);
        var birthYear = year - age;
        var nationalityId = PolishNationalityId;
        var cultureId = _nationalities.GetNameCultureId(nationalityId);
        var firstName = _names.GetRandomFirstName(sex, birthYear, cultureId, random);
        var surname = _names.GetRandomSurname(sex, cultureId, random);
        var renown = random.NextInt(_rules.NpcRenownMin, _rules.NpcRenownMax);
        var reputation = random.NextInt(_rules.NpcReputationMin, _rules.NpcReputationMax);

        state.HeadPersonId = null;
        state.NpcName = $"{firstName} {surname}";
        state.NpcSex = sex;
        state.NpcBirthYear = birthYear;
        state.NpcNationalityId = nationalityId;
        state.NpcRenown = renown;
        state.NpcReputation = reputation;
        state.Approval = ApprovalTarget(renown, reputation);
        state.OfficeStartYear = year;
        state.LastOfficeActionYear = int.MinValue;
        state.PendingApprovalAdjustment = 0;
    }

    private void NormalizeOfficeNationality(
        CivicOfficeTownState state,
        TownInfo town,
        int year)
    {
        if (state.HeadPersonId is Guid personId)
        {
            var person = _gameState.People.FirstOrDefault(candidate => candidate.Id == personId);
            if (person is not null && IsPolish(person))
                return;

            if (person is not null)
                person.Tags.Remove(TownHeadTag);
        }
        else if (string.Equals(
            state.NpcNationalityId,
            PolishNationalityId,
            StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        AppointNpc(state, town, year, "polish-office-normalization");
    }

    private bool IsPolish(IPerson person) =>
        _nationalities.GetNationality(person).Equals(
            PolishNationalityId,
            StringComparison.OrdinalIgnoreCase);

    private CivicOfficeHeadInfo BuildInfo(
        CivicOfficeTownState state,
        TownInfo town,
        int year,
        CivicOfficeProfileInfo profile)
    {
        if (state.HeadPersonId is Guid personId
            && _gameState.People.FirstOrDefault(person => person.Id == personId) is { } person)
        {
            var social = _status.GetStatus(person);
            return new CivicOfficeHeadInfo(
                town.Id,
                _family.GetDisplayName(person),
                _family.GetSex(person),
                person.BirthDate?.Year ?? year - person.Age,
                _nationalities.GetNationality(person),
                profile.ResolveHeadTitle(town.SettlementClass),
                social.LocalRenown,
                social.Reputation,
                state.Approval,
                state.OfficeStartYear,
                true,
                person.Id,
                CalculateSalary(town));
        }

        return new CivicOfficeHeadInfo(
            town.Id,
            state.NpcName,
            state.NpcSex,
            state.NpcBirthYear,
            state.NpcNationalityId,
            profile.ResolveHeadTitle(town.SettlementClass),
            state.NpcRenown,
            state.NpcReputation,
            state.Approval,
            state.OfficeStartYear,
            false,
            null,
            0m);
    }

    private decimal CalculateSalary(TownInfo town)
    {
        var levelFiveReference = _career.GetLevelOneSalary(PublicAdministrationCareerId) * 10m;
        var settlement = _rules.SettlementSalaryMultiplier(town.SettlementClass);
        var strength = _economicStrength.ResolveCareer(town, "Generic", ["public_service"]);
        var prosperity = _prosperity.GetIncomeMultiplier(town, strength);
        return Math.Round(levelFiveReference * settlement * prosperity, 0, MidpointRounding.AwayFromZero);
    }

    private CivicOfficeTownState? FindOfficeByPerson(Guid personId) =>
        GetWorldState(create: false)?.CivicOffices
            .FirstOrDefault(office => office.HeadPersonId == personId);

    private static int GetParticipationCount(IPerson person) =>
        person.Components.Get<CommunityParticipationComponent>()?.Count ?? 0;

    private static double ApprovalTarget(double localRenown, double reputation) =>
        Math.Clamp(30 + 0.45 * localRenown + 0.20 * reputation, 0, 100);

    private CommunityWorldStateComponent? GetWorldState(bool create)
    {
        var anchor = _gameState.People.FirstOrDefault();
        if (anchor is null)
            return null;
        var state = anchor.Components.Get<CommunityWorldStateComponent>();
        if (state is null && create)
        {
            state = new CommunityWorldStateComponent();
            anchor.Components.Set(state);
        }
        if (state is not null)
            state.CivicOffices ??= [];
        return state;
    }

    private string BuildKey(string townId, int year, string salt)
    {
        var anchor = _gameState.People.FirstOrDefault()?.Id.ToString("N") ?? "no-anchor";
        return $"{anchor}|{_gameState.StartYear}|{townId}|{year}|civic|{salt}";
    }

    private static ulong StableHash64(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }

    private sealed record PlayerCandidate(IPerson Person, double Weight);
    private sealed record NpcCandidate(int Slot, double Weight);

    private sealed class StableGameRandom : IGameRandom
    {
        private ulong _state;
        public StableGameRandom(ulong seed) =>
            _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;

        public int NextInt(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxInclusive));
            var width = (ulong)((long)maxInclusive - minInclusive + 1L);
            return minInclusive + (int)(NextUInt64() % width);
        }

        public double NextDouble() =>
            (NextUInt64() >> 11) * (1.0 / (1UL << 53));

        public bool Chance(double probability) =>
            NextDouble() < Math.Clamp(probability, 0, 1);

        private ulong NextUInt64()
        {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            return _state * 2685821657736338717UL;
        }
    }
}
