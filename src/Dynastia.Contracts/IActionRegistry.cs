namespace Dynastia.Contracts;

public interface IActionRegistry
{
    void Register(GameActionDefinition action);

    IReadOnlyList<GameActionDefinition> GetAvailableActions(
        IPerson actor,
        IPerson target);

    GameActionResult Execute(
        string actionId,
        IPerson actor,
        IPerson target);
}
