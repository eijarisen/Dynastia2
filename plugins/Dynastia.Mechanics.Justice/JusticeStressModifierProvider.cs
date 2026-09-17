using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

internal sealed class JusticeStressModifierProvider : IStressModifierProvider
{
    private readonly IJusticeService _justice;

    public JusticeStressModifierProvider(IJusticeService justice)
    {
        _justice = justice;
    }

    public string Id => "justice.stress";

    public IEnumerable<StressContribution> GetStressContributions(IPerson person, int year)
    {
        if (_justice.IsImprisoned(person))
            yield return new StressContribution("justice.imprisonment", 2);
    }
}
