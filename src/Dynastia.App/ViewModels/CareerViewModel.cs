using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class CareerViewModel
{
    public CareerViewModel(
        CareerSnapshot snapshot,
        bool isAlive)
    {
        JobLevel =
            snapshot.JobLevel;

        JobTitle =
            snapshot.JobTitle;

        CareerId =
            snapshot.CareerId;

        CareerName =
            snapshot.CareerName;

        JobSatisfaction =
            snapshot.JobSatisfaction;

        AnnualIncome =
            snapshot.AnnualIncome;

        IsRetired =
            snapshot.IsRetired;

        OccupationText =
            snapshot.JobLevel > 0
                ? $"Occupation: " +
                  $"{snapshot.JobTitle} " +
                  $"({snapshot.JobLevel})"
                : $"Occupation: " +
                  $"{snapshot.JobTitle}";

        var unemployedAdult =
            isAlive
            && !snapshot.IsRetired
            && snapshot.JobLevel <= 0
            && snapshot.JobTitle.Equals(
                "Unemployed",
                StringComparison.OrdinalIgnoreCase);

        var housewife =
            isAlive
            && !snapshot.IsRetired
            && snapshot.JobLevel <= 0
            && snapshot.JobTitle.Equals(
                "Housewife",
                StringComparison.OrdinalIgnoreCase);

        var nonWorkingAdult =
            unemployedAdult
            || housewife;

        SatisfactionLabel =
            isAlive
            && !snapshot.IsRetired
                ? snapshot.JobLevel > 0
                    ? snapshot.JobSatisfactionText
                    : nonWorkingAdult
                        ? "N/A"
                        : string.Empty
                : string.Empty;

        SatisfactionText =
            string.IsNullOrWhiteSpace(
                SatisfactionLabel)
                ? string.Empty
                : $"Career: {SatisfactionLabel}";

        IncomeText =
            !isAlive
                ? string.Empty
                : snapshot.IsRetired
                    ? $"Pension: " +
                      $"{snapshot.AnnualIncome:N0} zł/year"
                    : snapshot.JobLevel > 0
                        ? $"Income: " +
                          $"{snapshot.AnnualIncome:N0} zł/year"
                        : nonWorkingAdult
                            ? "Income: 0 zł"
                            : string.Empty;
    }

    public int JobLevel { get; }

    public string JobTitle { get; }

    public string? CareerId { get; }

    public string? CareerName { get; }

    public int JobSatisfaction { get; }

    public decimal AnnualIncome { get; }

    public bool IsRetired { get; }

    public string OccupationText { get; }

    public string SatisfactionLabel { get; }

    public string SatisfactionText { get; }

    public bool HasSatisfaction =>
        !string.IsNullOrWhiteSpace(
            SatisfactionText);

    public string IncomeText { get; }
}
