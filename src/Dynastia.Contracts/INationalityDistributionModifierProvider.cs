namespace Dynastia.Contracts;

public interface INationalityDistributionModifierProvider
{
    IReadOnlyDictionary<string, double> Apply(
        string regionId,
        int year,
        IReadOnlyDictionary<string, double> distribution);
}
