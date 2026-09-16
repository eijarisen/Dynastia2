namespace Dynastia.Mechanics.Career;

public sealed class CareerComponent
{
    public string? CareerId { get; set; }

    public int JobLevel { get; set; }

    public int PeakJobLevel { get; set; }

    public string? PeakCareerId { get; set; }

    public int JobSatisfaction { get; set; }

    public decimal LastIncome { get; set; }

    public bool IsRetired { get; set; }

    public Dictionary<string, int> ExperienceYearsByCareer { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
