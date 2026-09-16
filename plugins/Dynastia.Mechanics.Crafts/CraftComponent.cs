namespace Dynastia.Mechanics.Crafts;

public sealed class CraftComponent
{
    public List<string> CraftIds { get; set; } = [];

    public string? ActiveCraftOccupationId { get; set; }

    public Dictionary<string, int> CraftWorkYearsByCraft { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public decimal LastAnnualIncome { get; set; }

    public int LastIncomeYear { get; set; }
}
