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
        const string header = "Level,DisplayName,RequiredMasteryProgress,MinimumRelevantExperienceYears";
        var lines = NonEmptyLines(text).ToList();
        ValidateHeader(lines, MasteryLevelsPath, header);
        var rows = lines.Skip(1).ToList();
        if (rows.Count != CraftRules.MasteryLevels.Count)
        {
            throw CatalogValidation.Error(
                MasteryLevelsPath,
                $"exactly {CraftRules.MasteryLevels.Count} mastery rows",
                field: "RowCount",
                value: rows.Count);
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var columns = rows[index].Split(',');
            var row = index + 2;
            if (columns.Length != 4)
                throw CatalogValidation.FieldCount(MasteryLevelsPath, row, columns.Length, 4);

            var expected = CraftRules.MasteryLevels[index];
            var level = CatalogValidation.ParseInt(MasteryLevelsPath, row, "Level", columns[0]);
            var progress = CatalogValidation.ParseDouble(MasteryLevelsPath, row, "RequiredMasteryProgress", columns[2]);
            var years = CatalogValidation.ParseInt(MasteryLevelsPath, row, "MinimumRelevantExperienceYears", columns[3]);

            RequireEqual(MasteryLevelsPath, row, level.ToString(CultureInfo.InvariantCulture), expected.Level.ToString(CultureInfo.InvariantCulture), "Level", expected.Level);
            RequireEqual(MasteryLevelsPath, row, columns[1], expected.DisplayName, "DisplayName", expected.DisplayName);
            if (Math.Abs(progress - expected.RequiredMasteryProgress) > 0.0000001)
                throw CatalogValidation.Error(MasteryLevelsPath, expected.RequiredMasteryProgress.ToString(CultureInfo.InvariantCulture), row, expected.DisplayName, "RequiredMasteryProgress", progress);
            RequireEqual(MasteryLevelsPath, row, years.ToString(CultureInfo.InvariantCulture), expected.MinimumRelevantExperienceYears.ToString(CultureInfo.InvariantCulture), "MinimumRelevantExperienceYears", expected.MinimumRelevantExperienceYears);
        }
    }

    private static void ValidateEducationRules(string text)
    {
        using var document = ParseDocument(text, EducationRulesPath);
        var root = RequireObjectRoot(document, EducationRulesPath);
        var educationWindow = RequireObject(root, "educationWindow", EducationRulesPath);
        var slots = RequireObject(root, "slots", EducationRulesPath);
        var improvement = RequireObject(root, "existingCraftImprovement", EducationRulesPath);
        var unlock = RequireObject(root, "newCraft", EducationRulesPath);

        RequireDecimal(educationWindow, "priceForEveryOption", CraftRules.EducationCost, EducationRulesPath);
        RequireInt(slots, "maximumCrafts", CraftRules.MaximumCrafts, EducationRulesPath);
        RequireDouble(improvement, "successfulProgressGain", 3.0, EducationRulesPath);
        RequireDouble(unlock, "educationProgressGranted", 0.0, EducationRulesPath);
        RequireBool(educationWindow, "standardEducationFirst", true, EducationRulesPath);
        RequireBool(educationWindow, "unknownCraftsVisibleOnlyWhenChosenSlotEmpty", true, EducationRulesPath);
        RequireBool(educationWindow, "knownCraftsVisibleForImprovement", true, EducationRulesPath);
        RequireBool(educationWindow, "masterCraftsDisabled", true, EducationRulesPath);
        RequireBool(RequireObject(slots, "inheritedSlot", EducationRulesPath), "replaceable", false, EducationRulesPath);
        RequireBool(RequireObject(slots, "chosenSlot", EducationRulesPath), "replaceable", false, EducationRulesPath);
    }

    private static void ValidateProgressionRules(string text)
    {
        using var document = ParseDocument(text, ProgressionRulesPath);
        var root = RequireObjectRoot(document, ProgressionRulesPath);
        var experience = RequireObject(root, "experience", ProgressionRulesPath);
        var education = RequireObject(root, "education", ProgressionRulesPath);
        var retirement = RequireObject(root, "retirement", ProgressionRulesPath);

        RequireInt(experience, "maximumCreditPerCalendarYearPerCraft", 1, ProgressionRulesPath);
        RequireDecimal(education, "cost", CraftRules.EducationCost, ProgressionRulesPath);
        RequireDouble(education, "successfulImprovementProgress", 3.0, ProgressionRulesPath);
        RequireBool(root, "automaticLeveling", true, ProgressionRulesPath);
        RequireBool(retirement, "automaticRetirement", false, ProgressionRulesPath);

        var levelsProperty = RequireProperty(root, "levels", ProgressionRulesPath);
        if (levelsProperty.ValueKind != JsonValueKind.Array)
        {
            throw CatalogValidation.Error(
                ProgressionRulesPath,
                "a JSON array",
                field: "levels",
                value: levelsProperty.ValueKind);
        }

        var levels = levelsProperty.EnumerateArray().ToList();
        if (levels.Count != CraftRules.MasteryLevels.Count)
        {
            throw CatalogValidation.Error(
                ProgressionRulesPath,
                $"exactly {CraftRules.MasteryLevels.Count} mastery level objects",
                field: "levels",
                value: levels.Count);
        }

        foreach (var expected in CraftRules.MasteryLevels)
        {
            var level = levels.FirstOrDefault(item =>
                TryGetInt(item, "level", out var value) && value == expected.Level);
            if (level.ValueKind == JsonValueKind.Undefined)
            {
                throw CatalogValidation.Error(
                    ProgressionRulesPath,
                    $"an object for mastery level {expected.Level}",
                    item: expected.DisplayName,
                    field: "levels",
                    value: "<missing>");
            }

            RequireString(level, "name", expected.DisplayName, ProgressionRulesPath, expected.DisplayName);
            RequireDouble(level, "requiredProgress", expected.RequiredMasteryProgress, ProgressionRulesPath, expected.DisplayName);
            RequireInt(level, "minimumRelevantExperienceYears", expected.MinimumRelevantExperienceYears, ProgressionRulesPath, expected.DisplayName);
        }
    }

    private static void ValidateEconomyRules(string text)
    {
        using var document = ParseDocument(text, EconomyRulesPath);
        var root = RequireObjectRoot(document, EconomyRulesPath);
        var baseSalary = RequireObject(root, "baseSalary", EconomyRulesPath);
        var incomeRoll = RequireObject(root, "incomeRoll", EconomyRulesPath);
        var commission = RequireObject(root, "majorCommission", EconomyRulesPath);
        var selfEmployment = RequireObject(root, "selfEmployment", EconomyRulesPath);

        RequireDecimal(root, "educationCost", CraftRules.EducationCost, EconomyRulesPath);
        RequireString(baseSalary, "source", "Crafts/crafts.csv#BaseSalary", EconomyRulesPath);
        RequireInt(baseSalary, "minimum", 400, EconomyRulesPath);
        RequireInt(baseSalary, "maximum", 800, EconomyRulesPath);
        RequireInt(incomeRoll, "integerMinimum", 0, EconomyRulesPath);
        RequireInt(incomeRoll, "integerMaximumInclusive", 94, EconomyRulesPath);
        RequireInt(incomeRoll, "denominatorMaximum", 99, EconomyRulesPath);
        RequireString(root, "incomeFormula", "yearlyIncome = round(baseSalary * 100 / min(99, (100 - randomRoll - masteryLevel)))", EconomyRulesPath);
        RequireInt(commission, "integerRollRequired", 94, EconomyRulesPath);
        RequireInt(commission, "minimumMasteryLevel", 5, EconomyRulesPath);
        RequireBool(selfEmployment, "subjectToRandomFiring", false, EconomyRulesPath);
        RequireBool(selfEmployment, "subjectToCareerVacancy", false, EconomyRulesPath);
        RequireBool(selfEmployment, "continuesAcrossHouseholdMoves", true, EconomyRulesPath);
        RequireBool(selfEmployment, "automaticRetirement", false, EconomyRulesPath);
    }

    private static void ValidateIncomeReference(string text)
    {
        const string header = "Level,DisplayName,ExpectedIncomeMultiplier,MedianIncomeMultiplier,MaximumIncomeMultiplier,RollRange";
        var lines = NonEmptyLines(text).ToList();
        ValidateHeader(lines, IncomeReferencePath, header);
        var rows = lines.Skip(1).ToList();
        if (rows.Count != CraftRules.MasteryLevels.Count)
        {
            throw CatalogValidation.Error(
                IncomeReferencePath,
                $"exactly {CraftRules.MasteryLevels.Count} income rows",
                field: "RowCount",
                value: rows.Count);
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var columns = rows[index].Split(',');
            var row = index + 2;
            if (columns.Length != 6)
                throw CatalogValidation.FieldCount(IncomeReferencePath, row, columns.Length, 6);

            var level = CatalogValidation.ParseInt(IncomeReferencePath, row, "Level", columns[0]);
            var rule = CraftRules.GetMasteryRule(level);
            var expectedMultiplier = CatalogValidation.ParseDecimal(IncomeReferencePath, row, "ExpectedIncomeMultiplier", columns[2]);
            var medianMultiplier = CatalogValidation.ParseDecimal(IncomeReferencePath, row, "MedianIncomeMultiplier", columns[3]);
            var maximumMultiplier = CatalogValidation.ParseDecimal(IncomeReferencePath, row, "MaximumIncomeMultiplier", columns[4]);

            if (!columns[1].Equals(rule.DisplayName, StringComparison.Ordinal))
                throw CatalogValidation.Error(IncomeReferencePath, rule.DisplayName, row, rule.DisplayName, "DisplayName", columns[1]);
            if (Math.Abs(expectedMultiplier - CraftRules.GetExpectedIncomeMultiplier(level)) > 0.000001m)
                throw CatalogValidation.Error(IncomeReferencePath, CraftRules.GetExpectedIncomeMultiplier(level).ToString("0.######", CultureInfo.InvariantCulture), row, rule.DisplayName, "ExpectedIncomeMultiplier", expectedMultiplier);
            if (Math.Abs(medianMultiplier - CraftRules.GetMedianIncomeMultiplier(level)) > 0.000001m)
                throw CatalogValidation.Error(IncomeReferencePath, CraftRules.GetMedianIncomeMultiplier(level).ToString("0.######", CultureInfo.InvariantCulture), row, rule.DisplayName, "MedianIncomeMultiplier", medianMultiplier);
            if (Math.Abs(maximumMultiplier - CraftRules.GetMaximumIncomeMultiplier(level)) > 0.000001m)
                throw CatalogValidation.Error(IncomeReferencePath, CraftRules.GetMaximumIncomeMultiplier(level).ToString("0.######", CultureInfo.InvariantCulture), row, rule.DisplayName, "MaximumIncomeMultiplier", maximumMultiplier);
            if (!columns[5].Equals("0-94", StringComparison.Ordinal))
                throw CatalogValidation.Error(IncomeReferencePath, "0-94", row, rule.DisplayName, "RollRange", columns[5]);
        }
    }

    private static void ValidateCalibration(string text)
    {
        const string header = "PrimaryStat,ProgressPerRelevantWorkYear,EducationSuccessByCurrentLevel_Novice_Apprentice_Adept_Expert,ApproxWorkOnlyYearsToMaster,ApproxYearsToMaster_WithRelevantWorkAndAnnualEducation";
        var lines = NonEmptyLines(text).ToList();
        ValidateHeader(lines, CalibrationPath, header);
        var rows = lines.Skip(1).ToList();
        if (rows.Count != 5)
        {
            throw CatalogValidation.Error(
                CalibrationPath,
                "exactly 5 calibration rows",
                field: "RowCount",
                value: rows.Count);
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var columns = rows[index].Split(',');
            var row = index + 2;
            if (columns.Length < 5)
            {
                throw CatalogValidation.Error(
                    CalibrationPath,
                    "at least 5 columns",
                    row,
                    field: "FieldCount",
                    value: columns.Length);
            }

            var stat = CatalogValidation.ParseInt(CalibrationPath, row, "PrimaryStat", columns[0]);
            var expectedGain = CraftRules.GetExperienceProgressGain(stat);
            var listedGain = CatalogValidation.ParseDouble(CalibrationPath, row, "ProgressPerRelevantWorkYear", columns[1]);
            if (Math.Abs(expectedGain - listedGain) > 0.0000001)
            {
                throw CatalogValidation.Error(
                    CalibrationPath,
                    expectedGain.ToString(CultureInfo.InvariantCulture),
                    row,
                    item: $"PrimaryStat {stat}",
                    field: "ProgressPerRelevantWorkYear",
                    value: listedGain);
            }
        }
    }

    private static IEnumerable<string> NonEmptyLines(string text) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimStart('\uFEFF'));

    private static void ValidateHeader(
        IReadOnlyList<string> lines,
        string path,
        string expected)
    {
        if (lines.Count < 2
            || !lines[0].Equals(expected, StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                path,
                lines.Count == 0 ? null : lines[0],
                expected);
        }
    }

    private static JsonDocument ParseDocument(string text, string path)
    {
        try
        {
            return JsonDocument.Parse(text);
        }
        catch (JsonException exception)
        {
            var row = exception.LineNumber is long lineNumber && lineNumber < int.MaxValue
                ? (int)lineNumber + 1
                : (int?)null;
            throw new InvalidDataException(
                $"{path}" +
                (row is int sourceRow ? $" row {sourceRow}" : string.Empty) +
                $" field '{exception.Path ?? "$"}': invalid JSON; expected a valid configuration document. {exception.Message}",
                exception);
        }
    }

    private static JsonElement RequireObjectRoot(JsonDocument document, string path)
    {
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw CatalogValidation.Error(
                path,
                "a JSON object",
                field: "Root",
                value: document.RootElement.ValueKind);
        }
        return document.RootElement;
    }

    private static JsonElement RequireObject(JsonElement element, string name, string path)
    {
        var property = RequireProperty(element, name, path);
        if (property.ValueKind != JsonValueKind.Object)
        {
            throw CatalogValidation.Error(
                path,
                "a JSON object",
                field: name,
                value: property.ValueKind);
        }
        return property;
    }

    private static JsonElement RequireProperty(JsonElement element, string name, string path)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            throw CatalogValidation.Error(
                path,
                "a required property",
                field: name,
                value: null);
        }
        return property;
    }

    private static bool TryGetInt(JsonElement element, string name, out int value)
    {
        value = default;
        return element.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value);
    }

    private static void RequireInt(
        JsonElement element,
        string name,
        int expected,
        string path,
        string? item = null)
    {
        if (!element.TryGetProperty(name, out var property)
            || property.ValueKind != JsonValueKind.Number
            || !property.TryGetInt32(out var actual)
            || actual != expected)
        {
            throw CatalogValidation.Error(
                path,
                expected.ToString(CultureInfo.InvariantCulture),
                item: item,
                field: name,
                value: element.TryGetProperty(name, out property) ? JsonValue(property) : null);
        }
    }

    private static void RequireDouble(
        JsonElement element,
        string name,
        double expected,
        string path,
        string? item = null)
    {
        if (!element.TryGetProperty(name, out var property)
            || property.ValueKind != JsonValueKind.Number
            || !property.TryGetDouble(out var actual)
            || Math.Abs(actual - expected) > 0.0000001)
        {
            throw CatalogValidation.Error(
                path,
                expected.ToString(CultureInfo.InvariantCulture),
                item: item,
                field: name,
                value: element.TryGetProperty(name, out property) ? JsonValue(property) : null);
        }
    }

    private static void RequireDecimal(
        JsonElement element,
        string name,
        decimal expected,
        string path,
        string? item = null)
    {
        if (!element.TryGetProperty(name, out var property)
            || property.ValueKind != JsonValueKind.Number
            || !property.TryGetDecimal(out var actual)
            || Math.Abs(actual - expected) > 0.0000001m)
        {
            throw CatalogValidation.Error(
                path,
                expected.ToString(CultureInfo.InvariantCulture),
                item: item,
                field: name,
                value: element.TryGetProperty(name, out property) ? JsonValue(property) : null);
        }
    }

    private static void RequireBool(
        JsonElement element,
        string name,
        bool expected,
        string path,
        string? item = null)
    {
        if (!element.TryGetProperty(name, out var property)
            || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || property.GetBoolean() != expected)
        {
            throw CatalogValidation.Error(
                path,
                expected.ToString().ToLowerInvariant(),
                item: item,
                field: name,
                value: element.TryGetProperty(name, out property) ? JsonValue(property) : null);
        }
    }

    private static void RequireString(
        JsonElement element,
        string name,
        string expected,
        string path,
        string? item = null)
    {
        if (!element.TryGetProperty(name, out var property)
            || property.ValueKind != JsonValueKind.String
            || !string.Equals(property.GetString(), expected, StringComparison.Ordinal))
        {
            throw CatalogValidation.Error(
                path,
                expected,
                item: item,
                field: name,
                value: element.TryGetProperty(name, out property) ? JsonValue(property) : null);
        }
    }

    private static object? JsonValue(JsonElement property) =>
        property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => property.ValueKind.ToString()
        };

    private static void RequireEqual(
        string path,
        int row,
        string actual,
        string expected,
        string field,
        object expectedValue)
    {
        if (!actual.Equals(expected, StringComparison.Ordinal))
        {
            throw CatalogValidation.Error(
                path,
                Convert.ToString(expectedValue, CultureInfo.InvariantCulture) ?? expected,
                row,
                field: field,
                value: actual);
        }
    }
}
