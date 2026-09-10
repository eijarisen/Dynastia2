using Dynastia.Contracts;

namespace Dynastia.Plugin.Sample;

public sealed class SamplePlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        var actions = context.GetService<IActionRegistry>();

        if (actions is not null)
        {
            actions.Register(
                new GameActionDefinition
                {
                    Id = "sample.record_event",
                    Label = "Record Test Event",
                    Description =
                        "Temporary action used to verify the plugin-driven action system.",
                    Mode = ActionExecutionMode.Immediate,

                    IsAvailable = actionContext =>
                        actionContext.Target.Tags.Has("state.alive"),

                    Execute = actionContext =>
                    {
                        actionContext.EventBus.Publish(
                            new GameEvent
                            {
                                Type = "sample.action",
                                Year = actionContext.GameState.Year,
                                SubjectId = actionContext.Target.Id,
                                Data = new Dictionary<string, string>
                                {
                                    ["text"] =
                                        $"{actionContext.Target.Name} " +
                                        $"{actionContext.Target.Surname} " +
                                        "recorded a test event."
                                }
                            });

                        return new GameActionResult(true);
                    }
                });

            context.Log("Sample action registered.");
        }

        context.Log("Sample plugin initialized successfully.");
    }
}
