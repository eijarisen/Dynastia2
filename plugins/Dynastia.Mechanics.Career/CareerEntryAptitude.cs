namespace Dynastia.Mechanics.Career;

internal enum CareerEntryAptitude
{
    Strength,
    Intellect
}

internal static class CareerEntryAptitudeClassifier
{
    // These careers are primarily office, professional, scientific,
    // creative, analytical, or technical paths for job-entry purposes.
    // All remaining careers use Strength because their entry path is
    // primarily manual/physical.
    private static readonly HashSet<string>
        IntellectDrivenCareers =
            new(
                StringComparer.OrdinalIgnoreCase)
            {
                "newspapers_and_publishing",
                "banking",
                "insurance",
                "tailoring_and_fashion",
                "jewellery_and_watchmaking",
                "photography",
                "real_estate",
                "accounting",
                "legal_services",
                "healthcare_services",
                "pharmacy",
                "education",
                "cinema_and_film",
                "advertising",
                "chemical_industry",
                "beauty_and_cosmetics",
                "radio_broadcasting",
                "aviation",
                "tourism_and_travel",
                "pharmaceuticals",
                "television",
                "telecommunications",
                "engineering_services",
                "computing_and_it_services",
                "investment_and_financial_services",
                "biotechnology",
                "software_industry",
                "video_game_industry",
                "business_process_outsourcing",
                "digital_media",
                "cybersecurity"
            };

    public static CareerEntryAptitude Get(
        CareerDefinition career)
    {
        return IntellectDrivenCareers.Contains(
            career.Id)
                ? CareerEntryAptitude.Intellect
                : CareerEntryAptitude.Strength;
    }

    public static string GetStatId(
        CareerEntryAptitude aptitude)
    {
        return aptitude ==
            CareerEntryAptitude.Intellect
                ? "intellect"
                : "strength";
    }
}

internal sealed record EmploymentOpportunity(
    CareerDefinition Career,
    CareerEntryAptitude Aptitude,
    string StatId,
    int StatValue,
    double SuccessChance);
