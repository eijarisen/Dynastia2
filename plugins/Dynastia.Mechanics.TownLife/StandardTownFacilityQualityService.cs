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

    public StandardTownFacilityQualityService(
        ITownInstitutionService institutions,
        TownFacilityQualityCatalog catalog)
    {
        _institutions = institutions;
        _catalog = catalog;
    }

    public BankOfferQualityInfo GetBankQuality(TownInfo town, int year)
    {
        var tier = _institutions.Resolve(town, year).GetTier("bank");
        return tier <= 0
            ? NoBank
            : _catalog.Bank.TryGetValue(tier, out var quality)
                ? quality
                : throw new InvalidDataException($"No bank quality row for Tier {tier}.");
    }

    public MedicalQualityInfo GetMedicalQuality(TownInfo town, int year)
    {
        var tier = _institutions.Resolve(town, year).GetTier("medical");
        return tier <= 0
            ? NoMedical
            : _catalog.Medical.TryGetValue(tier, out var quality)
                ? quality
                : throw new InvalidDataException($"No medical quality row for Tier {tier}.");
    }
}
