using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Heirlooms;

public sealed class StandardHeirloomService : IHeirloomService
{
    private readonly IGameState _gameState;
    private readonly IEconomyService _economy;
    private readonly IFamilyService _family;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;
    private readonly HeirloomCatalog _catalog;

    public StandardHeirloomService(
        IGameState gameState,
        IEconomyService economy,
        IFamilyService family,
        IGameRandom random,
        IGameEventBus events,
        HeirloomCatalog catalog)
    {
        _gameState = gameState;
        _economy = economy;
        _family = family;
        _random = random;
        _events = events;
        _catalog = catalog;
    }

    public IReadOnlyList<HeirloomAssetInfo> GetHeirlooms(IPerson householdMember)
    {
        var householdId = _economy.GetHouseholdId(householdMember);
        if (householdId is null)
            return [];

        return FindHouseholdState(householdId.Value)?.Assets
            .Select(ToInfo)
            .ToList()
            ?? [];
    }

    public HeirloomAssetInfo Create(
        IPerson householdMember,
        HeirloomCreationRequest request)
    {
        var householdId = RequireHouseholdId(householdMember);
        var component = GetOrCreateHouseholdState(householdMember, householdId);
        var template = _catalog.GetTemplate(request.TemplateId);

        if (_gameState.Year < template.StartYear
            || template.EndYear is int endYear && _gameState.Year > endYear)
        {
            throw new InvalidOperationException(
                $"Heirloom template '{template.TemplateId}' is not available in {_gameState.Year}.");
        }

        var origin = request.OriginPersonId is Guid originId
            ? _gameState.People.FirstOrDefault(person => person.Id == originId)
            : null;
        var personName = origin is null
            ? null
            : _family.GetDisplayName(origin);

        var displayName = template.NamePattern.Replace(
            "{Person}",
            personName ?? "Family",
            StringComparison.Ordinal);

        decimal value;
        if (request.AppraisedValueOverride is decimal overrideValue)
        {
            if (overrideValue <= 0m)
                throw new InvalidOperationException("Heirloom appraised-value overrides must be positive.");
            value = overrideValue;
        }
        else
        {
            var min = (double)_catalog.Rules.Creation.ValueVarianceMultiplierMin;
            var max = (double)_catalog.Rules.Creation.ValueVarianceMultiplierMax;
            var multiplier = min + (_random.NextDouble() * (max - min));
            value = RoundCurrency(template.BaseValue * (decimal)multiplier);
        }

        var state = new HeirloomAssetState
        {
            Id = _random.NextGuid(),
            TemplateId = template.TemplateId,
            Emoji = template.Emoji,
            DisplayName = displayName,
            AppraisedValue = value,
            AcquiredYear = _gameState.Year,
            OriginPersonId = request.OriginPersonId,
            OriginTriggerType = request.OriginTriggerType,
            OriginTriggerId = request.OriginTriggerId,
            OriginDescription = request.OriginDescription,
            IsStolen = request.IsStolen,
            RoyaltyAuthorId = request.RoyaltyAuthorId,
            RoyaltyAnnualRate = request.RoyaltyAnnualRate
        };

        state.OwnershipHistory.Add(
            new HeirloomOwnershipRecordState
            {
                Year = _gameState.Year,
                HouseholdId = householdId,
                PersonId = request.OriginPersonId,
                Reason = "created"
            });

        component.Assets.Add(state);
        var info = ToInfo(state);

        _events.Publish(
            new GameEvent
            {
                Type = "heirloom.created",
                Year = _gameState.Year,
                SubjectId = request.OriginPersonId ?? householdMember.Id,
                RelatedPersonIds = request.OriginPersonId is Guid id ? [id] : [],
                Data = new Dictionary<string, string>
                {
                    ["heirloomId"] = info.Id.ToString(),
                    ["templateId"] = info.TemplateId,
                    ["item"] = info.DisplayName,
                    ["value"] = info.AppraisedValue.ToString(CultureInfo.InvariantCulture),
                    ["familyNews"] = request.OriginTriggerType.Equals(
                        "artistic_work",
                        StringComparison.OrdinalIgnoreCase)
                            ? "false"
                            : "true",
                    ["suppressChronicle"] = request.OriginTriggerType.Equals(
                        "artistic_work",
                        StringComparison.OrdinalIgnoreCase)
                            ? "true"
                            : "false",
                    ["text"] = _catalog.BuildCreationNews(
                        template,
                        info.DisplayName,
                        personName,
                        request.NewsTokens)
                }
            });

        return info;
    }

    public HeirloomAssetInfo? Take(IPerson householdMember, Guid heirloomId)
    {
        var householdId = _economy.GetHouseholdId(householdMember);
        if (householdId is null)
            return null;

        var component = FindHouseholdState(householdId.Value);
        if (component is null)
            return null;

        var index = component.Assets.FindIndex(asset => asset.Id == heirloomId);
        if (index < 0)
            return null;

        var state = component.Assets[index];
        component.Assets.RemoveAt(index);
        return ToInfo(state);
    }

    public IReadOnlyList<HeirloomAssetInfo> TakeAll(IPerson householdMember)
    {
        var householdId = _economy.GetHouseholdId(householdMember);
        if (householdId is null)
            return [];

        var component = FindHouseholdState(householdId.Value);
        if (component is null || component.Assets.Count == 0)
            return [];

        var result = component.Assets.Select(ToInfo).ToList();
        component.Assets.Clear();
        return result;
    }

    public void AddExisting(
        IPerson householdMember,
        HeirloomAssetInfo heirloom,
        int year,
        Guid? personId,
        string reason)
    {
        var householdId = RequireHouseholdId(householdMember);
        var component = GetOrCreateHouseholdState(householdMember, householdId);
        if (component.Assets.Any(asset => asset.Id == heirloom.Id))
            return;

        var state = ToState(heirloom);
        state.AssignedHeirId = null;
        state.OwnershipHistory.Add(
            new HeirloomOwnershipRecordState
            {
                Year = year,
                HouseholdId = householdId,
                PersonId = personId,
                Reason = reason
            });
        component.Assets.Add(state);
    }

    public IReadOnlyList<HeirloomAssetInfo> GetPending(IPerson person) =>
        person.Components.Get<PendingHeirloomComponent>()?.Assets
            .Select(ToInfo)
            .ToList()
        ?? [];

    public void AddPending(
        IPerson person,
        HeirloomAssetInfo heirloom,
        int year,
        string reason)
    {
        var component = person.Components.Get<PendingHeirloomComponent>();
        if (component is null)
        {
            component = new PendingHeirloomComponent();
            person.Components.Set(component);
        }

        if (component.Assets.Any(asset => asset.Id == heirloom.Id))
            return;

        var state = ToState(heirloom);
        state.AssignedHeirId = null;
        state.OwnershipHistory.Add(
            new HeirloomOwnershipRecordState
            {
                Year = year,
                HouseholdId = Guid.Empty,
                PersonId = person.Id,
                Reason = reason
            });
        component.Assets.Add(state);
    }

    public IReadOnlyList<HeirloomAssetInfo> TakePending(IPerson person)
    {
        var component = person.Components.Get<PendingHeirloomComponent>();
        if (component is null || component.Assets.Count == 0)
            return [];

        var result = component.Assets.Select(ToInfo).ToList();
        component.Assets.Clear();
        return result;
    }

    public bool SetInheritanceHeir(
        IPerson householdMember,
        Guid heirloomId,
        Guid? heirId)
    {
        var householdId = _economy.GetHouseholdId(householdMember);
        if (householdId is null)
            return false;

        var asset = FindHouseholdState(householdId.Value)?.Assets
            .FirstOrDefault(candidate => candidate.Id == heirloomId);
        if (asset is null)
            return false;

        if (heirId is Guid id)
        {
            var validChild = _family.GetChildren(householdMember)
                .Any(child =>
                    child.Id == id
                    && child.Tags.Has("state.alive"));

            if (!validChild)
                return false;
        }

        asset.AssignedHeirId = heirId;
        return true;
    }

    public decimal GetSaleValue(HeirloomAssetInfo heirloom) =>
        RoundCurrency(heirloom.AppraisedValue * _catalog.Rules.Sale.SaleValueMultiplier);

    public void ClearInheritanceAssignments(Guid heirId)
    {
        foreach (var person in _gameState.People)
        {
            var household = person.Components.Get<HeirloomHouseholdComponent>();
            if (household is not null)
            {
                foreach (var asset in household.Assets)
                {
                    if (asset.AssignedHeirId == heirId)
                        asset.AssignedHeirId = null;
                }
            }

            var pending = person.Components.Get<PendingHeirloomComponent>();
            if (pending is null)
                continue;

            foreach (var asset in pending.Assets)
            {
                if (asset.AssignedHeirId == heirId)
                    asset.AssignedHeirId = null;
            }
        }
    }

    private Guid RequireHouseholdId(IPerson person) =>
        _economy.GetHouseholdId(person)
        ?? throw new InvalidOperationException(
            $"{_family.GetDisplayName(person)} does not belong to a household.");

    private HeirloomHouseholdComponent GetOrCreateHouseholdState(
        IPerson householdMember,
        Guid householdId)
    {
        var existing = FindHouseholdState(householdId);
        if (existing is not null)
            return existing;

        var storage = _gameState.People.FirstOrDefault(person =>
                _economy.GetHouseholdId(person) == householdId
                && _economy.HasHousehold(person))
            ?? householdMember;

        var component = new HeirloomHouseholdComponent
        {
            HouseholdId = householdId
        };
        storage.Components.Set(component);
        return component;
    }

    private HeirloomHouseholdComponent? FindHouseholdState(Guid householdId) =>
        _gameState.People
            .Select(person => person.Components.Get<HeirloomHouseholdComponent>())
            .FirstOrDefault(component => component?.HouseholdId == householdId);

    private static decimal RoundCurrency(decimal amount) =>
        Math.Round(amount, 0, MidpointRounding.AwayFromZero);

    private static HeirloomAssetInfo ToInfo(HeirloomAssetState state) =>
        new(
            state.Id,
            state.TemplateId,
            state.Emoji,
            state.DisplayName,
            state.AppraisedValue,
            state.AcquiredYear,
            state.OriginPersonId,
            state.OriginTriggerType,
            state.OriginTriggerId,
            state.OriginDescription,
            state.AssignedHeirId,
            state.IsStolen,
            state.OwnershipHistory
                .Select(record => new HeirloomOwnershipRecordInfo(
                    record.Year,
                    record.HouseholdId,
                    record.PersonId,
                    record.Reason))
                .ToList(),
            state.RoyaltyAuthorId,
            state.RoyaltyAnnualRate);

    private static HeirloomAssetState ToState(HeirloomAssetInfo info)
    {
        var state = new HeirloomAssetState
        {
            Id = info.Id,
            TemplateId = info.TemplateId,
            Emoji = info.Emoji,
            DisplayName = info.DisplayName,
            AppraisedValue = info.AppraisedValue,
            AcquiredYear = info.AcquiredYear,
            OriginPersonId = info.OriginPersonId,
            OriginTriggerType = info.OriginTriggerType,
            OriginTriggerId = info.OriginTriggerId,
            OriginDescription = info.OriginDescription,
            AssignedHeirId = info.AssignedHeirId,
            IsStolen = info.IsStolen,
            RoyaltyAuthorId = info.RoyaltyAuthorId,
            RoyaltyAnnualRate = info.RoyaltyAnnualRate
        };

        foreach (var record in info.OwnershipHistory)
        {
            state.OwnershipHistory.Add(
                new HeirloomOwnershipRecordState
                {
                    Year = record.Year,
                    HouseholdId = record.HouseholdId,
                    PersonId = record.PersonId,
                    Reason = record.Reason
                });
        }

        return state;
    }
}
