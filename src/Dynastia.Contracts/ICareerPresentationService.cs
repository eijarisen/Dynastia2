namespace Dynastia.Contracts;

public interface ICareerPresentationService
{
    string GetCareerEmoji(string? careerId);

    string GetOccupationEmoji(IPerson person);
}
