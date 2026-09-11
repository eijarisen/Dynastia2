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
                  $"(Level {snapshot.JobLevel})"
                : $"Occupation: " +
                  $"{snapshot.JobTitle}";

        SatisfactionText =
            isAlive
            && !snapshot.IsRetired
            && snapshot.JobLevel > 0
                ? $"Job satisfaction: " +
                  $"{snapshot.JobSatisfactionText}"
                : string.Empty;

        IncomeText =
            !isAlive
                ? string.Empty
                : snapshot.IsRetired
                    ? $"Pension: " +
                      $"{snapshot.AnnualIncome:N0} zł/year"
                    : snapshot.JobLevel > 0
                        ? $"Income: " +
                          $"{snapshot.AnnualIncome:N0} zł/year"
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

    public string SatisfactionText { get; }

    public string IncomeText { get; }
}
