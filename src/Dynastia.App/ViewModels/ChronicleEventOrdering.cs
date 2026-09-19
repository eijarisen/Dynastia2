using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public static class ChronicleEventOrdering
{
    public static int GetSeverity(
        GameEvent gameEvent)
    {
        var type = gameEvent.Type;

        if (type.StartsWith(
                "historical.",
                StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }

        if (type.Equals(
                "life.death",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "life.birth",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "peripheral.birth",
                StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (type.Equals(
                "justice.crime",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "justice.crime_uncaught",
                StringComparison.OrdinalIgnoreCase)
            || type.StartsWith(
                "rare.",
                StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (type.Equals(
                "life.adult",
                StringComparison.OrdinalIgnoreCase)
            || type.StartsWith(
                "relationship.married",
                StringComparison.OrdinalIgnoreCase)
            || type.StartsWith(
                "relationship.remarried",
                StringComparison.OrdinalIgnoreCase)
            || type.StartsWith(
                "relationship.partnered",
                StringComparison.OrdinalIgnoreCase)
            || type.Contains(
                "divorce",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.affair",
                StringComparison.OrdinalIgnoreCase)
            || type.StartsWith(
                "health.",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "birth.condition",
                StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 3;
    }
}
