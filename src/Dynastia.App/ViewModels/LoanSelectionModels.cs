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

    public string RoleText =>
        IsGivingLoan ? "Borrower" : "Lender";

    public string OriginNationalityText =>
        $"Birthplace: {Offer.OriginTownDisplayName} · Nationality: {Offer.DisplayNationality}";

    public string AmountText =>
        $"Amount: {Offer.Terms.Principal:N0} zł";

    public string DurationText =>
        $"Duration: {Offer.Terms.DurationYears} " +
        (Offer.Terms.DurationYears == 1 ? "year" : "years");

    public string InterestText =>
        $"Interest: {Offer.Terms.TotalInterestRate:P1}";

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

    public LoanSelectionResult ToSelection() =>
        new(
            Offer.Terms.Principal,
            Offer.Terms.DurationYears,
            Offer.CounterpartyName,
            Offer.OriginTownId,
            Offer.NationalityId,
            Offer.Terms.InterestMultiplier);
}
