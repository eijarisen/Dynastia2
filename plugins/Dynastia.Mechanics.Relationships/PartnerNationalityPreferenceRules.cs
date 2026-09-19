using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public static class PartnerNationalityPreferenceRules
{
    private const string PolishNationalityId = "polish";
    private const double ForeignCandidateMultiplier = 0.5;

    public static IReadOnlyDictionary<string, double> AdjustDistribution(
        IReadOnlyDictionary<string, double> source,
        string seekerNationalityId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(seekerNationalityId);

        // Regional nationality data remains authoritative. This is a social
        // matching preference applied only while building spouse-candidate
        // pools for Polish seekers: the combined foreign share is halved and
        // the displaced probability is added to Polish candidates. Relative
        // proportions among foreign nationalities are preserved exactly.
        if (!seekerNationalityId.Equals(
                PolishNationalityId,
                StringComparison.OrdinalIgnoreCase))
        {
            return source;
        }

        var foreignTotal = source
            .Where(pair => !pair.Key.Equals(
                PolishNationalityId,
                StringComparison.OrdinalIgnoreCase))
            .Sum(pair => Math.Max(0.0, pair.Value));

        if (foreignTotal <= 0.0)
            return source;

        var adjusted = new Dictionary<string, double>(
            StringComparer.OrdinalIgnoreCase);
        var polishSeen = false;

        foreach (var pair in source)
        {
            if (pair.Key.Equals(
                    PolishNationalityId,
                    StringComparison.OrdinalIgnoreCase))
            {
                polishSeen = true;
                adjusted[pair.Key] =
                    Math.Max(0.0, pair.Value)
                    + foreignTotal * (1.0 - ForeignCandidateMultiplier);
            }
            else
            {
                adjusted[pair.Key] =
                    Math.Max(0.0, pair.Value)
                    * ForeignCandidateMultiplier;
            }
        }

        if (!polishSeen)
        {
            adjusted[PolishNationalityId] =
                foreignTotal * (1.0 - ForeignCandidateMultiplier);
        }

        return adjusted;
    }

    public static string ChooseNationality(
        IReadOnlyDictionary<string, double> source,
        string seekerNationalityId,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var distribution = AdjustDistribution(
            source,
            seekerNationalityId);
        var total = distribution.Sum(pair => pair.Value);

        if (total <= 0.0)
        {
            throw new InvalidOperationException(
                "Partner nationality distribution has zero total weight.");
        }

        var roll = random.NextDouble() * total;
        string? last = null;

        foreach (var pair in distribution)
        {
            if (pair.Value <= 0.0)
                continue;

            last = pair.Key;
            if (roll < pair.Value)
                return pair.Key;

            roll -= pair.Value;
        }

        return last
            ?? throw new InvalidOperationException(
                "Partner nationality distribution has no selectable entries.");
    }
}
