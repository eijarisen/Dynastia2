namespace Dynastia.Contracts;

public sealed record HousePurchaseOfferInfo(
    string OfferId,
    TownInfo Town,
    int OfferYear,
    int BaseResidentCapacity,
    decimal AskingPrice,
    decimal AppraisedMarketValue);

public interface IHouseMarketService
{
    IReadOnlyList<HousePurchaseOfferInfo> GetOffers(
        IPerson householdRepresentative,
        TownInfo town,
        int year);

    HousePurchaseOfferInfo? ResolveOffer(
        IPerson householdRepresentative,
        string townId,
        int offerYear,
        string offerId);
}
