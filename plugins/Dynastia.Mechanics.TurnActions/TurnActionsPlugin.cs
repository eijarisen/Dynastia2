using Dynastia.Contracts;

namespace Dynastia.Mechanics.TurnActions;

public sealed class TurnActionsPlugin :
    IGamePlugin
{
    public void Initialize(
        IGamePluginContext context)
    {
        var actions =
            context.GetService<IActionRegistry>()
            ?? throw new InvalidOperationException(
                "Action registry is unavailable.");

        actions.Register(
            new GameActionDefinition
            {
                Id =
                    "turn.pass",

                Label =
                    "Pass",

                Description =
                    "Do nothing this year. This still counts as the " +
                    "male heir's required annual action.",

                Mode =
                    ActionExecutionMode.Queued,

                QueuePhase =
                    YearPhase.QueuedActionsEarly,

                BypassGuards =
                    true,

                IsAvailable =
                    actionContext =>
                        actionContext.Actor.Id
                            == actionContext.Target.Id
                        && actionContext.Actor.Tags.Has(
                            "state.alive")
                        && !actionContext.Target.Tags.Has(
                            "state.dead")
                        && actionContext.Actor.Tags.Has(
                            "control.playable"),

                Execute =
                    _ =>
                        new GameActionResult(
                            true)
            });

        context.Log(
            "Pass annual action registered.");
    }
}
