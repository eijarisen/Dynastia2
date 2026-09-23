using System.Text.Json;
using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Mechanics.Career;
using Dynastia.Mechanics.Justice;

namespace Dynastia.Core.Tests;

public sealed class ContentReworkBatch2CareerCrimeTests
{
    private static readonly string[] LegacyCareerIds =
    [
        "agriculture_and_farm_estates", "horse_and_carriage_trade", "blacksmithing", "coal_mining",
        "oil_and_refining", "steel_and_foundry", "textile_mills", "timber_and_sawmills", "construction",
        "railways", "shipping_and_ports", "mass_factory_manufacturing", "traditional_printworks",
        "newspapers_and_publishing", "retail_trade", "banking", "insurance", "hotels_and_restaurants",
        "food_processing", "baking_and_confectionery", "meat_trade", "tailoring_and_fashion",
        "leather_and_shoemaking", "furniture_and_carpentry", "glass_and_ceramics",
        "jewellery_and_watchmaking", "photography", "domestic_service", "laundry_and_dry_cleaning",
        "funeral_services", "real_estate", "accounting", "legal_services", "healthcare_services",
        "pharmacy", "education", "electric_power", "cinema_and_film", "advertising",
        "automotive_industry", "chemical_industry", "beauty_and_cosmetics", "radio_broadcasting",
        "aviation", "road_haulage", "consumer_goods_and_appliances", "tourism_and_travel",
        "plastics_industry", "electronics_manufacturing", "pharmaceuticals", "television",
        "telecommunications", "engineering_services", "logistics_and_warehousing",
        "computing_and_it_services", "investment_and_financial_services", "private_security",
        "biotechnology", "software_industry", "video_game_industry", "business_process_outsourcing",
        "e_commerce", "digital_media", "cybersecurity", "renewable_energy"
    ];

    private static readonly string[] LegacyCrimeIds =
    [
        "vandalism", "brawling", "petty_theft", "burglary", "smuggling",
        "fraud", "embezzlement", "robbery", "aggravated_assault", "arson",
        "manslaughter", "kidnapping", "murder"
    ];

    [Fact]
    public void CareerCatalogDataExpandsBeyondLegacyLimitWithoutDroppingSaveIds()
    {
        var rows = ReadCsv(CreateRepositoryData().ReadText("Career/careers.csv"));
        var ids = rows.Select(row => row["Id"]).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(76, rows.Count);
        Assert.True(rows.Count > 65);
        Assert.All(LegacyCareerIds, id => Assert.Contains(id, ids));
        Assert.Contains("performing_arts", ids);
        Assert.Contains("scientific_research", ids);
        Assert.Contains("municipal_utilities", ids);
    }

    [Fact]
    public void CareerMetadataSupportsAppealEducationProfilesFamiliesAndCorrectedStarts()
    {
        var rows = ReadCsv(CreateRepositoryData().ReadText("Career/careers.csv"));
        var performingArts = rows.Single(row => row["Id"] == "performing_arts");
        var research = rows.Single(row => row["Id"] == "scientific_research");
        var radio = rows.Single(row => row["Id"] == "radio_broadcasting");
        var television = rows.Single(row => row["Id"] == "television");

        Assert.Equal("appeal", performingArts["PrimaryStat"]);
        Assert.Equal("intellect", performingArts["SecondaryStat"]);
        Assert.Equal("Clerical", performingArts["EducationProfile"]);
        Assert.Equal("media_creative", performingArts["CareerFamily"]);
        Assert.Equal("intellect", research["PrimaryStat"]);
        Assert.Equal("-", research["SecondaryStat"]);
        Assert.Equal("Academic", research["EducationProfile"]);
        Assert.Equal("1925", radio["StartYear"]);
        Assert.Equal("1952", television["StartYear"]);
    }

    [Fact]
    public void CareerAptitudeCompositeUsesPrimaryAndSecondaryStatsAtSeventyFiveTwentyFive()
    {
        Assert.Equal(4.0, CareerBalanceRules.GetCompositeAptitude(5, 1), 6);
        Assert.Equal(2.0, CareerBalanceRules.GetCompositeAptitude(1, 5), 6);
        Assert.Equal(5.0, CareerBalanceRules.GetCompositeAptitude(5), 6);
    }

    [Fact]
    public void EducationProfilePenaltyRemainsSoftRatherThanAnEligibilityBan()
    {
        Assert.Equal(1.0, CareerBalanceRules.GetEducationPromotionMultiplier(3, 3), 6);
        Assert.Equal(0.35, CareerBalanceRules.GetEducationPromotionMultiplier(2, 3), 6);
        Assert.Equal(0.10, CareerBalanceRules.GetEducationPromotionMultiplier(1, 3), 6);
    }

    [Fact]
    public void CareerContextUsesAgeAndCareerSpecificTemperamentInsteadOfUniversalRanking()
    {
        var data = CreateRepositoryData();
        var rows = ReadCsv(data.ReadText("Career/careers.csv"));
        var catalog = new ContextWeightService(data).LoadCatalog(
            "Career/career_context_weights.csv",
            rows.Select(row => row["Id"]));

        var youngSanguine = catalog.GetMultiplier(
            "performing_arts",
            new ContextWeightContext(1900, 25, Sex.Male, "Sanguine"));
        var youngPhlegmatic = catalog.GetMultiplier(
            "performing_arts",
            new ContextWeightContext(1900, 25, Sex.Male, "Phlegmatic"));
        var olderSanguine = catalog.GetMultiplier(
            "performing_arts",
            new ContextWeightContext(1900, 60, Sex.Male, "Sanguine"));

        Assert.True(youngSanguine > youngPhlegmatic);
        Assert.True(youngSanguine > olderSanguine);

        var agricultureSanguine = catalog.GetMultiplier(
            "agriculture_and_farm_estates",
            new ContextWeightContext(1900, 40, Sex.Male, "Sanguine"));
        var agriculturePhlegmatic = catalog.GetMultiplier(
            "agriculture_and_farm_estates",
            new ContextWeightContext(1900, 40, Sex.Male, "Phlegmatic"));
        Assert.True(agriculturePhlegmatic > agricultureSanguine);
    }

    [Fact]
    public void CrimeAttemptContextMatchesBatchRegressionExamples()
    {
        var data = CreateRepositoryData();
        var context = new ContextWeightService(data).LoadGlobalCatalog(
            "Justice/crime_attempt_context_weights.csv");
        var rules = LoadAttemptRules(data);

        double Chance(ContextWeightContext value, bool broke = false, double stress = 0) =>
            CrimeRules.CalculateAttemptChance(
                rules,
                context.GetMultiplier("global", value),
                broke,
                stress);

        var melancholic = Chance(new ContextWeightContext(2026, 40, Sex.Male, "Melancholic", "Neutral"));
        var phlegmatic = Chance(new ContextWeightContext(2026, 40, Sex.Male, "Phlegmatic", "Neutral"));
        var sanguine = Chance(new ContextWeightContext(2026, 40, Sex.Male, "Sanguine", "Neutral"));
        var choleric = Chance(new ContextWeightContext(2026, 40, Sex.Male, "Choleric", "Neutral"));

        Assert.Equal(0.00864, phlegmatic, 6);
        Assert.Equal(melancholic, phlegmatic, 6);
        Assert.True(sanguine < phlegmatic);
        Assert.True(choleric > phlegmatic);

        var good = Chance(new ContextWeightContext(2026, 40, Sex.Male, "Phlegmatic", "Good"));
        var neutral = phlegmatic;
        var evil = Chance(new ContextWeightContext(2026, 40, Sex.Male, "Phlegmatic", "Evil"));
        Assert.True(good < neutral);
        Assert.True(evil > neutral);

        Assert.Equal(
            0.001656,
            Chance(new ContextWeightContext(2026, 40, Sex.Female, "Sanguine", "Good")),
            6);
        Assert.Equal(
            0.02309472,
            Chance(new ContextWeightContext(2026, 25, Sex.Male, "Choleric", "Evil")),
            6);
        Assert.Equal(
            0.02156544,
            Chance(new ContextWeightContext(2026, 25, Sex.Male, "Phlegmatic", "Neutral"), broke: true, stress: 5),
            6);
    }

    [Fact]
    public void PovertyAndStressRaiseAttemptChanceWithoutExceedingConfiguredCap()
    {
        var rules = LoadAttemptRules(CreateRepositoryData());
        var baseline = CrimeRules.CalculateAttemptChance(rules, 1.0, broke: false, stress: 0);
        var poor = CrimeRules.CalculateAttemptChance(rules, 1.0, broke: true, stress: 0);
        var stressed = CrimeRules.CalculateAttemptChance(rules, 1.0, broke: false, stress: 5);
        var extreme = CrimeRules.CalculateAttemptChance(rules, 99, broke: true, stress: 999);

        Assert.Equal(0.008, baseline, 6);
        Assert.True(poor > baseline);
        Assert.True(stressed > baseline);
        Assert.Equal(0.04, extreme, 6);
    }

    [Fact]
    public void CrimeCatalogContainsTwentyTwoEraAwareDefinitions()
    {
        var crimes = LoadCrimes(CreateRepositoryData());
        Assert.Equal(22, crimes.Count);
        var ids = crimes.Select(crime => crime.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.All(LegacyCrimeIds, id => Assert.Contains(id, ids));

        var cybercrime = crimes.Single(crime => crime.Id == "cybercrime");
        Assert.False(cybercrime.IsAvailable(1979, 30));
        Assert.True(cybercrime.IsAvailable(1980, 30));

        var embezzlement = crimes.Single(crime => crime.Id == "embezzlement");
        var taxEvasion = crimes.Single(crime => crime.Id == "tax_evasion");
        Assert.True(embezzlement.RequiresEmployment);
        Assert.True(taxEvasion.RequiresEmployment);
    }

    [Fact]
    public void CrimeSelectionMetadataRedirectsPovertyAndStressWithoutChangingGlobalGate()
    {
        var crimes = LoadCrimes(CreateRepositoryData());
        var theft = crimes.Single(crime => crime.Id == "petty_theft");
        var fraud = crimes.Single(crime => crime.Id == "fraud");
        var brawling = crimes.Single(crime => crime.Id == "brawling");

        Assert.True(theft.PovertyMultiplier > fraud.PovertyMultiplier);
        Assert.True(
            CrimeRules.CalculateStressSelectionMultiplier(brawling, 5)
            > CrimeRules.CalculateStressSelectionMultiplier(fraud, 5));
        Assert.Contains("property", theft.BehaviorTags);
        Assert.Contains("violent", brawling.BehaviorTags);
    }

    [Fact]
    public void ProfitCrimeSuccessUsesDataDefinedAptitudeAndPlannedDetectionUsesIntellect()
    {
        var crimes = LoadCrimes(CreateRepositoryData());
        var fraud = crimes.Single(crime => crime.Id == "fraud");

        var lowAptitude = CrimeRules.CalculateAptitude(
            fraud,
            stat => stat == "intellect" ? 1 : 5);
        var highAptitude = CrimeRules.CalculateAptitude(
            fraud,
            stat => stat == "intellect" ? 5 : 1);

        Assert.True(highAptitude > lowAptitude);
        Assert.True(
            CrimeRules.CalculateProfitSuccessChance(fraud, highAptitude)
            > CrimeRules.CalculateProfitSuccessChance(fraud, lowAptitude));
        Assert.True(fraud.IsPlanned);
        Assert.True(
            CrimeRules.CalculateDetectionChance(fraud, 5)
            < CrimeRules.CalculateDetectionChance(fraud, 1));
    }

    [Fact]
    public void TransportTheftKeepsStableIdWhilePresentationChangesByEra()
    {
        var data = CreateRepositoryData();
        var crimes = LoadCrimes(data);
        var crime = crimes.Single(definition => definition.Id == "transport_theft");
        var catalog = CrimeHistoricalCatalog.Load(data, crimes.Select(definition => definition.Id));

        Assert.Equal("horse theft", catalog.Resolve(crime, 1850).DisplayName);
        Assert.Equal("motor vehicle theft", catalog.Resolve(crime, 1920).DisplayName);
        Assert.Equal("vehicle theft", catalog.Resolve(crime, 2000).DisplayName);
        Assert.Equal("transport_theft", crime.Id);
    }

    private static CrimeAttemptRules LoadAttemptRules(IGameDataService data) =>
        JsonSerializer.Deserialize<CrimeAttemptRules>(
            data.ReadText("Justice/crime_attempt_rules.json"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private static List<CrimeDefinition> LoadCrimes(IGameDataService data) =>
        JsonSerializer.Deserialize<List<CrimeDefinition>>(
            data.ReadText("Common/crimes.json"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private static List<Dictionary<string, string>> ReadCsv(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var headers = lines[0].TrimStart('\uFEFF').Split(',');
        return lines.Skip(1)
            .Select(line => line.Split(','))
            .Select(fields => headers
                .Select((header, index) => (header, value: fields[index].Trim()))
                .ToDictionary(pair => pair.header, pair => pair.value, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private static IGameDataService CreateRepositoryData() =>
        new JsonGameDataService(RepositoryFiles.Path("data"));
}
