using System.Globalization;
using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Family;

internal sealed class ForeignBirthplaceCatalog
{
    private const string CitiesPath = "Locations/foreign_cities.csv";
    private const string CountryWeightsPath = "Locations/nationality_country_weights.csv";
    private const string RulesPath = "Locations/foreign_birthplace_rules.json";
    private const string NationalitiesPath = "Nationalities/nationalities.csv";

    private readonly INationalityService _nationalities;
    private readonly IReadOnlyList<ForeignCityRow> _cities;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<CountryWeightRow>> _countryWeights;
    private readonly ForeignBirthplaceRules _rules;

    private ForeignBirthplaceCatalog(
        INationalityService nationalities,
        IReadOnlyList<ForeignCityRow> cities,
        IReadOnlyDictionary<string, IReadOnlyList<CountryWeightRow>> countryWeights,
        ForeignBirthplaceRules rules)
    {
        _nationalities = nationalities;
        _cities = cities;
        _countryWeights = countryWeights;
        _rules = rules;
    }

    public static ForeignBirthplaceCatalog Load(
        IGameDataService data,
        INationalityService nationalities)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(nationalities);

        var cities = ParseCities(data.ReadText(CitiesPath));
        var weights = ParseCountryWeights(data.ReadText(CountryWeightsPath));
        var rules = JsonSerializer.Deserialize<ForeignBirthplaceRules>(
            data.ReadText(RulesPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw CatalogValidation.Error(RulesPath, "valid foreign birthplace rules");

        Validate(cities, weights, rules, data, nationalities);

        return new ForeignBirthplaceCatalog(
            nationalities,
            cities,
            weights
                .GroupBy(row => row.NationalityId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<CountryWeightRow>)group.ToArray(),
                    StringComparer.OrdinalIgnoreCase),
            rules);
    }

    public ForeignBirthplaceInfo? Select(
        TownInfo originTown,
        string nationalityId,
        int birthYear,
        int year,
        IGameRandom random)
    {
        if (nationalityId.Equals(
                _rules.PolishNationalityId,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!_countryWeights.TryGetValue(nationalityId, out var configuredWeights))
            return null;

        var distribution = _nationalities.ResolveDistribution(originTown.RegionId, year);
        var localShare = distribution.TryGetValue(nationalityId, out var share)
            ? share
            : 0.0;
        var chance = _rules.ForeignBornChanceByLocalNationalityShare
            .OrderByDescending(rule => rule.MinimumLocalSharePercent)
            .First(rule => localShare >= rule.MinimumLocalSharePercent)
            .Chance;

        if (!random.Chance(chance))
            return null;

        var activeWeights = configuredWeights
            .Where(row => birthYear >= row.YearFrom && birthYear <= row.YearTo)
            .ToArray();
        if (activeWeights.Length == 0)
            return null;

        var selectedCountry = WeightedCountry(activeWeights, random);
        var activeCities = _cities
            .Where(city =>
                city.Country.Equals(selectedCountry, StringComparison.OrdinalIgnoreCase)
                && birthYear >= city.YearFrom
                && birthYear <= city.YearTo)
            .ToArray();
        if (activeCities.Length == 0)
            return null;

        var city = activeCities[random.NextInt(0, activeCities.Length - 1)];
        return new ForeignBirthplaceInfo(city.City, city.Country);
    }

    private static string WeightedCountry(
        IReadOnlyList<CountryWeightRow> rows,
        IGameRandom random)
    {
        var total = rows.Sum(row => row.Weight);
        var roll = random.NextDouble() * total;
        foreach (var row in rows)
        {
            roll -= row.Weight;
            if (roll < 0)
                return row.Country;
        }
        return rows[^1].Country;
    }

    private static IReadOnlyList<ForeignCityRow> ParseCities(string text)
    {
        var lines = Lines(text);
        ExpectHeader(lines, CitiesPath, "City,Country,YearFrom,YearTo");
        var rows = new List<ForeignCityRow>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            if (fields.Length != 4)
                throw CatalogValidation.FieldCount(CitiesPath, index + 1, fields.Length, 4);
            var from = CatalogValidation.ParseInt(CitiesPath, index + 1, "YearFrom", fields[2]);
            var to = CatalogValidation.ParseInt(CitiesPath, index + 1, "YearTo", fields[3]);
            if (to < from)
                throw CatalogValidation.Error(CitiesPath, "YearTo >= YearFrom", index + 1);
            rows.Add(new ForeignCityRow(fields[0].Trim(), fields[1].Trim(), from, to));
        }
        return rows;
    }

    private static IReadOnlyList<CountryWeightRow> ParseCountryWeights(string text)
    {
        var lines = Lines(text);
        ExpectHeader(lines, CountryWeightsPath, "NationalityId,Country,YearFrom,YearTo,Weight");
        var rows = new List<CountryWeightRow>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            if (fields.Length != 5)
                throw CatalogValidation.FieldCount(CountryWeightsPath, index + 1, fields.Length, 5);
            var from = CatalogValidation.ParseInt(CountryWeightsPath, index + 1, "YearFrom", fields[2]);
            var to = CatalogValidation.ParseInt(CountryWeightsPath, index + 1, "YearTo", fields[3]);
            var weight = CatalogValidation.ParseDouble(CountryWeightsPath, index + 1, "Weight", fields[4]);
            if (to < from || weight <= 0)
                throw CatalogValidation.Error(CountryWeightsPath, "YearTo >= YearFrom and Weight > 0", index + 1);
            rows.Add(new CountryWeightRow(fields[0].Trim(), fields[1].Trim(), from, to, weight));
        }
        return rows;
    }

    private static void Validate(
        IReadOnlyList<ForeignCityRow> cities,
        IReadOnlyList<CountryWeightRow> weights,
        ForeignBirthplaceRules rules,
        IGameDataService data,
        INationalityService nationalities)
    {
        var duplicateCity = cities
            .GroupBy(row => $"{row.City}\u001f{row.Country}\u001f{row.YearFrom}\u001f{row.YearTo}", StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateCity is not null)
            throw CatalogValidation.Error(CitiesPath, "unique City/Country/year windows", value: duplicateCity.Key);

        foreach (var row in weights)
        {
            try { _ = nationalities.GetDisplayName(row.NationalityId); }
            catch (Exception exception)
            {
                throw new InvalidDataException($"{CountryWeightsPath}: unknown NationalityId '{row.NationalityId}'.", exception);
            }

            var overlapsCity = cities.Any(city =>
                city.Country.Equals(row.Country, StringComparison.OrdinalIgnoreCase)
                && Math.Max(city.YearFrom, row.YearFrom) <= Math.Min(city.YearTo, row.YearTo));
            if (!overlapsCity)
                throw CatalogValidation.Error(CountryWeightsPath, "a mapped country with an active city in an overlapping year", value: row.Country);
        }

        var mapped = weights.Select(row => row.NationalityId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nationalityLines = Lines(data.ReadText(NationalitiesPath));
        for (var index = 1; index < nationalityLines.Length; index++)
        {
            var fields = nationalityLines[index].Split(',', 5);
            if (fields.Length < 4 || fields[3].Trim() != "1")
                continue;
            var id = fields[0].Trim();
            if (!id.Equals(rules.PolishNationalityId, StringComparison.OrdinalIgnoreCase)
                && !mapped.Contains(id))
            {
                throw CatalogValidation.Error(CountryWeightsPath, "a country mapping for every enabled non-Polish nationality", value: id);
            }
        }

        if (rules.ForeignBornChanceByLocalNationalityShare.Count == 0
            || rules.ForeignBornChanceByLocalNationalityShare.Any(rule => rule.Chance < 0 || rule.Chance > 1))
        {
            throw CatalogValidation.Error(RulesPath, "foreign-born chances between 0 and 1");
        }
    }

    private static string[] Lines(string text) =>
        text.Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static void ExpectHeader(string[] lines, string path, string expected)
    {
        var actual = lines.FirstOrDefault()?.TrimStart('\uFEFF');
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw CatalogValidation.UnexpectedHeader(path, actual, expected);
    }

    private sealed record ForeignCityRow(string City, string Country, int YearFrom, int YearTo);
    private sealed record CountryWeightRow(string NationalityId, string Country, int YearFrom, int YearTo, double Weight);

    private sealed class ForeignBirthplaceRules
    {
        public string PolishNationalityId { get; set; } = "polish";
        public List<ForeignChanceRule> ForeignBornChanceByLocalNationalityShare { get; set; } = [];
    }

    private sealed class ForeignChanceRule
    {
        public double MinimumLocalSharePercent { get; set; }
        public double Chance { get; set; }
    }
}

internal sealed record ForeignBirthplaceInfo(string City, string Country);
