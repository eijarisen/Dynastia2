using Dynastia.Mechanics.Economy;
using Dynastia.Mechanics.Loans;

namespace Dynastia.Core.Tests;

public sealed class LoanAndDebtRulesTests
{
    [Fact]
    public void OneYearLoanUsesTwentyPercentTotalInterest()
    {
        var terms =
            LoanTermsCalculator.Calculate(
                1000m,
                1);

        Assert.Equal(0.20m, terms.TotalInterestRate);
        Assert.Equal(1200m, terms.TotalRepayment);
        Assert.Equal(1200m, terms.AnnualPayment);
    }

    [Fact]
    public void FiftyYearLoanUsesTwoHundredPercentTotalInterest()
    {
        var terms =
            LoanTermsCalculator.Calculate(
                10000m,
                50);

        Assert.Equal(2.00m, terms.TotalInterestRate);
        Assert.Equal(30000m, terms.TotalRepayment);
        Assert.Equal(600m, terms.AnnualPayment);
    }

    [Fact]
    public void TenYearLoanInterpolatesTotalInterestLinearly()
    {
        var terms =
            LoanTermsCalculator.Calculate(
                10000m,
                10);

        Assert.InRange(
            terms.TotalInterestRate,
            0.5306m,
            0.5307m);

        Assert.Equal(15306m, terms.TotalRepayment);
        Assert.Equal(1531m, terms.AnnualPayment);
    }


    [Fact]
    public void LoanPaymentsAreRoundedToWholeZloty()
    {
        var terms =
            LoanTermsCalculator.Calculate(
                7000m,
                17);

        Assert.Equal(
            decimal.Truncate(terms.TotalRepayment),
            terms.TotalRepayment);

        Assert.Equal(
            decimal.Truncate(terms.AnnualPayment),
            terms.AnnualPayment);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(1500)]
    [InlineData(11000)]
    public void PrincipalMustUseAllowedOneThousandIncrements(
        int principal)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => LoanTermsCalculator.Calculate(
                principal,
                10));
    }

    [Fact]
    public void ExistingDebtSurvivesOrdinaryFinancePass()
    {
        var result =
            EconomyBalanceRules.ApplyOrdinaryAnnualFinance(
                -600m,
                500m,
                250m);

        Assert.Equal(-100m, result);
    }

    [Fact]
    public void IncomeClearsDebtBeforeOrdinaryExpenses()
    {
        var result =
            EconomyBalanceRules.ApplyOrdinaryAnnualFinance(
                -600m,
                1000m,
                250m);

        Assert.Equal(150m, result);
    }

    [Fact]
    public void OrdinaryExpensesStillCannotCreateDebt()
    {
        var result =
            EconomyBalanceRules.ApplyOrdinaryAnnualFinance(
                100m,
                0m,
                250m);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void PositiveOrdinaryTransfer_FillsExistingDebtBeforeCreatingCash()
    {
        Assert.Equal(
            -600m,
            EconomyBalanceRules.ApplyOrdinaryWealthChange(
                -1600m,
                1000m));
    }

    [Fact]
    public void OrdinarySpending_CannotDeepenOrEraseExistingDebt()
    {
        Assert.Equal(
            -600m,
            EconomyBalanceRules.ApplyOrdinaryWealthChange(
                -600m,
                -250m));
    }
}
