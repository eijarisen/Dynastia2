using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.GameScore;

public sealed class StandardGameScoreService : IGameScoreService, IGameScorePreviewService
{
    private readonly IGameState _gameState;
    private readonly IGameEventBus _events;
    private readonly IFamilyService _family;
    private readonly IHouseholdService _households;
    private readonly IPersonLookup? _people;
    private readonly ICareerService? _career;
    private readonly ICraftService? _crafts;
    private readonly ICriminalOccupationService? _crime;
    private readonly ICivicOfficeService? _civicOffice;
    private readonly Dictionary<GameEvent, int> _eventIndices =
        new(ReferenceEqualityComparer.Instance);
    private int _indexedEventCount;
    private bool _rebuilding;

    public StandardGameScoreService(
        IGameState gameState,
        IGameEventBus events,
        IFamilyService family,
        IHouseholdService households,
        IPersonLookup? people,
        ICareerService? career,
        ICraftService? crafts,
        ICriminalOccupationService? crime,
        ICivicOfficeService? civicOffice)
    {
        _gameState = gameState;
        _events = events;
        _family = family;
        _households = households;
        _people = people;
        _career = career;
        _crafts = crafts;
        _crime = crime;
        _civicOffice = civicOffice;
        _events.EventPublished += OnEventPublished;
    }

    public long TotalScore => GetComponent(false)?.TotalScore ?? 0;

    public long GetYearDelta(int year) =>
        GetComponent(false)?.YearDeltas.TryGetValue(year, out var value) == true ? value : 0;

    public long GetScoreAfterYear(int year) =>
        GetComponent(false)?.YearDeltas
            .Where(pair => pair.Key <= year)
            .Sum(pair => pair.Value) ?? 0;

    public long GetEventDelta(GameEvent gameEvent)
    {
        var component = GetComponent(false);
        if (component is null)
            return 0;
        var index = IndexOfEvent(gameEvent);
        return index >= 0 && component.EventDeltas.TryGetValue(index, out var value) ? value : 0;
    }

    public IReadOnlyList<GameScoreEntryInfo> GetEntriesForYear(int year) =>
        GetComponent(false)?.Entries
            .Where(entry => entry.Year == year)
            .Select(entry => new GameScoreEntryInfo(entry.Year, entry.SourceEventIndex, entry.Delta, entry.Reason, entry.OutcomeKey))
            .ToList() ?? [];

    public long PreviewEventDelta(IReadOnlyList<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var current = GetComponent(false);
        if (current is null && !_gameState.People.Any(_family.IsBloodline))
            return 0;

        // Only decision state is copied. Historical entries and totals are unnecessary:
        // the detached ledger starts at zero and accumulates the hypothetical delta.
        var preview = new GameScoreComponent
        {
            ActiveClaims = current?.ActiveClaims.ToDictionary(
                pair => pair.Key,
                pair => new GameScoreClaimState
                {
                    Points = pair.Value.Points,
                    AwardedTierKeys = [.. pair.Value.AwardedTierKeys]
                },
                StringComparer.OrdinalIgnoreCase) ?? new(StringComparer.OrdinalIgnoreCase),
            LockedClaimKeys = current is null ? [] : [.. current.LockedClaimKeys],
            AwardedOutcomeKeys = current is null ? [] : [.. current.AwardedOutcomeKeys]
        };
        for (var index = 0; index < events.Count; index++)
            ProcessEvent(preview, events[index], _events.AllEvents.Count + index);
        return preview.TotalScore;
    }

    public void ReconcileAfterNewGame()
    {
        RebuildEventIndexCache();
        var component = GetComponent(true);
        if (component is null)
            return;
        if (_events.AllEvents.Count == 0)
            Reset(component);
        else if (component.ProcessedEventCount < _events.AllEvents.Count)
            ReplayFrom(component.ProcessedEventCount);
    }

    public void ReconcileAfterLoad()
    {
        RebuildEventIndexCache();
        var component = GetComponent(false);
        if (component is null || component.SchemaVersion != 1 || component.ProcessedEventCount > _events.AllEvents.Count)
        {
            Rebuild();
            return;
        }

        if (component.ProcessedEventCount < _events.AllEvents.Count)
            ReplayFrom(component.ProcessedEventCount);

        ReconcileClaims();
    }

    public void ReconcileClaims()
    {
        var component = GetComponent(false);
        if (component is null)
            return;

        foreach (var pair in component.ActiveClaims.ToList())
        {
            var key = pair.Key;
            if (!TryClaimPerson(key, out var personId))
                continue;
            var person = FindPerson(personId);
            if (person is null)
                continue;
            if (person.Tags.Has("state.dead") || person.DeathDate is not null)
            {
                LockClaim(component, key);
                continue;
            }

            var valid = true;
            if (key.StartsWith("career:", StringComparison.OrdinalIgnoreCase))
                valid = _career?.IsEmployed(person) ?? true;
            else if (key.StartsWith("craft-occupation:", StringComparison.OrdinalIgnoreCase))
                valid = _crafts?.IsSelfEmployed(person) ?? person.Tags.Has("career.craft_self_employed");
            else if (key.StartsWith("criminal-occupation:", StringComparison.OrdinalIgnoreCase))
                valid = _crime?.IsActive(person) ?? true;
            else if (key.StartsWith("religious-vocation:", StringComparison.OrdinalIgnoreCase))
                valid = person.Tags.Has("vocation.religious.active");
            else if (key.StartsWith("civic-office:", StringComparison.OrdinalIgnoreCase))
                valid = _civicOffice?.IsTownHead(person) ?? person.Tags.Has("civic.office.town_head");
            else
                continue;

            if (!valid)
                ReverseClaim(component, key, _gameState.Year, -1, "Achievement no longer active");
        }
    }

    private void OnEventPublished(object? sender, GameEvent gameEvent)
    {
        if (_rebuilding)
            return;
        var index = IndexOfEvent(gameEvent);
        var component = GetComponent(true);
        if (component is null || index < 0)
            return;

        // Resolve by reference rather than AllEvents.Count - 1. Earlier event
        // subscribers are allowed to publish nested events, so callbacks can
        // arrive here out of event-index order. Every live callback is still
        // processed exactly once by the event bus; the persisted count remains
        // a high-water mark for load/rebuild purposes.
        ProcessEvent(component, gameEvent, index);
        component.ProcessedEventCount = Math.Max(component.ProcessedEventCount, index + 1);
    }

    private void ReplayFrom(int startIndex)
    {
        var component = GetComponent(true);
        if (component is null)
            return;
        _rebuilding = true;
        try
        {
            for (var index = Math.Max(0, startIndex); index < _events.AllEvents.Count; index++)
            {
                ProcessEvent(component, _events.AllEvents[index], index);
                component.ProcessedEventCount = index + 1;
            }
        }
        finally { _rebuilding = false; }
    }

    private void Rebuild()
    {
        var component = GetComponent(true);
        if (component is null)
            return;
        Reset(component);
        ReplayFrom(0);
        ReconcileClaims();
    }

    private static void Reset(GameScoreComponent c)
    {
        c.SchemaVersion = 1;
        c.TotalScore = 0;
        c.YearDeltas.Clear();
        c.EventDeltas.Clear();
        c.Entries.Clear();
        c.ActiveClaims.Clear();
        c.LockedClaimKeys.Clear();
        c.AwardedOutcomeKeys.Clear();
        c.ProcessedEventCount = 0;
    }

    private void ProcessEvent(GameScoreComponent c, GameEvent e, int index)
    {
        if (!_households.ShouldShowFamilyNews(e))
            return;

        var type = e.Type;
        if (type.Equals("life.birth", StringComparison.OrdinalIgnoreCase))
            AwardPerson(c, e, index, "birth", 100, "Child born");
        else if (type.Equals("peripheral.birth", StringComparison.OrdinalIgnoreCase))
            AwardOnce(c, $"birth-event:{index}", 100, e.Year, index, "Child born");
        else if (type is "reproduction.unknown_father_birth" or "reproduction.teen_birth")
            AwardChildFromData(c, e, index);
        else if (type.Equals("reproduction.birth_and_marriage", StringComparison.OrdinalIgnoreCase))
        {
            AwardChildFromData(c, e, index);
            AddUnionClaim(c, e, index);
        }
        else if (type.Equals("reproduction.teen_marriage", StringComparison.OrdinalIgnoreCase)
                 || type.Equals("relationship.married", StringComparison.OrdinalIgnoreCase)
                 || type.Equals("relationship.remarried", StringComparison.OrdinalIgnoreCase)
                 || type.Equals("relationship.partnered", StringComparison.OrdinalIgnoreCase))
            AddUnionClaim(c, e, index);
        else if (type.Equals("relationship.divorce", StringComparison.OrdinalIgnoreCase)
                 || type.Equals("relationship.low_satisfaction_divorce", StringComparison.OrdinalIgnoreCase)
                 || type.Equals("relationship.prison_divorce", StringComparison.OrdinalIgnoreCase))
            ReverseUnion(c, e, index);
        else if (type.Equals("life.adult", StringComparison.OrdinalIgnoreCase))
            AwardPerson(c, e, index, "adult", 50, "Reached adulthood");
        else if (type.Equals("life.death", StringComparison.OrdinalIgnoreCase))
            ScoreDeath(c, e, index);
        else if (type is "education.success" or "education.help_learning_success" or "education.private_tutor_success" or "rare.scholarship")
            ScoreEducation(c, e, index);
        else if (type.Equals("stats.paid_improvement", StringComparison.OrdinalIgnoreCase))
            ScoreStat(c, e, index);
        else if (type.Equals("craft.learned", StringComparison.OrdinalIgnoreCase))
            ScoreCraftLearned(c, e, index);
        else if (type is "craft.mastery_increased" or "craft.became_master")
            ScoreCraftMastery(c, e, index);
        else if (type.Equals("craft.self_employment_started", StringComparison.OrdinalIgnoreCase))
            AddPersonClaim(c, "craft-occupation", e.SubjectId, 10, e.Year, index, "Craft self-employment started");
        else if (type.Equals("craft.self_employment_ended", StringComparison.OrdinalIgnoreCase))
            ReversePersonClaim(c, "craft-occupation", e.SubjectId, e.Year, index, "Craft self-employment ended");
        else if (type.Equals("career.employment", StringComparison.OrdinalIgnoreCase))
            AddPersonClaim(c, "career", e.SubjectId, 10, e.Year, index, "Employment started");
        else if (type.Equals("career.promotion", StringComparison.OrdinalIgnoreCase))
            ScorePromotion(c, e, index);
        else if (type.Equals("career.fired", StringComparison.OrdinalIgnoreCase) || type.Equals("career.quit", StringComparison.OrdinalIgnoreCase))
            ReversePersonClaim(c, "career", e.SubjectId, e.Year, index, "Employment ended");
        else if (type.Equals("career.ask_quit_success", StringComparison.OrdinalIgnoreCase))
            ReversePersonClaim(c, "career", e.RelatedPersonIds.FirstOrDefault(), e.Year, index, "Employment ended");
        else if (type.Equals("career.retirement", StringComparison.OrdinalIgnoreCase))
            LockPersonClaim(c, "career", e.SubjectId);
        else if (type.Equals("justice.life_of_crime_started", StringComparison.OrdinalIgnoreCase))
            AddPersonClaim(c, "criminal-occupation", e.SubjectId, 10, e.Year, index, "Life of Crime started");
        else if (type.Equals("justice.life_of_crime_ended", StringComparison.OrdinalIgnoreCase))
            ReversePersonClaim(c, "criminal-occupation", e.SubjectId, e.Year, index, "Life of Crime ended");
        else if (type is "religion.calling_priest" or "religion.calling_nun")
            AddPersonClaim(c, "religious-vocation", e.SubjectId, 25, e.Year, index, "Religious vocation entered");
        else if (type.Equals("religion.vocation_left", StringComparison.OrdinalIgnoreCase))
            ReversePersonClaim(c, "religious-vocation", e.SubjectId, e.Year, index, "Religious vocation left");
        else if (type.Equals("community.civic_office_appointed", StringComparison.OrdinalIgnoreCase))
            ScoreCivicOffice(c, e, index, true);
        else if (type.Equals("community.civic_office_lost", StringComparison.OrdinalIgnoreCase))
            ScoreCivicOffice(c, e, index, false);
        else if (type.Equals("household.house_bought", StringComparison.OrdinalIgnoreCase))
            AddAssetClaim(c, "house", e, "propertyId", 50, index, "House bought");
        else if (type.Equals("household.house_extended", StringComparison.OrdinalIgnoreCase))
            GrowAssetClaim(c, "house", e, "propertyId", $"extension:{index}", 25, index, "House extended");
        else if (type.Equals("household.house_sold", StringComparison.OrdinalIgnoreCase))
            ReverseAssetClaim(c, "house", e, "propertyId", index, "House sold");
        else if (type.Equals("farmland.bought", StringComparison.OrdinalIgnoreCase))
            AddAssetClaim(c, "farmland", e, "farmlandId", 35, index, "Farmland bought");
        else if (type.Equals("farming.livestock_added", StringComparison.OrdinalIgnoreCase))
            GrowFarmlandLivestock(c, e, index);
        else if (type.Equals("farmland.sold", StringComparison.OrdinalIgnoreCase))
            ReverseAssetClaim(c, "farmland", e, "farmlandId", index, "Farmland sold");
        else if (type.Equals("farming.relocation_sale", StringComparison.OrdinalIgnoreCase))
            ReverseRelocationFarmland(c, e, index);
        else if (type is "heirloom.created" or "artistic.work_created" or "heirloom.inherited")
            ScoreHeirloom(c, e, index);
    }

    private static Guid? GuidData(GameEvent e, string key)
        => e.Data.TryGetValue(key, out var raw) && Guid.TryParse(raw, out var id) ? id : null;

    private static int IntData(GameEvent e, string key, int fallback = 0)
        => e.Data.TryGetValue(key, out var raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;

    private void AwardChildFromData(GameScoreComponent c, GameEvent e, int index)
    {
        var id = GuidData(e, "childId") ?? e.SubjectId ?? e.RelatedPersonIds.FirstOrDefault();
        if (id is Guid childId && childId != Guid.Empty)
            AwardOnce(c, $"birth:{childId:D}", 100, e.Year, index, "Child born");
    }

    private void AwardPerson(GameScoreComponent c, GameEvent e, int index, string prefix, long points, string reason)
    {
        if (e.SubjectId is Guid id)
            AwardOnce(c, $"{prefix}:{id:D}", points, e.Year, index, reason);
    }

    private void ScoreDeath(GameScoreComponent c, GameEvent e, int index)
    {
        if (e.SubjectId is not Guid id)
            return;
        var age = IntData(e, "age", -1);
        if (age < 0)
            age = FindPerson(id)?.Age ?? 0;
        AwardOnce(c, $"death:{id:D}", Math.Max(0, age), e.Year, index, "Life completed");
        foreach (var key in c.ActiveClaims.Keys.Where(k => IsPersonalClaimFor(k, id)).ToList())
            LockClaim(c, key);
        foreach (var key in c.ActiveClaims.Keys.Where(k => k.StartsWith("union:", StringComparison.OrdinalIgnoreCase) && k.Contains(id.ToString("D"), StringComparison.OrdinalIgnoreCase)).ToList())
            LockClaim(c, key);
    }

    private void ScoreEducation(GameScoreComponent c, GameEvent e, int index)
    {
        if (e.SubjectId is not Guid id) return;
        var level = IntData(e, e.Type.Equals("rare.scholarship", StringComparison.OrdinalIgnoreCase) ? "newEducation" : "level");
        if (level > 0) AwardOnce(c, $"education:{id:D}:{level}", 15, e.Year, index, "Education gained");
    }

    private void ScoreStat(GameScoreComponent c, GameEvent e, int index)
    {
        if (e.SubjectId is not Guid id || !e.Data.TryGetValue("statId", out var statId)) return;
        var before = IntData(e, "previousValue");
        var after = IntData(e, "newValue");
        for (var value = before + 1; value <= after; value++)
            AwardOnce(c, $"stat:{id:D}:{statId}:{value}", 25, e.Year, index, "Stat improved");
    }

    private void ScoreCraftLearned(GameScoreComponent c, GameEvent e, int index)
    {
        if (e.SubjectId is Guid id && e.Data.TryGetValue("craftId", out var craftId) && !string.IsNullOrWhiteSpace(craftId))
            AwardOnce(c, $"craft-learned:{id:D}:{craftId}", 15, e.Year, index, "Craft learned");
    }

    private void ScoreCraftMastery(GameScoreComponent c, GameEvent e, int index)
    {
        if (e.SubjectId is not Guid id || !e.Data.TryGetValue("craftId", out var craftId)) return;
        var previous = IntData(e, "previousLevel", 1);
        var level = IntData(e, "level");
        for (var tier = Math.Max(2, previous + 1); tier <= Math.Min(5, level); tier++)
        {
            var points = tier switch { 2 => 10, 3 => 15, 4 => 25, 5 => 50, _ => 0 };
            AwardOnce(c, $"craft-mastery:{id:D}:{craftId}:{tier}", points, e.Year, index, "Craft mastery gained");
        }
    }

    private void ScorePromotion(GameScoreComponent c, GameEvent e, int index)
    {
        if (e.SubjectId is not Guid id) return;
        var level = IntData(e, "jobLevel", IntData(e, "level"));
        var points = level switch { 2 => 10, 3 => 15, 4 => 25, 5 => 50, _ => 0 };
        if (points <= 0) return;
        GrowClaim(c, $"career:{id:D}", $"career-tier:{id:D}:{level}", points, e.Year, index, "Career promotion");
    }

    private void AddUnionClaim(GameScoreComponent c, GameEvent e, int index)
    {
        if (!TryUnionIds(e, out var a, out var b)) return;
        AddClaim(c, UnionKey(a, b), 25, e.Year, index, "Marriage or partnership");
    }

    private void ReverseUnion(GameScoreComponent c, GameEvent e, int index)
    {
        if (TryUnionIds(e, out var a, out var b))
            ReverseClaim(c, UnionKey(a, b), e.Year, index, "Divorce");
    }

    private static bool TryUnionIds(GameEvent e, out Guid a, out Guid b)
    {
        a = e.SubjectId ?? Guid.Empty;
        b = GuidData(e, "spouseId")
            ?? GuidData(e, "husbandId")
            ?? GuidData(e, "partnerId")
            ?? e.RelatedPersonIds.FirstOrDefault();
        return a != Guid.Empty && b != Guid.Empty && a != b;
    }

    private static string UnionKey(Guid a, Guid b)
    {
        var left = string.CompareOrdinal(a.ToString("D"), b.ToString("D")) <= 0 ? a : b;
        var right = left == a ? b : a;
        return $"union:{left:D}:{right:D}";
    }

    private void ScoreCivicOffice(GameScoreComponent c, GameEvent e, int index, bool add)
    {
        if (e.SubjectId is not Guid id || !e.Data.TryGetValue("townId", out var townId) || string.IsNullOrWhiteSpace(townId)) return;
        var key = $"civic-office:{id:D}:{townId}";
        if (add) AddClaim(c, key, 50, e.Year, index, "Civic office appointed");
        else ReverseClaim(c, key, e.Year, index, "Civic office lost");
    }

    private void AddAssetClaim(GameScoreComponent c, string prefix, GameEvent e, string dataKey, long points, int index, string reason)
    {
        if (e.Data.TryGetValue(dataKey, out var id) && !string.IsNullOrWhiteSpace(id)) AddClaim(c, $"{prefix}:{id}", points, e.Year, index, reason);
    }
    private void GrowAssetClaim(GameScoreComponent c, string prefix, GameEvent e, string dataKey, string tier, long points, int index, string reason)
    {
        if (e.Data.TryGetValue(dataKey, out var id) && !string.IsNullOrWhiteSpace(id)) GrowClaim(c, $"{prefix}:{id}", tier, points, e.Year, index, reason);
    }
    private void ReverseAssetClaim(GameScoreComponent c, string prefix, GameEvent e, string dataKey, int index, string reason)
    {
        if (e.Data.TryGetValue(dataKey, out var id) && !string.IsNullOrWhiteSpace(id)) ReverseClaim(c, $"{prefix}:{id}", e.Year, index, reason);
    }

    private void GrowFarmlandLivestock(GameScoreComponent c, GameEvent e, int index)
    {
        if (!e.Data.TryGetValue("farmlandId", out var id) || string.IsNullOrWhiteSpace(id)) return;
        GrowClaim(c, $"farmland:{id}", $"farmland-livestock:{id}", 20, e.Year, index, "Livestock added");
    }

    private void ReverseRelocationFarmland(GameScoreComponent c, GameEvent e, int index)
    {
        if (!e.Data.TryGetValue("farmlandIds", out var raw) || string.IsNullOrWhiteSpace(raw)) return;
        foreach (var id in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            ReverseClaim(c, $"farmland:{id}", e.Year, index, "Farmland sold during relocation");
    }

    private void ScoreHeirloom(GameScoreComponent c, GameEvent e, int index)
    {
        var id = e.Data.TryGetValue("heirloomId", out var value) ? value : e.Data.TryGetValue("assetId", out value) ? value : string.Empty;
        if (!string.IsNullOrWhiteSpace(id)) AwardOnce(c, $"heirloom:{id}", 25, e.Year, index, "Heirloom acquired");
    }

    private static void AddPersonClaim(GameScoreComponent c, string prefix, Guid? id, long points, int year, int index, string reason)
    { if (id is Guid personId) AddClaim(c, $"{prefix}:{personId:D}", points, year, index, reason); }
    private static void ReversePersonClaim(GameScoreComponent c, string prefix, Guid? id, int year, int index, string reason)
    { if (id is Guid personId) ReverseClaim(c, $"{prefix}:{personId:D}", year, index, reason); }
    private static void LockPersonClaim(GameScoreComponent c, string prefix, Guid? id)
    { if (id is Guid personId) LockClaim(c, $"{prefix}:{personId:D}"); }

    private static void AwardOnce(GameScoreComponent c, string key, long points, int year, int index, string reason)
    {
        if (points <= 0 || c.AwardedOutcomeKeys.Contains(key, StringComparer.OrdinalIgnoreCase)) return;
        c.AwardedOutcomeKeys.Add(key);
        AddDelta(c, points, year, index, reason, key);
    }

    private static void AddClaim(GameScoreComponent c, string key, long points, int year, int index, string reason)
    {
        if (points <= 0 || c.ActiveClaims.ContainsKey(key) || c.LockedClaimKeys.Contains(key, StringComparer.OrdinalIgnoreCase)) return;
        c.ActiveClaims[key] = new GameScoreClaimState { Points = points };
        AddDelta(c, points, year, index, reason, key);
    }

    private static void GrowClaim(GameScoreComponent c, string key, string tierKey, long points, int year, int index, string reason)
    {
        if (!c.ActiveClaims.TryGetValue(key, out var claim) || points <= 0 || claim.AwardedTierKeys.Contains(tierKey, StringComparer.OrdinalIgnoreCase)) return;
        claim.AwardedTierKeys.Add(tierKey);
        claim.Points += points;
        AddDelta(c, points, year, index, reason, tierKey);
    }

    private static void ReverseClaim(GameScoreComponent c, string key, int year, int index, string reason)
    {
        if (!c.ActiveClaims.Remove(key, out var claim) || claim.Points <= 0) return;
        AddDelta(c, -claim.Points, year, index, reason, key);
    }

    private static void LockClaim(GameScoreComponent c, string key)
    {
        if (!c.ActiveClaims.Remove(key)) return;
        if (!c.LockedClaimKeys.Contains(key, StringComparer.OrdinalIgnoreCase)) c.LockedClaimKeys.Add(key);
    }

    private static void AddDelta(GameScoreComponent c, long delta, int year, int index, string reason, string key)
    {
        if (delta == 0) return;
        c.TotalScore += delta;
        c.YearDeltas[year] = c.YearDeltas.GetValueOrDefault(year) + delta;
        if (index >= 0) c.EventDeltas[index] = c.EventDeltas.GetValueOrDefault(index) + delta;
        c.Entries.Add(new GameScoreEntryState { Year = year, SourceEventIndex = index, Delta = delta, Reason = reason, OutcomeKey = key });
    }

    private GameScoreComponent? GetComponent(bool create)
    {
        var anchor = _gameState.People
            .Where(_family.IsBloodline)
            .OrderBy(person => _family.GetGeneration(person) ?? int.MaxValue)
            .ThenBy(person => person.BirthDate?.Year ?? int.MaxValue)
            .ThenBy(person => person.Id)
            .FirstOrDefault();
        if (anchor is null) return null;
        var component = anchor.Components.Get<GameScoreComponent>();
        if (component is null && create)
        {
            component = new GameScoreComponent();
            anchor.Components.Set(component);
        }
        return component;
    }

    private IPerson? FindPerson(Guid id) => _people?.FindPerson(id) ?? _gameState.People.FirstOrDefault(person => person.Id == id);

    private int IndexOfEvent(GameEvent e)
    {
        EnsureEventIndexCache();
        return _eventIndices.TryGetValue(e, out var index) ? index : -1;
    }

    private void EnsureEventIndexCache()
    {
        if (_indexedEventCount > _events.AllEvents.Count)
            RebuildEventIndexCache();

        for (var index = _indexedEventCount; index < _events.AllEvents.Count; index++)
            _eventIndices[_events.AllEvents[index]] = index;

        _indexedEventCount = _events.AllEvents.Count;
    }

    private void RebuildEventIndexCache()
    {
        _eventIndices.Clear();
        _indexedEventCount = 0;
        EnsureEventIndexCache();
    }

    private static bool TryClaimPerson(string key, out Guid id)
    {
        id = Guid.Empty;
        var parts = key.Split(':');
        if (parts.Length < 2) return false;
        return Guid.TryParse(parts[1], out id);
    }

    private static bool IsPersonalClaimFor(string key, Guid id)
    {
        return (key.StartsWith("career:", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("craft-occupation:", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("criminal-occupation:", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("religious-vocation:", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("civic-office:", StringComparison.OrdinalIgnoreCase))
               && TryClaimPerson(key, out var personId) && personId == id;
    }
}
