using Avalonia.Media;
using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed record LoanSelectionResult(
    decimal Principal,
    int DurationYears,
    string CounterpartyName,
    string CounterpartyTownId,
    string CounterpartyNationalityId,
    decimal InterestMultiplier = 1m);

public sealed class LoanOfferCardViewModel
{
    private static readonly IBrush FavorableBrush =
        new SolidColorBrush(Color.Parse("#2F6F3E"));
    private static readonly IBrush FairBrush =
        new SolidColorBrush(Color.Parse("#806633"));
    private static readonly IBrush UnfavorableBrush =
        new SolidColorBrush(Color.Parse("#A13A2B"));

    public LoanOfferCardViewModel(
        LoanOfferInfo offer,
        bool isGivingLoan)
    {
        Offer = offer;
        IsGivingLoan = isGivingLoan;
    }

    public LoanOfferInfo Offer { get; }
    public bool IsGivingLoan { get; }

    public string PortraitEmoji => Offer.PortraitEmoji;

    public string Heading =>
        $"{Offer.CounterpartyName}, {Offer.CounterpartyAge}";


    public string OriginNationalityText =>
        $"Birthplace: {Offer.OriginTownDisplayName} · Nationality: {Offer.DisplayNationality}";

    public string AmountText =>
        $"Amount: {Offer.Terms.Principal:N0} zł";

    public string DurationText =>
        $"Duration: {Offer.Terms.DurationYears} " +
        (Offer.Terms.DurationYears == 1 ? "year" : "years");

    public string InterestText =>
        $"Interest: {Offer.Terms.TotalInterestRate:P1}";

    public string FavorabilityText =>
        $"Favorability: {GetFavorabilityLabel()}";

    public IBrush FavorabilityBrush =>
        GetFavorabilityScore() switch
        {
            > 0 => FavorableBrush,
            < 0 => UnfavorableBrush,
            _ => FairBrush
        };

    public string AnnualPaymentText =>
        IsGivingLoan
            ? $"Yearly repayment: {Offer.Terms.AnnualPayment:N0} zł"
            : $"Yearly payment: {Offer.Terms.AnnualPayment:N0} zł";

    public string TotalText =>
        IsGivingLoan
            ? $"Total expected return: {Offer.Terms.TotalRepayment:N0} zł"
            : $"Total amount to repay: {Offer.Terms.TotalRepayment:N0} zł";

    public string ActionText =>
        IsGivingLoan ? "Lend" : "Borrow";

    private int GetFavorabilityScore()
    {
        var multiplier = Offer.Terms.InterestMultiplier;
        if (IsGivingLoan)
        {
            if (multiplier >= 1.05m)
                return 1;
            if (multiplier < 0.95m)
                return -1;
            return 0;
        }

        if (multiplier <= 0.95m)
            return 1;
        if (multiplier > 1.05m)
            return -1;
        return 0;
    }

    private string GetFavorabilityLabel() =>
        GetFavorabilityScore() switch
        {
            > 0 => "Favorable",
            < 0 => "Unfavorable",
            _ => "Fair"
        };

    public LoanSelectionResult ToSelection() =>
        new(
            Offer.Terms.Principal,
            Offer.Terms.DurationYears,
            Offer.CounterpartyName,
            Offer.OriginTownId,
            Offer.NationalityId,
            Offer.Terms.InterestMultiplier);
}
