using Dynastia.App.ViewModels;
using Dynastia.App.ViewModels.Actions;
using Dynastia.Contracts;

namespace Dynastia.App.Tests;

/// <summary>Tests collaborators independently of MainWindow's refresh and binding wiring.</summary>
internal sealed class ActionCoordinatorFixture : IDisposable
{
    public ActionCoordinatorFixture(IFamilyService? family = null, IFarmingService? farming = null,
        ILoanService? loans = null, IHeirloomService? heirlooms = null, IJusticeService? justice = null)
    {
        Selected = Source.Head;
        Options = new ActionSelectionOptionService(Source.Succession, Source.Registry, Source.Economy,
            farming, heirlooms, loans, Source.Locations, null, null);
        Surfaces = new ActionSurfaceDefinitions(Source.Locations, Source.Economy, justice);
        Panel = new ActionPanelCoordinator(Source.State, Source.Succession, Source.Registry, family,
            Source.Economy, farming, Source.Locations, Options, Surfaces, () => Selected);
        Panel.ActionExecuted += (_, result) => Results.Add(result);
        Panel.ActionSelectionRequested += (_, request) => Requests.Add(request.ActionId);
    }

    public ActionPanelFixture Source { get; } = new();
    public IPerson? Selected { get; set; }
    public ActionSelectionOptionService Options { get; }
    public ActionSurfaceDefinitions Surfaces { get; }
    public ActionPanelCoordinator Panel { get; }
    public List<ActionUiExecutionResult> Results { get; } = [];
    public List<string> Requests { get; } = [];
    public void Click(string id) => Panel.AvailableActions.Single(action => action.Id == id).ExecuteCommand.Execute(null);
    public void Dispose() => Source.Dispose();
}
