using Dynastia.Contracts;

namespace Dynastia.Mechanics.TownLife;

internal sealed class StandardTownFacilityQualityService : ITownFacilityQualityService
{
    private static readonly BankOfferQualityInfo NoBank =
        new(0, "Unavailable", 1m, 1m, 1m, 1m, 1m, 1m);

    private static readonly MedicalQualityInfo NoMedical =
        new(0, "Unavailable", 0, 1m);

    private readonly ITownInstitutionService _institutions;
    private readonly TownFacilityQualityCatalog _catalog;
    private readonly Func<ICommunityPolicyService?> _communityResolver;

    public StandardTownFacilityQualityService(
        ITownInstitutionService institutions,
        TownFacilityQualityCatalog catalog,
        Func<ICommunityPolicyService?>? communityResolver = null)
    {
        _institutions = institutions;
        _catalog = catalog;
        _communityResolver = communityResolver ?? (() => null);
    }

    public BankOfferQualityInfo GetBankQuality(TownInfo town, int year)
    {
        var modifiers = _communityResolver()?.GetModifiers(town, year)
            ?? new CommunityPolicyModifierSnapshot();
        var baseTier = _institutions.Resolve(town, year).GetTier("bank");
        var tier = Math.Clamp(baseTier + modifiers.BankServiceTierAdd, 0, 5);
        if (tier <= 0)
            return NoBank;
        if (!_catalog.Bank.TryGetValue(tier, out var quality))
            throw new InvalidDataException($"No bank quality row for Tier {tier}.");

        var multiplier = modifiers.BankQualityMultiplier;
        if (multiplier == 1m)
            return quality;

        return quality with
        {
            PrincipalMultiplierMin = quality.PrincipalMultiplierMin * multiplier,
            PrincipalMultiplierMax = quality.PrincipalMultiplierMax * multiplier,
            InterestMultiplierMin = quality.InterestMultiplierMin / multiplier,
            InterestMultiplierMax = quality.InterestMultiplierMax / multiplier,
            DurationMultiplierMin = quality.DurationMultiplierMin * multiplier,
            DurationMultiplierMax = quality.DurationMultiplierMax * multiplier,
            LendingPrincipalMultiplierMin = quality.LendingPrincipalMultiplierMin * multiplier,
            LendingPrincipalMultiplierMax = quality.LendingPrincipalMultiplierMax * multiplier,
            LendingInterestMultiplierMin = quality.LendingInterestMultiplierMin * multiplier,
            LendingInterestMultiplierMax = quality.LendingInterestMultiplierMax * multiplier,
            LendingDurationMultiplierMin = quality.LendingDurationMultiplierMin / multiplier,
            LendingDurationMultiplierMax = quality.LendingDurationMultiplierMax / multiplier
        };
    }

    public MedicalQualityInfo GetMedicalQuality(TownInfo town, int year)
    {
        var modifiers = _communityResolver()?.GetModifiers(town, year)
            ?? new CommunityPolicyModifierSnapshot();
        var baseTier = _institutions.Resolve(town, year).GetTier("medical");
        var tier = Math.Clamp(baseTier + modifiers.MedicalServiceTierAdd, 0, 5);
        if (tier <= 0)
            return NoMedical;
        if (!_catalog.Medical.TryGetValue(tier, out var quality))
            throw new InvalidDataException($"No medical quality row for Tier {tier}.");

        return quality with
        {
            TreatmentSuccessAdd = quality.TreatmentSuccessAdd + modifiers.MedicalSuccessAdd,
            TreatmentCostMultiplier = quality.TreatmentCostMultiplier * modifiers.MedicalCostMultiplier
        };
    }
}
