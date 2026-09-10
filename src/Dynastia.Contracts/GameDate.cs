namespace Dynastia.Contracts;

public readonly record struct GameDate(
    int Year,
    int? Month = null,
    int? Day = null)
{
    public override string ToString()
    {
        if (Day.HasValue && Month.HasValue)
            return $"{Day.Value}/{Month.Value}/{Year}";

        if (Month.HasValue)
            return $"{Month.Value}/{Year}";

        return Year.ToString();
    }
}
