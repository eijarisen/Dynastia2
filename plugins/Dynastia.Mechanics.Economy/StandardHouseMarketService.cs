using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

internal sealed class StandardHouseMarketService : IHouseMarketService
{
    private readonly IGameState _gameState;
    private readonly ILocationService _locations;
    private readonly IEconomyService _economy;
    private readonly ITownProsperityService _prosperity;
    private readonly HouseMarketRules _rules;
    private readonly ILocalServiceTownResolver? _localServiceTowns;
    private readonly Func<ICommunityPolicyService?>? _communityResolver;

    public StandardHouseMarketService(
        IGameState gameState,
        ILocationService locations,
        IEconomyService economy,
        ITownProsperityService prosperity,
        HouseMarketRules rules,
        ILocalServiceTownResolver? localServiceTowns = null,
        Func<ICommunityPolicyService?>? communityResolver = null)
    {
        _gameState = gameState;
        _locations = locations;
        _economy = economy;
        _prosperity = prosperity;
        _rules = rules;
        _localServiceTowns = localServiceTowns;
        _communityResolver = communityResolver;
    }

    public IReadOnlyList<HousePurchaseOfferInfo> GetOffers(
        IPerson householdRepresentative,
        TownInfo town,
        int year)
    {
        ArgumentNullException.ThrowIfNull(householdRepresentative);
        ArgumentNullException.ThrowIfNull(town);

        var marketTown = _localServiceTowns?.Resolve(town.Id, year) ?? town;
        var range = _rules.GetOfferCountRange(town.SettlementClass);
        var countUnit = Unit(BuildKey(town.Id, year, "count"));
        var count = range.Min + (int)Math.Floor(countUnit * (range.Max - range.Min + 1));
        count = Math.Clamp(count, range.Min, range.Max);
        var policyModifiers = _communityResolver?.Invoke()?
            .GetModifiers(marketTown, year)
            ?? new CommunityPolicyModifierSnapshot();
        count += policyModifiers.ExtraHousingOffers;

        var prosperity = _prosperity.Get(marketTown, year);
        var prosperityMultiplier = _rules.GetProsperityMultiplier(prosperity.Index);
        var basePrice = GetHousePriceForYear(town, marketTown, year);
        var offers = new List<HousePurchaseOfferInfo>(count);

        for (var slot = 0; slot < count; slot++)
        {
            var capacity = _rules.DrawCapacity(
                town.SettlementClass,
                Unit(BuildKey(town.Id, year, $"capacity:{slot}")));
            var priceFactorUnit = Unit(BuildKey(town.Id, year, $"price:{slot}"));
            var randomMultiplier = _rules.OfferRandomMinimum
                + (decimal)priceFactorUnit
                * (_rules.OfferRandomMaximum - _rules.OfferRandomMinimum);
            var capacityMultiplier = _rules.GetCapacityMultiplier(capacity);
            var askingPrice = RoundCurrency(
                basePrice
                * capacityMultiplier
                * prosperityMultiplier
                * randomMultiplier);
            var appraisal = RoundCurrency(
                basePrice
                * capacityMultiplier
                * prosperityMultiplier);
            var offerId = CreateOfferId(town.Id, year, slot);

            offers.Add(new HousePurchaseOfferInfo(
                offerId,
                town,
                year,
                capacity,
                askingPrice,
                appraisal));
        }

        return offers;
    }

    public HousePurchaseOfferInfo? ResolveOffer(
        IPerson householdRepresentative,
        string townId,
        int offerYear,
        string offerId)
    {
        if (string.IsNullOrWhiteSpace(townId)
            || string.IsNullOrWhiteSpace(offerId))
        {
            return null;
        }

        var town = _locations.FindTownAtYear(townId, offerYear);
        return town is null
            ? null
            : GetOffers(householdRepresentative, town, offerYear)
                .FirstOrDefault(offer => offer.OfferId.Equals(
                    offerId,
                    StringComparison.OrdinalIgnoreCase));
    }

    private decimal GetHousePriceForYear(
        TownInfo town,
        TownInfo marketTown,
        int year)
    {
        var currentPrice = _economy.GetHousePrice(town);
        var community = _communityResolver?.Invoke();
        if (community is null)
            return currentPrice;

        var currentMultiplier = community
            .GetModifiers(marketTown, _gameState.Year)
            .HousingPriceMultiplier;
        var requestedMultiplier = community
            .GetModifiers(marketTown, year)
            .HousingPriceMultiplier;
        var normalizedPrice = currentMultiplier == 0m
            ? currentPrice
            : currentPrice / currentMultiplier;
        return RoundCurrency(normalizedPrice * requestedMultiplier);
    }

    private string BuildKey(string townId, int year, string salt) =>
        $"{WorldKey()}|{townId}|{year.ToString(CultureInfo.InvariantCulture)}|{salt}";

    private string CreateOfferId(string townId, int year, int slot)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            BuildKey(townId, year, $"offer:{slot}")));
        return Convert.ToHexString(bytes.AsSpan(0, 8)).ToLowerInvariant();
    }

    private string WorldKey()
    {
        var anchor = _gameState.People.FirstOrDefault()?.Id.ToString("N") ?? "no-anchor";
        return $"{_gameState.DynastySurname}|{_gameState.StartYear}|{anchor}";
    }

    private static double Unit(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var value = BinaryPrimitives.ReadUInt64LittleEndian(bytes) >> 11;
        return value * (1.0 / (1UL << 53));
    }

    private static decimal RoundCurrency(decimal amount) =>
        Math.Round(amount, 0, MidpointRounding.AwayFromZero);
}
