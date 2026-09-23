using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed partial class EconomyPlugin
{
    private static void RegisterLifestyleActions(
        IActionRegistry actions,
        StandardEconomyService economy,
        IFamilyService family,
        IGameEventBus events)
    {
        RegisterLifestyleAction(
            actions,
            economy,
            family,
            events,
            "economy.lifestyle.lavish",
            "Lavish Lifestyle",
            "Spend freely on comfort, leisure and opportunities. Living costs rise by 10%, while household wellbeing and social advantages improve slightly.",
            HouseholdLifestyleStance.Lavish);

        RegisterLifestyleAction(
            actions,
            economy,
            family,
            events,
            "economy.lifestyle.balanced",
            "Balanced Lifestyle",
            "Maintain the household's ordinary standard of living with no lifestyle bonuses or penalties.",
            HouseholdLifestyleStance.Balanced);

        RegisterLifestyleAction(
            actions,
            economy,
            family,
            events,
            "economy.lifestyle.thrifty",
            "Thrifty Lifestyle",
            "Cut discretionary spending and live carefully. Living costs fall by 10%, with small trade-offs to comfort, morale and opportunity.",
            HouseholdLifestyleStance.Thrifty);
    }

    private static void RegisterLifestyleAction(
        IActionRegistry actions,
        StandardEconomyService economy,
        IFamilyService family,
        IGameEventBus events,
        string id,
        string label,
        string description,
        HouseholdLifestyleStance stance)
    {
        actions.Register(
            new GameActionDefinition
            {
                Id = id,
                Presentation = new()
                {
                    Emoji = "⚙️",
                    Categories = [ActionPresentationCategories.Personal]
                },
                Label = label,
                Description = description,
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,
                IsAvailable = context =>
                    context.ActorHasControl
                    && context.Actor.Id == context.Target.Id
                    && context.Actor.Tags.Has("state.alive")
                    && economy.HasHousehold(context.Actor)
                    && economy.GetLifestyle(context.Actor) != stance,
                Execute = context =>
                {
                    if (!context.Actor.Tags.Has("state.alive")
                        || !economy.HasHousehold(context.Actor))
                    {
                        return new GameActionResult(false);
                    }

                    economy.SetLifestyle(context.Actor, stance);

                    events.Publish(
                        new GameEvent
                        {
                            Type = "economy.lifestyle_changed",
                            Year = context.GameState.Year,
                            SubjectId = context.Actor.Id,
                            Data = new Dictionary<string, string>
                            {
                                ["stance"] = stance.ToString(),
                                ["text"] =
                                    $"{family.GetDisplayName(context.Actor)} set the household lifestyle to {stance}."
                            }
                        });

                    return new GameActionResult(true);
                }
            });
    }
}
