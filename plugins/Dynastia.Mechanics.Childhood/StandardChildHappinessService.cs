using Dynastia.Contracts;

namespace Dynastia.Mechanics.Childhood;

public sealed class StandardChildHappinessService : IChildHappinessService
{
    public ChildHappinessSnapshot? GetHappiness(IPerson person)
    {
        if (person.Age >= 18)
            return null;

        EnsureHappiness(person);
        var value = person.Components.Get<ChildHappinessComponent>()!.Value;
        return new ChildHappinessSnapshot(value, Label(value));
    }

    public void EnsureHappiness(IPerson person)
    {
        if (person.Age >= 18)
        {
            person.Tags.Remove("child.happiness.unhappy");
            person.Tags.Remove("child.happiness.miserable");
            return;
        }

        if (person.Components.Has<ChildHappinessComponent>())
        {
            SyncTags(person, person.Components.Get<ChildHappinessComponent>()!.Value);
            return;
        }

        person.Components.Set(new ChildHappinessComponent());
        SyncTags(person, 3);
    }

    public void ChangeHappiness(IPerson person, int amount)
    {
        if (person.Age >= 18 || amount == 0)
            return;

        EnsureHappiness(person);
        var component = person.Components.Get<ChildHappinessComponent>()!;
        var scaled = ScaleChange(person, amount);
        component.Value = Math.Clamp(component.Value + scaled, 1, 5);
        SyncTags(person, component.Value);
    }


    private static void SyncTags(IPerson person, int value)
    {
        person.Tags.Remove("child.happiness.unhappy");
        person.Tags.Remove("child.happiness.miserable");

        if (value <= 1)
            person.Tags.Add("child.happiness.miserable");
        else if (value == 2)
            person.Tags.Add("child.happiness.unhappy");
    }

    private static int ScaleChange(IPerson person, int amount)
    {
        // Melancholic and Choleric children are more reactive and therefore
        // occasionally move two steps from a meaningful event. Phlegmatic
        // children resist extremes; Sanguine children respond more strongly
        // to positive attention than to ordinary setbacks.
        var magnitude = Math.Abs(amount);
        if (magnitude == 0)
            return 0;

        if ((person.Tags.Has("personality.melancholic")
             || person.Tags.Has("personality.choleric"))
            && magnitude >= 2)
        {
            magnitude++;
        }
        else if (person.Tags.Has("personality.phlegmatic"))
        {
            magnitude = Math.Max(1, magnitude - 1);
        }
        else if (person.Tags.Has("personality.sanguine") && amount > 0)
        {
            magnitude++;
        }

        return Math.Sign(amount) * magnitude;
    }

    private static string Label(int value) => value switch
    {
        <= 1 => "Miserable",
        2 => "Unhappy",
        3 => "Content",
        4 => "Happy",
        _ => "Thriving"
    };
}
