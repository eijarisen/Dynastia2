using Dynastia.Contracts;

namespace Dynastia.Mechanics.Loans;

public sealed partial class StandardLoanService :
    ILoanService
{
    private const decimal ExternalLendingInterestScale = 0.50m;

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly LoanEraCatalog _loanEras;
    private readonly IGameRandom _random;
    private readonly IHistoricalNameService _historicalNames;
    private readonly ILocationService _locations;
    private readonly IOutsiderIdentityService _outsiderIdentities;
    private readonly IAppearanceService _appearance;
    private readonly ITownFacilityQualityService _facilityQuality;

    public StandardLoanService(
        IGameState gameState,
        IFamilyService family,
        IEconomyService economy,
        LoanEraCatalog loanEras,
        IGameRandom random,
        IHistoricalNameService historicalNames,
        ILocationService locations,
        IOutsiderIdentityService outsiderIdentities,
        IAppearanceService appearance,
        ITownFacilityQualityService facilityQuality)
    {
        _gameState = gameState;
        _family = family;
        _economy = economy;
        _loanEras = loanEras;
        _random = random;
        _historicalNames = historicalNames;
        _locations = locations;
        _outsiderIdentities = outsiderIdentities;
        _appearance = appearance;
        _facilityQuality = facilityQuality;
    }

    public LoanTermsInfo CalculateTerms(
        decimal principal,
        int durationYears,
        decimal interestMultiplier = 1m) =>
        LoanTermsCalculator.Calculate(
            principal,
            durationYears,
            interestMultiplier);

    public IReadOnlyList<LoanOfferInfo> GetOffers(
        IPerson householdRepresentative,
        bool isGivingLoan,
        decimal maximumPrincipal)
    {
        ArgumentNullException.ThrowIfNull(householdRepresentative);

        var maximumThousands = Math.Min(
            LoanTermsCalculator.MaximumPrincipal / LoanTermsCalculator.PrincipalStep,
            (int)Math.Floor(maximumPrincipal / LoanTermsCalculator.PrincipalStep));

        if (maximumThousands < 1)
            return [];

        var town = _locations.GetLocation(householdRepresentative).HomeTown;
        var bankQuality = _facilityQuality.GetBankQuality(town, _gameState.Year);
        if (!bankQuality.IsAvailable)
            return [];

        int[] durations = [1, 3, 5, 10, 15, 20, 30, 40, 50];
        var offers = new List<LoanOfferInfo>(3);
        var usedTerms = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < 3; index++)
        {
            var seed = CreateOfferSeed(
                householdRepresentative.Id,
                _gameState.Year,
                isGivingLoan,
                index);

            var random = new LoanOfferRandom(seed);
            var principalThousands = random.NextInt(1, maximumThousands);
            var durationIndex = (random.NextInt(0, durations.Length - 1) + index) % durations.Length;
            var duration = durations[durationIndex];

            var termsKey = $"{principalThousands}|{duration}";
            var attempts = 0;
            while (!usedTerms.Add(termsKey) && attempts < durations.Length)
            {
                durationIndex = (durationIndex + 1) % durations.Length;
                duration = durations[durationIndex];
                termsKey = $"{principalThousands}|{duration}";
                attempts++;
            }

            var baselinePrincipal =
                principalThousands * LoanTermsCalculator.PrincipalStep;
            decimal principal = baselinePrincipal;
            var offerDuration = duration;
            var interestMultiplier = 1m;

            var qualityRandom =
                new LoanOfferRandom(CreateQualitySeed(seed));
            var principalMinimum = isGivingLoan
                ? bankQuality.LendingPrincipalMultiplierMin
                : bankQuality.PrincipalMultiplierMin;
            var principalMaximum = isGivingLoan
                ? bankQuality.LendingPrincipalMultiplierMax
                : bankQuality.PrincipalMultiplierMax;
            var interestMinimum = isGivingLoan
                ? bankQuality.LendingInterestMultiplierMin
                : bankQuality.InterestMultiplierMin;
            var interestMaximum = isGivingLoan
                ? bankQuality.LendingInterestMultiplierMax
                : bankQuality.InterestMultiplierMax;
            var durationMinimum = isGivingLoan
                ? bankQuality.LendingDurationMultiplierMin
                : bankQuality.DurationMultiplierMin;
            var durationMaximum = isGivingLoan
                ? bankQuality.LendingDurationMultiplierMax
                : bankQuality.DurationMultiplierMax;

            principal = ApplyPrincipalQuality(
                baselinePrincipal,
                maximumPrincipal,
                principalMinimum,
                principalMaximum,
                qualityRandom);
            offerDuration = ApplyDurationQuality(
                duration,
                durationMinimum,
                durationMaximum,
                qualityRandom);
            interestMultiplier = NextMultiplier(
                qualityRandom,
                interestMinimum,
                interestMaximum);
            if (isGivingLoan)
                interestMultiplier *= ExternalLendingInterestScale;

            var terms = CalculateTerms(
                principal,
                offerDuration,
                interestMultiplier);
            var counterparty = GenerateExternalCounterparty(
                householdRepresentative,
                _gameState.Year,
                seed,
                random);

            offers.Add(new LoanOfferInfo(
                seed.ToString("N"),
                counterparty.Name,
                counterparty.Sex,
                counterparty.Age,
                counterparty.PortraitEmoji,
                terms)
            {
                OriginTownId = counterparty.OriginTown.Id,
                OriginTownDisplayName = counterparty.OriginDisplayName,
                NationalityId = counterparty.NationalityId,
                DisplayNationality = counterparty.DisplayNationality
            });
        }

        return offers;
    }

    public bool HasActiveSelfOriginatedBankLoan(
        IPerson borrower) =>
        GetPortfolio(
            borrower,
            create: false)?
            .Contracts
            .Any(contract =>
                contract.Status != LoanStatus.Completed
                && !contract.IsExternalReceivable
                && contract.CreditorType == LoanCreditorType.Bank
                && contract.IsSelfOriginatedBankLoan)
        == true;

    public IReadOnlyList<LoanContractInfo> GetDebts(
        IPerson householdRepresentative)
    {
        var householdId =
            _economy.GetHouseholdId(
                householdRepresentative);

        if (householdId is null)
            return [];

        return EnumerateContracts()
            .Where(item =>
                item.Contract.Status == LoanStatus.Active
                && !item.Contract.IsExternalReceivable
                && ResolveServicingHouseholdId(
                    item.PortfolioOwner,
                    item.Contract)
                    == householdId)
            .Select(item =>
                ToInfo(
                    item.Contract,
                    1m))
            .OrderBy(info =>
                info.CreditorName,
                StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<LoanContractInfo> GetLoansGiven(
        IPerson householdRepresentative)
    {
        var householdId =
            _economy.GetHouseholdId(
                householdRepresentative);

        if (householdId is null)
            return [];

        var result =
            new List<LoanContractInfo>();

        foreach (var item in
            EnumerateContracts()
                .Where(item =>
                    item.Contract.Status == LoanStatus.Active
                    && item.Contract.CreditorType == LoanCreditorType.Private))
        {
            var share =
                item.Contract.CreditorShares
                    .Where(creditorShare =>
                        ResolveCreditorHouseholdId(
                            creditorShare)
                            == householdId)
                    .Sum(creditorShare =>
                        creditorShare.Share);

            if (share <= 0)
                continue;

            result.Add(
                ToInfo(
                    item.Contract,
                    share));
        }

        return result
            .OrderBy(info =>
                info.YearsRemaining)
            .ThenByDescending(info =>
                info.RemainingAmount)
            .ToList();
    }

    internal LoanContractState CreateBankLoan(
        IPerson borrower,
        decimal principal,
        int durationYears,
        int startYear,
        decimal interestMultiplier = 1m,
        string? externalCreditorName = null)
    {
        var terms =
            CalculateTerms(
                principal,
                durationYears,
                interestMultiplier);

        var contract =
            new LoanContractState
            {
                ContractId = _random.NextGuid(),
                CreditorType = LoanCreditorType.Bank,
                Principal = terms.Principal,
                DurationYears = terms.DurationYears,
                TotalInterestRate = terms.TotalInterestRate,
                AnnualPayment = terms.AnnualPayment,
                RemainingAmount = terms.TotalRepayment,
                StartYear = startYear,
                Status = LoanStatus.Active,
                IsSelfOriginatedBankLoan = true,
                IsExternalReceivable = false,
                ExternalCreditorName = string.IsNullOrWhiteSpace(externalCreditorName)
                    ? null
                    : externalCreditorName,
                ServicingHouseholdId = _economy.GetHouseholdId(borrower)
            };

        GetPortfolio(borrower, create: true)!
            .Contracts.Add(contract);

        return contract;
    }

    internal LoanContractState CreateExternalReceivable(
        IPerson lender,
        decimal principal,
        int durationYears,
        int startYear,
        decimal interestMultiplier = 1m)
    {
        var terms =
            CalculateTerms(
                principal,
                durationYears,
                interestMultiplier);

        var contract =
            new LoanContractState
            {
                ContractId = _random.NextGuid(),
                CreditorType = LoanCreditorType.Private,
                Principal = terms.Principal,
                DurationYears = terms.DurationYears,
                TotalInterestRate = terms.TotalInterestRate,
                AnnualPayment = terms.AnnualPayment,
                RemainingAmount = terms.TotalRepayment,
                StartYear = startYear,
                Status = LoanStatus.Active,
                IsSelfOriginatedBankLoan = false,
                IsExternalReceivable = true,
                CreditorShares =
                [
                    new LoanCreditorShareState
                    {
                        OwnerPersonId = lender.Id,
                        RecipientHouseholdId = _economy.GetHouseholdId(lender),
                        Share = 1m
                    }
                ]
            };

        // External customers are intentionally not simulated people or
        // households. The receivable lives in the lender's portfolio and
        // produces deterministic repayments from outside the dynasty.
        GetPortfolio(lender, create: true)!
            .Contracts.Add(contract);

        return contract;
    }

    internal IEnumerable<(IPerson PortfolioOwner, LoanContractState Contract)> EnumerateContracts()
    {
        foreach (var owner in _gameState.People)
        {
            var portfolio =
                GetPortfolio(
                    owner,
                    create: false);

            if (portfolio is null)
                continue;

            foreach (var contract in portfolio.Contracts)
            {
                yield return (
                    owner,
                    contract);
            }
        }
    }


    internal GeneratedLoanCounterparty GenerateExternalCounterparty(
        IPerson householdRepresentative,
        int year,
        Guid seed)
    {
        return GenerateExternalCounterparty(
            householdRepresentative,
            year,
            seed,
            new LoanOfferRandom(seed));
    }

    private GeneratedLoanCounterparty GenerateExternalCounterparty(
        IPerson householdRepresentative,
        int year,
        Guid seed,
        IGameRandom random)
    {
        var homeTown =
            _locations.GetLocation(
                householdRepresentative)
            .HomeTown;
        var originTown =
            ChooseExternalOrigin(
                homeTown,
                _locations.GetTowns(),
                random);

        var sex = year < 1918
            ? Sex.Male
            : random.Chance(0.5)
                ? Sex.Male
                : Sex.Female;
        var age = random.NextInt(28, 64);
        var identity =
            _outsiderIdentities.Generate(
                originTown,
                sex,
                year - age,
                year,
                random);
        var displaySurname =
            _historicalNames.FormatSurname(
                identity.Surname,
                sex,
                identity.NameCultureId);
        var name =
            $"{identity.FirstName} {displaySurname}";
        var appearance =
            _appearance.GenerateCandidateAppearance(
                seed,
                sex);
        var portrait =
            _appearance.GetPortrait(
                appearance,
                sex,
                age,
                seed);

        return new GeneratedLoanCounterparty(
            name,
            sex,
            age,
            portrait,
            originTown,
            identity.ForeignBirthplaceDisplayName ?? originTown.DisplayName,
            identity.NationalityId,
            identity.DisplayNationality);
    }

    private static TownInfo ChooseExternalOrigin(
        TownInfo homeTown,
        IReadOnlyList<TownInfo> towns,
        IGameRandom random)
    {
        if (towns.Count == 0)
        {
            throw new InvalidOperationException(
                "External loan generation requires at least one current destination town.");
        }

        var currentHome =
            towns.FirstOrDefault(town =>
                town.Id.Equals(
                    homeTown.Id,
                    StringComparison.OrdinalIgnoreCase));

        if (currentHome is not null
            && random.Chance(0.55))
        {
            return currentHome;
        }

        var candidates =
            towns.Where(town =>
                    currentHome is null
                    || !town.Id.Equals(
                        currentHome.Id,
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

        if (candidates.Length == 0)
            return currentHome ?? towns[0];

        var weights = candidates
            .Select(town =>
                Math.Sqrt(
                    Math.Max(1, town.Population)))
            .ToArray();
        var total = weights.Sum();
        var roll = random.NextDouble() * total;

        for (var index = 0;
            index < candidates.Length;
            index++)
        {
            if (roll < weights[index])
                return candidates[index];

            roll -= weights[index];
        }

        return candidates[^1];
    }

    private static decimal ApplyPrincipalQuality(
        decimal baselinePrincipal,
        decimal maximumPrincipal,
        decimal minimumMultiplier,
        decimal maximumMultiplier,
        IGameRandom random)
    {
        var multiplier = NextMultiplier(
            random,
            minimumMultiplier,
            maximumMultiplier);
        var adjusted = Math.Round(
            baselinePrincipal * multiplier / LoanTermsCalculator.PrincipalStep,
            0,
            MidpointRounding.AwayFromZero)
            * LoanTermsCalculator.PrincipalStep;
        var maximum = Math.Min(
            LoanTermsCalculator.MaximumPrincipal,
            Math.Floor(maximumPrincipal / LoanTermsCalculator.PrincipalStep)
                * LoanTermsCalculator.PrincipalStep);
        return Math.Clamp(
            adjusted,
            LoanTermsCalculator.MinimumPrincipal,
            maximum);
    }

    private static int ApplyDurationQuality(
        int baselineDuration,
        decimal minimumMultiplier,
        decimal maximumMultiplier,
        IGameRandom random)
    {
        var multiplier = NextMultiplier(
            random,
            minimumMultiplier,
            maximumMultiplier);
        var adjusted = (int)Math.Round(
            baselineDuration * multiplier,
            0,
            MidpointRounding.AwayFromZero);
        return Math.Clamp(
            adjusted,
            LoanTermsCalculator.MinimumDurationYears,
            LoanTermsCalculator.MaximumDurationYears);
    }

    private static decimal NextMultiplier(
        IGameRandom random,
        decimal minimum,
        decimal maximum) =>
        minimum
        + ((maximum - minimum) * (decimal)random.NextDouble());

    private static Guid CreateQualitySeed(Guid offerSeed)
    {
        var bytes = offerSeed.ToByteArray();
        bytes[8] ^= 0xB4;
        bytes[9] ^= 0x71;
        bytes[10] ^= 0x2D;
        bytes[11] ^= 0x93;
        return new Guid(bytes);
    }

    private static Guid CreateOfferSeed(
        Guid householdRepresentativeId,
        int year,
        bool isGivingLoan,
        int offerIndex)
    {
        var bytes = householdRepresentativeId.ToByteArray();
        var yearBytes = BitConverter.GetBytes(year);
        for (var index = 0; index < yearBytes.Length; index++)
            bytes[index] ^= yearBytes[index];

        bytes[4] ^= isGivingLoan ? (byte)0xA7 : (byte)0x3D;
        bytes[5] ^= unchecked((byte)(17 + offerIndex * 73));
        bytes[6] ^= unchecked((byte)(29 + offerIndex * 131));
        bytes[7] ^= unchecked((byte)(43 + offerIndex * 191));
        return new Guid(bytes);
    }

    internal sealed record GeneratedLoanCounterparty(
        string Name,
        Sex Sex,
        int Age,
        string PortraitEmoji,
        TownInfo OriginTown,
        string OriginDisplayName,
        string NationalityId,
        string DisplayNationality);

    private sealed class LoanOfferRandom : IGameRandom
    {
        private ulong _state;

        public LoanOfferRandom(Guid seed)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var state = offsetBasis;

            foreach (var value in seed.ToByteArray())
            {
                state ^= value;
                state *= prime;
            }

            _state = state == 0 ? offsetBasis : state;
        }

        public int NextInt(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxInclusive));

            var range = (ulong)((long)maxInclusive - minInclusive + 1L);
            return minInclusive + (int)(NextUInt64() % range);
        }

        public double NextDouble() =>
            (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

        public bool Chance(double probability) =>
            probability switch
            {
                <= 0 => false,
                >= 1 => true,
                _ => NextDouble() < probability
            };

        private ulong NextUInt64()
        {
            _state = unchecked(
                _state * 6364136223846793005UL
                + 1442695040888963407UL);
            return _state;
        }
    }

    internal Guid NextContractId() =>
        _random.NextGuid();

    internal LoanPortfolioComponent? GetPortfolio(
        IPerson person,
        bool create)
    {
        var component =
            person.Components.Get<LoanPortfolioComponent>();

        if (component is not null
            || !create)
        {
            return component;
        }

        component =
            new LoanPortfolioComponent();

        person.Components.Set(
            component);

        return component;
    }

    internal Guid? ResolveServicingHouseholdId(
        IPerson borrower,
        LoanContractState contract)
    {
        if (contract.IsExternalReceivable)
            return null;

        if (borrower.Tags.Has("state.alive"))
        {
            var current =
                _economy.GetHouseholdId(
                    borrower);

            if (current is not null)
            {
                contract.ServicingHouseholdId =
                    current;
            }
        }

        return contract.ServicingHouseholdId;
    }

    internal IPerson? FindHouseholdRepresentative(
        Guid householdId)
    {
        foreach (var person in _gameState.People)
        {
            if (!_economy.HasHousehold(person))
                continue;

            if (_economy.GetHouseholdId(person)
                == householdId)
            {
                return person;
            }
        }

        return null;
    }

    internal Guid? ResolveCreditorHouseholdId(
        LoanCreditorShareState share)
    {
        if (share.OwnerPersonId is Guid ownerId)
        {
            var owner =
                _gameState.People.FirstOrDefault(
                    person => person.Id == ownerId);

            if (owner is not null
                && owner.Tags.Has("state.alive"))
            {
                if (owner.Age < 18)
                    return null;

                var current =
                    _economy.GetHouseholdId(
                        owner);

                if (current is not null)
                {
                    share.RecipientHouseholdId =
                        current;

                    return current;
                }
            }
        }

        return share.RecipientHouseholdId;
    }

    internal bool HasSurvivingFormerPartnerInHousehold(
        IPerson deceasedBorrower,
        Guid householdId)
    {
        foreach (var relationship in
            _family.GetRelationshipHistory(
                deceasedBorrower))
        {
            if (!string.Equals(
                    relationship.EndReason,
                    "death",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var formerPartner =
                _gameState.People.FirstOrDefault(
                    person =>
                        person.Id == relationship.SpouseId);

            if (formerPartner is null
                || !formerPartner.Tags.Has("state.alive"))
            {
                continue;
            }

            if (_economy.GetHouseholdId(
                    formerPartner)
                == householdId)
            {
                return true;
            }
        }

        return false;
    }

    internal string GetExternalCreditorLabel(int year) =>
        _loanEras.GetRule(year).ExternalCreditorLabel;

    internal string GetCreditorName(
        LoanContractState contract)
    {
        if (contract.CreditorType
            == LoanCreditorType.Bank)
        {
            return string.IsNullOrWhiteSpace(contract.ExternalCreditorName)
                ? GetExternalCreditorLabel(_gameState.Year)
                : contract.ExternalCreditorName;
        }

        var owners =
            contract.CreditorShares
                .Select(share => share.OwnerPersonId)
                .Where(id => id is not null)
                .Distinct()
                .Select(id =>
                    _gameState.People.FirstOrDefault(
                        person => person.Id == id))
                .Where(person => person is not null)
                .Cast<IPerson>()
                .ToList();

        return owners.Count switch
        {
            0 => "Private creditor",
            1 => _family.GetDisplayName(owners[0]),
            _ => "Family heirs"
        };
    }

    private LoanContractInfo ToInfo(
        LoanContractState contract,
        decimal share)
    {
        var remainingYears =
            Math.Max(
                0,
                contract.DurationYears
                - contract.YearsPaid);

        return new LoanContractInfo(
            contract.ContractId,
            GetCreditorName(contract),
            LoanTermsCalculator.RoundCurrency(
                contract.RemainingAmount
                * share),
            LoanTermsCalculator.RoundCurrency(
                contract.AnnualPayment
                * share),
            remainingYears,
            contract.CreditorType,
            contract.Status,
            contract.IsSelfOriginatedBankLoan,
            contract.IsExternalReceivable
                ? contract.ExternalBorrowerName
                : null);
    }
}
