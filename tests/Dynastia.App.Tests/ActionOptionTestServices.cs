using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.App.Tests;

internal static class ActionOptionTestServices
{
    internal sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;
        public CultureScope(string name) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
        public void Dispose() => CultureInfo.CurrentCulture = _previous;
    }

    internal sealed class Loans : ILoanService
    {
        public List<(Guid Person, bool Giving, decimal Maximum)> OfferRequests { get; } = [];
        public List<(decimal Principal, int Years, decimal Multiplier)> TermRequests { get; } = [];
        public LoanOfferInfo Offer { get; } = new("offer", "Banker", Sex.Male, 40, "Person",
            new LoanTermsInfo(5000m, 2, 0.2m, 6000m, 3000m, 0.85m))
        { OriginTownId = "bank-town", OriginTownDisplayName = "Bank Town", NationalityId = "polish" };
        public LoanTermsInfo CalculateTerms(decimal principal, int durationYears, decimal interestMultiplier = 1m)
        {
            TermRequests.Add((principal, durationYears, interestMultiplier));
            if (durationYears < 1) throw new ArgumentOutOfRangeException(nameof(durationYears));
            return Offer.Terms;
        }
        public IReadOnlyList<LoanOfferInfo> GetOffers(IPerson householdRepresentative, bool isGivingLoan, decimal maximumPrincipal)
        { OfferRequests.Add((householdRepresentative.Id, isGivingLoan, maximumPrincipal)); return [Offer]; }
        public bool HasActiveSelfOriginatedBankLoan(IPerson borrower) => false;
        public IReadOnlyList<LoanContractInfo> GetDebts(IPerson householdRepresentative) => [];
        public IReadOnlyList<LoanContractInfo> GetLoansGiven(IPerson householdRepresentative) => [];
    }

    internal sealed class Heirlooms : IHeirloomService
    {
        public List<HeirloomAssetInfo> Assets { get; } = [];
        public IReadOnlyList<HeirloomAssetInfo> GetHeirlooms(IPerson householdMember) => Assets;
        public decimal GetSaleValue(HeirloomAssetInfo heirloom) => 1234.5m;
        public HeirloomAssetInfo Create(IPerson householdMember, HeirloomCreationRequest request) => throw new NotSupportedException();
        public HeirloomAssetInfo? Take(IPerson householdMember, Guid heirloomId) => throw new NotSupportedException();
        public IReadOnlyList<HeirloomAssetInfo> TakeAll(IPerson householdMember) => throw new NotSupportedException();
        public void AddExisting(IPerson householdMember, HeirloomAssetInfo heirloom, int year, Guid? personId, string reason) => throw new NotSupportedException();
        public IReadOnlyList<HeirloomAssetInfo> GetPending(IPerson person) => [];
        public void AddPending(IPerson person, HeirloomAssetInfo heirloom, int year, string reason) => throw new NotSupportedException();
        public IReadOnlyList<HeirloomAssetInfo> TakePending(IPerson person) => throw new NotSupportedException();
        public bool SetInheritanceHeir(IPerson householdMember, Guid heirloomId, Guid? heirId) => throw new NotSupportedException();
        public void ClearInheritanceAssignments(Guid heirId) => throw new NotSupportedException();
    }

    internal sealed class Farming : IFarmingService
    {
        public List<FarmlandAssetInfo> Parcels { get; } = [];
        public decimal PurchasePrice => 20000m;
        public decimal SalePrice => 10000m;
        public decimal LivestockPurchasePrice => 5000m;
        public decimal LivestockSalePrice => 2345m;
        public FarmingHouseholdSnapshot GetSnapshot(IPerson householdRepresentative) => new(Parcels, "residence", Parcels.Count, 0, 0, 0m, 0m);
        public bool IsAvailableFarmWorker(IPerson person, IPerson householdRepresentative) => false;
        public bool IsWorkingFarmWorker(IPerson person, IPerson householdRepresentative) => false;
        public decimal GetExpectedAnnualIncome(IPerson householdRepresentative) => 0m;
        public decimal GetExpectedAnnualIncomeAfterAddingLocalParcel(IPerson householdRepresentative) => 0m;
        public IReadOnlyList<FarmingFlavorInfo> GetAvailableLivestockOptions(TownInfo town, int year) => [];
        public decimal GetFarmlandSaleValue(FarmlandAssetInfo farmland) => SalePrice + LivestockSalePrice;
        public FarmlandAssetInfo? AssignNewFarmlandType(IPerson householdRepresentative, Guid farmlandId) => throw new NotSupportedException();
        public FarmlandAssetInfo? EnsureFarmlandFlavor(IPerson householdRepresentative, Guid farmlandId) => throw new NotSupportedException();
        public FarmlandAssetInfo? AddLivestock(IPerson householdRepresentative, Guid farmlandId, int year) => throw new NotSupportedException();
        public FarmlandRelocationSaleResult SellOriginFarmlandForVoluntaryRelocation(IPerson householdRepresentative, TownInfo origin, TownInfo destination) => throw new NotSupportedException();
    }

    internal sealed class Family : IFamilyService
    {
        public Dictionary<Guid, (IPerson? Father, IPerson? Mother)> Parents { get; } = [];
        public void InitializePerson(IPerson person, Sex sex, int? generation = null) => throw new NotSupportedException();
        public Sex GetSex(IPerson person) => person.Tags.Has("sex.female") ? Sex.Female : Sex.Male;
        public int? GetGeneration(IPerson person) => null;
        public IPerson? GetFather(IPerson person) => Parents.GetValueOrDefault(person.Id).Father;
        public IPerson? GetMother(IPerson person) => Parents.GetValueOrDefault(person.Id).Mother;
        public IPerson? GetSpouse(IPerson person) => null;
        public IReadOnlyList<IPerson> GetChildren(IPerson person) => [];
        public void SetParents(IPerson child, IPerson? father, IPerson? mother) => Parents[child.Id] = (father, mother);
        public void SetSpouses(IPerson first, IPerson second, int startYear) => throw new NotSupportedException();
        public void EndRelationship(IPerson first, IPerson second, int endYear, string endReason, bool clearFirst = true, bool clearSecond = true) => throw new NotSupportedException();
        public IReadOnlyList<RelationshipHistoryInfo> GetRelationshipHistory(IPerson person) => [];
        public void SetGeneratedFamilyBackground(IPerson person, GeneratedFamilyBackgroundInfo background) => throw new NotSupportedException();
        public GeneratedFamilyBackgroundInfo? GetGeneratedFamilyBackground(IPerson person) => null;
        public string FormatSurname(string surname, Sex sex) => surname;
        public string GetDisplayName(IPerson person) => $"{person.Name} {person.Surname}";
        public bool IsBloodline(IPerson person) => false;
        public bool IsMaleLineage(IPerson person) => false;
    }

    internal sealed class Justice : IJusticeService
    {
        public HashSet<Guid> Prisoners { get; } = [];
        public bool IsImprisoned(IPerson person) => Prisoners.Contains(person.Id);
        public void EnsureJustice(IPerson person) => throw new NotSupportedException();
        public JusticeSnapshot GetStatus(IPerson person) => throw new NotSupportedException();
        public void Imprison(IPerson person, int sentence, string reasonId, string reasonName, string? reasonDescription = null) => throw new NotSupportedException();
        public CourtProtectionSnapshot GetCourtProtection(IPerson person) => throw new NotSupportedException();
        public int ConvictKnownOffense(IPerson person, int originalSentence, string reasonId, string reasonName, string? reasonDescription = null, decimal baseSentenceMultiplier = 1m) => throw new NotSupportedException();
        public decimal GetBailCost(IPerson person) => throw new NotSupportedException();
        public bool ReleaseFromPrison(IPerson person) => throw new NotSupportedException();
        public bool HasAttemptedEscapeThisImprisonment(IPerson person) => throw new NotSupportedException();
        public void MarkEscapeAttempted(IPerson person) => throw new NotSupportedException();
        public int ExtendSentence(IPerson person, int years) => throw new NotSupportedException();
        public double GetStolenHeirloomSaleDetectionChance(IPerson person) => throw new NotSupportedException();
    }
}
