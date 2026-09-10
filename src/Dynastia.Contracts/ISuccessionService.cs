namespace Dynastia.Contracts;

public interface ISuccessionService
{
    bool IsGameOver { get; }
    int? MaleLineEndedYear { get; }
    Guid? ActiveControllerId { get; }
    IPerson? ActiveController { get; }
    bool IsControllable(IPerson person);
    bool HasLivingMaleLineage { get; }
    bool SetActiveController(IPerson person);
    void Refresh();
    event EventHandler? StateChanged;
}
