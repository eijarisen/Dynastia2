using Dynastia.Contracts;

namespace Dynastia.Mechanics.Status;

internal sealed class StandardStatusService : IStatusService
{
    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly IEducationService _education;
    private readonly ICareerService _career;
    private readonly ICraftService _crafts;
    private readonly IFarmingService _farming;
    private readonly ILocationService _locations;
    private readonly ILoanService _loans;
    private readonly IHeirloomService _heirlooms;
    private readonly StatusRules _rules;

    public StandardStatusService(
        IGameState gameState,
        IFamilyService family,
        IEconomyService economy,
        IEducationService education,
        ICareerService career,
        ICraftService crafts,
        IFarmingService farming,
        ILocationService locations,
        ILoanService loans,
        IHeirloomService heirlooms,
        StatusRules rules)
    {
        _gameState = gameState;
        _family = family;
        _economy = economy;
        _education = education;
        _career = career;
        _crafts = crafts;
        _farming = farming;
        _locations = locations;
        _loans = loans;
        _heirlooms = heirlooms;
        _rules = rules;
    }

    public StatusSnapshot GetStatus(IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);
        if (person.Age < 18)
        {
            var household = GetHouseholdStatus(person);
            var renown = Math.Clamp(household.Renown, _rules.RenownMinimum, _rules.RenownMaximum);
            var reputation = Math.Clamp(household.Reputation, _rules.ReputationMinimum, _rules.ReputationMaximum);
            return CreateSnapshot(renown, renown, reputation);
        }

        return CalculateAdultStatus(person, applyLocalRecognition: true);
    }

    public HouseholdSocialStatusSnapshot GetHouseholdStatus(IPerson householdMember)
    {
        ArgumentNullException.ThrowIfNull(householdMember);
        return CalculateHouseholdStatus(householdMember, excludedPersonId: null);
    }

    public StatusSnapshot GetCandidateStatus(StatusCandidateProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var delta = CalculateProfileDelta(
            profile.NetWorth,
            profile.EducationLevel,
            profile.CareerLevel,
            profile.CraftMasteryLevel,
            profile.IsActiveFarmWorker,
            profile.IsCivicHead);
        var renown = Math.Clamp(
            _rules.BaseRenown + delta.Renown + profile.PersistentRenownDelta,
            _rules.RenownMinimum,
            _rules.RenownMaximum);
        var reputation = Math.Clamp(
            _rules.BaseReputation + delta.Reputation + profile.PersistentReputationDelta,
            _rules.ReputationMinimum,
            _rules.ReputationMaximum);
        return CreateSnapshot(renown, renown, reputation);
    }

    public double GetCareerApplicationBonus(IPerson person)
    {
        var status = GetStatus(person);
        return Math.Clamp(
            status.LocalRenown * _rules.ApplicationRenownFactor
            + status.Reputation * _rules.ApplicationReputationFactor,
            _rules.ApplicationMinimum,
            _rules.ApplicationMaximum);
    }

    public double GetCareerPromotionBonus(IPerson person)
    {
        var status = GetStatus(person);
        return Math.Clamp(
            status.LocalRenown * _rules.PromotionRenownFactor
            + status.Reputation * _rules.PromotionReputationFactor,
            _rules.PromotionMinimum,
            _rules.PromotionMaximum);
    }

    public void ApplyPersistentDelta(
        IPerson person,
        double renownDelta,
        double reputationDelta,
        string reason = "event")
    {
        ArgumentNullException.ThrowIfNull(person);
        var component = EnsureComponent(person);
        component.PersistentRenownDelta += renownDelta;
        component.PersistentReputationDelta += reputationDelta;
    }

    public void SeedAdultInheritance(IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);
        var component = EnsureComponent(person);
        if (component.AdultInheritanceSeeded)
            return;

        var household = CalculateHouseholdStatus(person, person.Id);
        if (household.AdultCount > 0)
        {
            component.InheritedRenown = Math.Min(
                _rules.InheritedRenownCap,
                Math.Max(0, household.Renown * _rules.InheritedRenownFraction));
            component.InheritedReputation = Math.Clamp(
                household.Reputation * _rules.InheritedReputationFraction,
                _rules.InheritedReputationMinimum,
                _rules.InheritedReputationMaximum);
        }

        component.AdultInheritanceSeeded = true;
    }

    public void ReconcileAll()
    {
        foreach (var person in _gameState.People)
        {
            var component = EnsureComponent(person);
            if (person.Age > 18 && !component.AdultInheritanceSeeded)
                component.AdultInheritanceSeeded = true;
            else if (person.Age == 18 && !component.AdultInheritanceSeeded)
                SeedAdultInheritance(person);

            SynchronizeLocality(person, component, initializeAsEstablished: true);
        }
    }

    private StatusSnapshot CalculateAdultStatus(IPerson person, bool applyLocalRecognition)
    {
        var component = EnsureComponent(person);
        var profile = CalculateProfileDelta(
            CalculateNetWorth(person),
            _education.GetEducationLevel(person),
            person.Tags.Has("civic.office.town_head")
                ? 0
                : _career.GetCareer(person).JobLevel,
            GetCraftMastery(person),
            _farming.IsWorkingFarmWorker(person, person),
            person.Tags.Has("civic.office.town_head"));

        var renown = Math.Clamp(
            _rules.BaseRenown
            + profile.Renown
            + component.InheritedRenown
            + component.PersistentRenownDelta,
            _rules.RenownMinimum,
            _rules.RenownMaximum);
        var reputation = Math.Clamp(
            _rules.BaseReputation
            + profile.Reputation
            + component.InheritedReputation
            + component.PersistentReputationDelta,
            _rules.ReputationMinimum,
            _rules.ReputationMaximum);

        var localRenown = renown;
        if (applyLocalRecognition)
        {
            SynchronizeLocality(person, component, initializeAsEstablished: false);
            var years = Math.Clamp(
                _gameState.Year - component.LocalTownSinceYear,
                0,
                _rules.YearsToFullRecognition);
            var progress = _rules.YearsToFullRecognition <= 0
                ? 1d
                : years / (double)_rules.YearsToFullRecognition;
            var multiplier = _rules.MoveMultiplier + (1d - _rules.MoveMultiplier) * progress;
            localRenown = Math.Clamp(
                renown * multiplier,
                _rules.RenownMinimum,
                _rules.RenownMaximum);
        }

        return CreateSnapshot(renown, localRenown, reputation);
    }

    private HouseholdSocialStatusSnapshot CalculateHouseholdStatus(
        IPerson householdMember,
        Guid? excludedPersonId)
    {
        var memberIds = _economy.GetHouseholdMemberIds(householdMember);
        var adults = memberIds
            .Select(id => _gameState.People.FirstOrDefault(person => person.Id == id))
            .Where(person => person is not null
                && person.Id != excludedPersonId
                && person.Age >= 18
                && person.Tags.Has("state.alive"))
            .Cast<IPerson>()
            .DistinctBy(person => person.Id)
            .ToList();

        if (adults.Count == 0
            && householdMember.Age >= 18
            && householdMember.Id != excludedPersonId
            && householdMember.Tags.Has("state.alive"))
        {
            adults.Add(householdMember);
        }

        if (adults.Count == 0)
            return new HouseholdSocialStatusSnapshot(_rules.BaseRenown, _rules.BaseReputation, 0);

        var statuses = adults
            .Select(person => CalculateAdultStatus(person, applyLocalRecognition: false))
            .ToList();
        return new HouseholdSocialStatusSnapshot(
            statuses.Average(status => status.Renown),
            statuses.Average(status => status.Reputation),
            statuses.Count);
    }

    private decimal CalculateNetWorth(IPerson person)
    {
        var household = _economy.GetHousehold(person);
        var wealth = household?.Wealth ?? 0m;
        var houseValue = _economy.GetHouses(person).Sum(_economy.GetHouseValue);
        var farmlandValue = _economy.GetFarmland(person).Sum(asset =>
            _farming.PurchasePrice
            + (string.IsNullOrWhiteSpace(asset.LivestockTypeId)
                ? 0m
                : _farming.LivestockPurchasePrice));
        var heirloomValue = _heirlooms.GetHeirlooms(person).Sum(item => item.AppraisedValue);
        var debt = _loans.GetDebts(person).Sum(item => item.RemainingAmount);
        return wealth + houseValue + farmlandValue + heirloomValue - debt;
    }

    private int GetCraftMastery(IPerson person)
    {
        var best = 0;
        foreach (var craft in _crafts.GetKnownCrafts(person))
        {
            var progress = _crafts.GetProgress(person, craft.Id);
            if (progress is not null)
                best = Math.Max(best, progress.MasteryLevel);
        }
        return best;
    }

    private StatusRules.StatusDelta CalculateProfileDelta(
        decimal netWorth,
        int educationLevel,
        int careerLevel,
        int craftMasteryLevel,
        bool activeFarmWorker,
        bool civicHead)
    {
        var wealth = _rules.WealthDelta(netWorth);
        var education = _rules.Education.GetValueOrDefault(Math.Clamp(educationLevel, 0, 5), new(0, 0));
        var career = _rules.Career.GetValueOrDefault(Math.Clamp(careerLevel, 0, 5), new(0, 0));
        var craft = _rules.CraftMastery.GetValueOrDefault(Math.Clamp(craftMasteryLevel, 0, 5), new(0, 0));
        var renown = wealth.Renown + education.Renown + career.Renown + craft.Renown;
        var reputation = wealth.Reputation + education.Reputation + career.Reputation + craft.Reputation;
        if (activeFarmWorker)
        {
            renown += _rules.ActiveFarmWorker.Renown;
            reputation += _rules.ActiveFarmWorker.Reputation;
        }
        if (civicHead)
        {
            renown += _rules.CivicHead.Renown;
            reputation += _rules.CivicHead.Reputation;
        }
        return new StatusRules.StatusDelta(renown, reputation);
    }

    private StatusSnapshot CreateSnapshot(double renown, double localRenown, double reputation) =>
        new(
            Math.Round(renown, 1),
            Math.Round(localRenown, 1),
            Math.Round(reputation, 1),
            _rules.RenownLabel(renown),
            _rules.ReputationLabel(reputation));

    private StatusComponent EnsureComponent(IPerson person)
    {
        var component = person.Components.Get<StatusComponent>();
        if (component is not null)
            return component;

        component = new StatusComponent();
        person.Components.Set(component);
        return component;
    }

    private void SynchronizeLocality(
        IPerson person,
        StatusComponent component,
        bool initializeAsEstablished)
    {
        TownInfo town;
        try
        {
            town = _locations.GetLocation(person).HomeTown;
        }
        catch
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(component.LocalTownId))
        {
            component.LocalTownId = town.Id;
            component.LocalTownSinceYear = initializeAsEstablished
                ? _gameState.Year - _rules.YearsToFullRecognition
                : _gameState.Year;
            return;
        }

        if (!component.LocalTownId.Equals(town.Id, StringComparison.OrdinalIgnoreCase))
        {
            component.LocalTownId = town.Id;
            component.LocalTownSinceYear = _gameState.Year;
        }
    }
}
