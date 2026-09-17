using System.Globalization;
using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Crafts;

public static class CraftVocationDataValidation
{
    private const string MasteryLevelsPath = "Crafts/craft_mastery_levels.csv";
    private const string EducationRulesPath = "Crafts/craft_education_rules.json";
    private const string ProgressionRulesPath = "Crafts/craft_mastery_progression.json";
    private const string EconomyRulesPath = "Crafts/craft_economy_rules.json";
    private const string IncomeReferencePath = "Crafts/craft_income_reference.csv";
    private const string CalibrationPath = "Crafts/craft_mastery_calibration.csv";

    public static void Validate(IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ValidateMasteryLevels(data.ReadText(MasteryLevelsPath));
        ValidateEducationRules(data.ReadText(EducationRulesPath));
        ValidateProgressionRules(data.ReadText(ProgressionRulesPath));
        ValidateEconomyRules(data.ReadText(EconomyRulesPath));
        ValidateIncomeReference(data.ReadText(IncomeReferencePath));
        ValidateCalibration(data.ReadText(CalibrationPath));
    }

    private static void ValidateMasteryLevels(string text)
    {
        var rows = NonEmptyLines(text).Skip(1).ToList();
        if (rows.Count != CraftRules.MasteryLevels.Count)
            Fail(MasteryLevelsPath, $"expected {CraftRules.MasteryLevels.Count} mastery rows but found {rows.Count}");

        for (var index = 0; index < rows.Count; index++)
        {
            var columns = rows[index].Split(',');
            if (columns.Length != 5)
                Fail(MasteryLevelsPath, $"row {index + 2} must contain 5 columns");

            var expected = CraftRules.MasteryLevels[index];
            var level = ParseInt(columns[0], MasteryLevelsPath);
            var bonus = ParseInt(columns[2], MasteryLevelsPath);
            var progress = ParseDouble(columns[3], MasteryLevelsPath);
            var years = ParseInt(columns[4], MasteryLevelsPath);

            if (level != expected.Level
                || !columns[1].Equals(expected.DisplayName, StringComparison.Ordinal)
                || bonus != expected.MasteryBonus
                || Math.Abs(progress - expected.RequiredMasteryProgress) > 0.0000001
                || years != expected.MinimumRelevantExperienceYears)
            {
                Fail(MasteryLevelsPath, $"row for level {expected.Level} does not match the implemented mastery rule");
            }
        }
    }

    private static void ValidateEducationRules(string text)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        var educationWindow = root.GetProperty("educationWindow");
        var slots = root.GetProperty("slots");
        var improvement = root.GetProperty("existingCraftImprovement");
        var unlock = root.GetProperty("newCraft");

        RequireDecimal(educationWindow, "priceForEveryOption", CraftRules.EducationCost, EducationRulesPath);
        RequireInt(slots, "maximumCrafts", CraftRules.MaximumCrafts, EducationRulesPath);
        RequireDouble(improvement, "successfulProgressGain", 3.0, EducationRulesPath);
        RequireDouble(unlock, "educationProgressGranted", 0.0, EducationRulesPath);
        RequireBool(educationWindow, "standardEducationFirst", true, EducationRulesPath);
        RequireBool(educationWindow, "unknownCraftsVisibleOnlyWhenChosenSlotEmpty", true, EducationRulesPath);
        RequireBool(educationWindow, "knownCraftsVisibleForImprovement", true, EducationRulesPath);
        RequireBool(educationWindow, "masterCraftsDisabled", true, EducationRulesPath);
        RequireBool(slots.GetProperty("inheritedSlot"), "replaceable", false, EducationRulesPath);
        RequireBool(slots.GetProperty("chosenSlot"), "replaceable", false, EducationRulesPath);
    }

    private static void ValidateProgressionRules(string text)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        var experience = root.GetProperty("experience");
        var education = root.GetProperty("education");
        var retirement = root.GetProperty("retirement");

        RequireInt(experience, "maximumCreditPerCalendarYearPerCraft", 1, ProgressionRulesPath);
        RequireDecimal(education, "cost", CraftRules.EducationCost, ProgressionRulesPath);
        RequireDouble(education, "successfulImprovementProgress", 3.0, ProgressionRulesPath);
        RequireBool(root, "automaticLeveling", true, ProgressionRulesPath);
        RequireBool(retirement, "automaticRetirement", false, ProgressionRulesPath);

        var levels = root.GetProperty("levels").EnumerateArray().ToList();
        if (levels.Count != CraftRules.MasteryLevels.Count)
            Fail(ProgressionRulesPath, "mastery level count does not match the implementation");

        foreach (var expected in CraftRules.MasteryLevels)
        {
            var level = levels.FirstOrDefault(item => item.GetProperty("level").GetInt32() == expected.Level);
            if (level.ValueKind == JsonValueKind.Undefined
                || !level.GetProperty("name").GetString()!.Equals(expected.DisplayName, StringComparison.Ordinal)
                || Math.Abs(level.GetProperty("requiredProgress").GetDouble() - expected.RequiredMasteryProgress) > 0.0000001
                || level.GetProperty("minimumRelevantExperienceYears").GetInt32() != expected.MinimumRelevantExperienceYears)
            {
                Fail(ProgressionRulesPath, $"level {expected.Level} does not match the implemented mastery rule");
            }
        }
    }

    private static void ValidateEconomyRules(string text)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        var incomeRoll = root.GetProperty("incomeRoll");
        var commission = root.GetProperty("majorCommission");
        var selfEmployment = root.GetProperty("selfEmployment");

        RequireDecimal(root, "educationCost", CraftRules.EducationCost, EconomyRulesPath);
        RequireDecimal(root, "monthlyIncomeBase", CraftRules.MonthlyIncomeBase, EconomyRulesPath);
        RequireInt(root, "monthsPerYear", 12, EconomyRulesPath);
        RequireInt(incomeRoll, "integerMinimum", 0, EconomyRulesPath);
        RequireInt(incomeRoll, "integerMaximumInclusive", 94, EconomyRulesPath);
        RequireInt(incomeRoll, "effectiveRollCap", 99, EconomyRulesPath);
        RequireInt(commission, "integerRollRequired", 94, EconomyRulesPath);
        RequireBool(selfEmployment, "subjectToRandomFiring", false, EconomyRulesPath);
        RequireBool(selfEmployment, "subjectToCareerVacancy", false, EconomyRulesPath);
        RequireBool(selfEmployment, "continuesAcrossHouseholdMoves", true, EconomyRulesPath);
        RequireBool(selfEmployment, "automaticRetirement", false, EconomyRulesPath);
    }

    private static void ValidateIncomeReference(string text)
    {
        var rows = NonEmptyLines(text).Skip(1).ToList();
        if (rows.Count != CraftRules.MasteryLevels.Count)
            Fail(IncomeReferencePath, $"expected {CraftRules.MasteryLevels.Count} income rows but found {rows.Count}");

        foreach (var row in rows)
        {
            var columns = row.Split(',');
            if (columns.Length < 10)
                Fail(IncomeReferencePath, "income rows must contain at least 10 columns");

            var level = ParseInt(columns[0], IncomeReferencePath);
            var rule = CraftRules.GetMasteryRule(level);
            var expectedAnnual = ParseDecimal(columns[6], IncomeReferencePath);
            var maximumMonthly = ParseDecimal(columns[9], IncomeReferencePath);
            var calculatedMaximum = Math.Round(
                CraftRules.CalculateMonthlyIncome(94.0, level),
                2,
                MidpointRounding.AwayFromZero);

            if (!columns[1].Equals(rule.DisplayName, StringComparison.Ordinal)
                || ParseInt(columns[2], IncomeReferencePath) != rule.MasteryBonus
                || Math.Abs(expectedAnnual - rule.ExpectedAnnualIncome) > 0.01m
                || Math.Abs(maximumMonthly - calculatedMaximum) > 0.01m)
            {
                Fail(IncomeReferencePath, $"income reference for level {level} does not match the implemented rule");
            }
        }
    }

    private static void ValidateCalibration(string text)
    {
        var rows = NonEmptyLines(text).Skip(1).ToList();
        if (rows.Count != 5)
            Fail(CalibrationPath, $"expected 5 calibration rows but found {rows.Count}");

        foreach (var row in rows)
        {
            var columns = row.Split(',');
            if (columns.Length < 5)
                Fail(CalibrationPath, "calibration rows must contain 5 columns");

            var stat = ParseInt(columns[0], CalibrationPath);
            var expectedGain = CraftRules.GetExperienceProgressGain(stat);
            var listedGain = ParseDouble(columns[1], CalibrationPath);
            if (Math.Abs(expectedGain - listedGain) > 0.0000001)
                Fail(CalibrationPath, $"experience progress for stat {stat} does not match the implementation");
        }
    }

    private static IEnumerable<string> NonEmptyLines(string text) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimStart('\uFEFF'));

    private static int ParseInt(string value, string path) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidDataException($"{path}: invalid integer '{value}'.");

    private static double ParseDouble(string value, string path) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidDataException($"{path}: invalid number '{value}'.");

    private static decimal ParseDecimal(string value, string path) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidDataException($"{path}: invalid decimal '{value}'.");

    private static void RequireInt(JsonElement element, string name, int expected, string path)
    {
        if (!element.TryGetProperty(name, out var property) || property.GetInt32() != expected)
            Fail(path, $"'{name}' must be {expected}");
    }

    private static void RequireDouble(JsonElement element, string name, double expected, string path)
    {
        if (!element.TryGetProperty(name, out var property)
            || Math.Abs(property.GetDouble() - expected) > 0.0000001)
        {
            Fail(path, $"'{name}' must be {expected.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    private static void RequireDecimal(JsonElement element, string name, decimal expected, string path)
    {
        if (!element.TryGetProperty(name, out var property)
            || Math.Abs(property.GetDecimal() - expected) > 0.0000001m)
        {
            Fail(path, $"'{name}' must be {expected.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    private static void RequireBool(JsonElement element, string name, bool expected, string path)
    {
        if (!element.TryGetProperty(name, out var property) || property.GetBoolean() != expected)
            Fail(path, $"'{name}' must be {expected.ToString().ToLowerInvariant()}");
    }

    private static void Fail(string path, string message) =>
        throw new InvalidDataException($"{path}: {message}.");
}
