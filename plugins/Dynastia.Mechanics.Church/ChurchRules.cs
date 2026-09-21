using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Church;

internal sealed class ChurchRules
{
    private const string DataPath = "LocalSociety/church_rules.json";

    private ChurchRules(
        AttendRule attend,
        IReadOnlyDictionary<string, DonationTierRule> churchDonationTiers,
        IReadOnlyDictionary<string, DonationTierRule> poorFamilyTiers,
        int roundMoneyTo,
        WelfareRule welfare)
    {
        Attend = attend;
        ChurchDonationTiers = churchDonationTiers;
        PoorFamilyTiers = poorFamilyTiers;
        RoundMoneyTo = roundMoneyTo;
        Welfare = welfare;
    }

    public AttendRule Attend { get; }
    public IReadOnlyDictionary<string, DonationTierRule> ChurchDonationTiers { get; }
    public IReadOnlyDictionary<string, DonationTierRule> PoorFamilyTiers { get; }
    public int RoundMoneyTo { get; }
    public WelfareRule Welfare { get; }

    public static ChurchRules Load(IGameDataService data)
    {
        var root = JsonSerializer.Deserialize<Root>(
            data.ReadText(DataPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"{DataPath}: invalid JSON root.");

        var donate = ConvertTiers(root.Actions.DonateChurch.Amounts, "donateChurch.amounts");
        var aid = ConvertTiers(root.Actions.AidPoorFamily.Amounts, "aidPoorFamily.amounts");
        if (root.Actions.DonateChurch.RoundMoneyTo <= 0
            || root.Actions.AidPoorFamily.RoundMoneyTo != root.Actions.DonateChurch.RoundMoneyTo)
        {
            throw new InvalidDataException($"{DataPath}: Church money rounding must be positive and consistent.");
        }

        var welfare = root.Actions.Welfare;
        if (welfare.RequiresHouseholdWealthAtMost != 0
            || welfare.RequiresHouseholdReputationAtLeast != -10
            || !welfare.OncePerHouseholdPerYear
            || welfare.RoundMoneyTo != root.Actions.DonateChurch.RoundMoneyTo
            || !welfare.GuaranteedIfEligible
            || welfare.StatusPenalty != 0)
        {
            throw new InvalidDataException($"{DataPath}: unsupported welfare eligibility configuration.");
        }

        return new ChurchRules(
            new AttendRule(
                root.Actions.Attend.Renown,
                root.Actions.Attend.Reputation,
                root.Actions.Attend.MoralsImproveChance,
                root.Actions.Attend.GoodMoralsProtectionChance),
            donate,
            aid,
            root.Actions.DonateChurch.RoundMoneyTo,
            new WelfareRule(
                welfare.RequiresHouseholdWealthAtMost,
                welfare.RequiresHouseholdReputationAtLeast,
                welfare.OncePerHouseholdPerYear,
                welfare.GuaranteedIfEligible,
                welfare.StatusPenalty));
    }

    public decimal CalculateDonationAmount(
        IReadOnlyDictionary<string, DonationTierRule> tiers,
        string tierId,
        decimal annualExpenses)
    {
        if (!tiers.TryGetValue(tierId, out var tier))
            throw new ArgumentOutOfRangeException(nameof(tierId), tierId, "Unknown Church donation tier.");

        var raw = Math.Max(tier.Minimum, annualExpenses * tier.ExpenseFraction);
        return RoundMoney(raw);
    }

    public bool IsWelfareEligible(decimal wealth, double householdReputation) =>
        wealth <= Welfare.MaximumWealth
        && householdReputation >= Welfare.MinimumReputation;

    public decimal CalculateWelfareAmount(decimal annualExpenses, int churchTier)
    {
        var factor = 0.08m + 0.02m * Math.Clamp(churchTier, 1, 5);
        var raw = Math.Clamp(annualExpenses * factor, 300m, 1500m);
        return RoundMoney(raw);
    }

    private decimal RoundMoney(decimal amount) =>
        Math.Round(
            amount / RoundMoneyTo,
            0,
            MidpointRounding.AwayFromZero) * RoundMoneyTo;

    private static IReadOnlyDictionary<string, DonationTierRule> ConvertTiers(
        Dictionary<string, DonationTierDto> source,
        string field)
    {
        var expected = new[] { "modest", "generous", "major" };
        var result = new Dictionary<string, DonationTierRule>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in expected)
        {
            if (!source.TryGetValue(id, out var dto)
                || dto.ExpenseFraction <= 0
                || dto.Minimum <= 0
                || dto.MoralsImproveChance is < 0 or > 1)
            {
                throw new InvalidDataException($"{DataPath}: invalid {field}.{id}.");
            }

            result[id] = new DonationTierRule(
                dto.ExpenseFraction,
                dto.Minimum,
                dto.Renown,
                dto.Reputation,
                dto.MoralsImproveChance);
        }

        return result;
    }

    internal sealed record AttendRule(
        double Renown,
        double Reputation,
        double MoralsImproveChance,
        double GoodMoralsProtectionChance);

    internal sealed record DonationTierRule(
        decimal ExpenseFraction,
        decimal Minimum,
        double Renown,
        double Reputation,
        double MoralsImproveChance);

    internal sealed record WelfareRule(
        decimal MaximumWealth,
        double MinimumReputation,
        bool OncePerHouseholdPerYear,
        bool GuaranteedIfEligible,
        double StatusPenalty);

    private sealed class Root
    {
        public ActionsDto Actions { get; set; } = new();
    }

    private sealed class ActionsDto
    {
        public AttendDto Attend { get; set; } = new();
        public DonationActionDto DonateChurch { get; set; } = new();
        public DonationActionDto AidPoorFamily { get; set; } = new();
        public WelfareDto Welfare { get; set; } = new();
    }

    private sealed class AttendDto
    {
        public double Renown { get; set; }
        public double Reputation { get; set; }
        public double MoralsImproveChance { get; set; }
        public double GoodMoralsProtectionChance { get; set; }
    }

    private sealed class DonationActionDto
    {
        public Dictionary<string, DonationTierDto> Amounts { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
        public int RoundMoneyTo { get; set; }
    }

    private sealed class DonationTierDto
    {
        public decimal ExpenseFraction { get; set; }
        public decimal Minimum { get; set; }
        public double Renown { get; set; }
        public double Reputation { get; set; }
        public double MoralsImproveChance { get; set; }
    }

    private sealed class WelfareDto
    {
        public decimal RequiresHouseholdWealthAtMost { get; set; }
        public double RequiresHouseholdReputationAtLeast { get; set; }
        public bool OncePerHouseholdPerYear { get; set; }
        public int RoundMoneyTo { get; set; }
        public bool GuaranteedIfEligible { get; set; }
        public double StatusPenalty { get; set; }
    }
}
