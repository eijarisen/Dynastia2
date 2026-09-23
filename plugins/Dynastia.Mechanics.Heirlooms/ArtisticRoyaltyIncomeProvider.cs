using Dynastia.Contracts;

namespace Dynastia.Mechanics.Heirlooms;

internal sealed class ArtisticRoyaltyIncomeProvider : IHouseholdIncomeProvider
{
    private readonly IGameState _gameState;
    private readonly IHeirloomService _heirlooms;
    private readonly int _afterAuthorDeathYears;

    public ArtisticRoyaltyIncomeProvider(
        IGameState gameState,
        IHeirloomService heirlooms,
        int afterAuthorDeathYears)
    {
        _gameState = gameState;
        _heirlooms = heirlooms;
        _afterAuthorDeathYears = afterAuthorDeathYears;
    }

    public string Id => "royalties";
    public string Label => "royalties";

    public decimal GetAnnualIncome(IPerson householdRepresentative) =>
        Calculate(householdRepresentative);

    public decimal GetExpectedAnnualIncome(IPerson householdRepresentative) =>
        Calculate(householdRepresentative);

    public decimal GetExpectedPassiveAnnualIncome(IPerson householdRepresentative) =>
        Calculate(householdRepresentative);

    private decimal Calculate(IPerson householdRepresentative)
    {
        decimal total = 0m;
        foreach (var heirloom in _heirlooms.GetHeirlooms(householdRepresentative))
        {
            if (heirloom.RoyaltyAnnualRate <= 0m
                || heirloom.RoyaltyAuthorId is not Guid authorId)
            {
                continue;
            }

            var author = _gameState.People.FirstOrDefault(person => person.Id == authorId);
            if (author is null || !IsRoyaltyEligible(author))
                continue;

            total += heirloom.AppraisedValue * heirloom.RoyaltyAnnualRate;
        }

        return total;
    }

    private bool IsRoyaltyEligible(IPerson author)
    {
        if (author.Tags.Has("state.alive"))
            return true;

        return author.DeathDate is GameDate death
            && _gameState.Year <= death.Year + _afterAuthorDeathYears;
    }
}
