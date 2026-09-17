using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class CareerViewModel
{
    public CareerViewModel(
        CareerSnapshot snapshot,
        bool isAlive,
        bool isFarmWorker = false)
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
            isFarmWorker
                ? $"Occupation: {FarmingPresentationDefaults.WorkerOccupationLabel}"
                : snapshot.JobLevel > 0
                    ? $"Occupation: " +
                      $"{snapshot.JobTitle} " +
                      $"({snapshot.JobLevel})"
                    : $"Occupation: " +
                      $"{snapshot.JobTitle}";

        var unemployedAdult =
            isAlive
            && !snapshot.IsRetired
            && snapshot.JobLevel <= 0
            && snapshot.StatusId?.Equals(
                "status.unemployed",
                StringComparison.OrdinalIgnoreCase) == true;

        var housewife =
            isAlive
            && !snapshot.IsRetired
            && snapshot.JobLevel <= 0
            && snapshot.StatusId?.Equals(
                "status.housewife",
                StringComparison.OrdinalIgnoreCase) == true;

        var householdRole =
            isAlive
            && !snapshot.IsRetired
            && snapshot.JobLevel <= 0
            && (
                snapshot.StatusId?.Equals(
                    "role.family_nanny",
                    StringComparison.OrdinalIgnoreCase) == true
                || snapshot.StatusId?.Equals(
                    "role.nanny",
                    StringComparison.OrdinalIgnoreCase) == true
            );

        var nonWorkingAdult =
            unemployedAdult
            || housewife
            || householdRole;

        SatisfactionLabel =
            isAlive
            && (!snapshot.IsRetired || snapshot.IsSelfEmployed)
                ? snapshot.IsEmployed
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
                : snapshot.IsSelfEmployed
                    ? $"Income: " +
                      $"{snapshot.AnnualIncome:N0} zł/year"
                    : snapshot.IsRetired
                        ? $"Pension: " +
                          $"{snapshot.AnnualIncome:N0} zł/year"
                        : snapshot.IsEmployed
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
