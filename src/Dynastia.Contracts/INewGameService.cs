namespace Dynastia.Contracts;

public interface INewGameService
{
    IPerson StartNewGame(
        string dynastySurname,
        int startYear = GameCalendarConfiguration.GameStartYear);
}
