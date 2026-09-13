namespace Dynastia.Contracts;

public interface ITownCareerOpportunityService
{
    LocationOpportunitySnapshot GetOpportunitySnapshot(TownInfo town);
}
