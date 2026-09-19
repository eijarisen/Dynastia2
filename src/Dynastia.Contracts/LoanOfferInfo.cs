namespace Dynastia.Contracts;

public sealed record LoanOfferInfo(
    string OfferKey,
    string CounterpartyName,
    Sex CounterpartySex,
    int CounterpartyAge,
    string PortraitEmoji,
    LoanTermsInfo Terms);
