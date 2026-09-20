namespace Dynastia.Contracts;

public static class PolishSurnameRules
{
    public static string Feminize(string surname)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surname);

        if (surname.EndsWith("dzki", StringComparison.OrdinalIgnoreCase))
            return surname[..^4] + "dzka";

        if (surname.EndsWith("ski", StringComparison.OrdinalIgnoreCase))
            return surname[..^3] + "ska";

        if (surname.EndsWith("cki", StringComparison.OrdinalIgnoreCase))
            return surname[..^3] + "cka";

        if (surname.EndsWith("chy", StringComparison.OrdinalIgnoreCase))
            return surname[..^3] + "cha";

        if (surname.EndsWith("ny", StringComparison.OrdinalIgnoreCase))
            return surname[..^2] + "na";

        return surname;
    }
}
