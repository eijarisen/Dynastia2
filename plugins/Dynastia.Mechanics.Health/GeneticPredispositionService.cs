using System.Security.Cryptography;
using System.Text;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

internal sealed class GeneticPredispositionService
{
    internal const string InitializedTag = "genetic.health_initialized";

    internal static readonly IReadOnlyDictionary<string, double> BaselineRates =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["genetic.migraine"] = 0.08,
            ["genetic.asthma"] = 0.06,
            ["genetic.epilepsy"] = 0.025,
            ["genetic.hypertension"] = 0.08,
            ["genetic.diabetes"] = 0.06,
            ["genetic.heart"] = 0.06,
            ["genetic.cancer"] = 0.05
        };

    private readonly IGameState _state;
    private readonly IFamilyService _family;

    public GeneticPredispositionService(IGameState state, IFamilyService family)
    {
        _state = state;
        _family = family;
    }

    public void ReconcileAll()
    {
        foreach (var person in _state.People
                     .OrderBy(p => p.BirthDate?.Year ?? int.MaxValue)
                     .ThenBy(p => p.Id))
            Ensure(person, new HashSet<Guid>());
    }

    public void Ensure(IPerson person) => Ensure(person, new HashSet<Guid>());

    private void Ensure(IPerson person, HashSet<Guid> visiting)
    {
        if (person.Tags.Has(InitializedTag))
            return;

        if (!visiting.Add(person.Id))
            return;

        var father = _family.GetFather(person);
        var mother = _family.GetMother(person);

        if (father is not null) Ensure(father, visiting);
        if (mother is not null) Ensure(mother, visiting);

        foreach (var (tag, baseline) in BaselineRates)
        {
            var inherited = false;
            var hasSimulatedParent = father is not null || mother is not null;

            if (father?.Tags.Has(tag) == true)
                inherited |= StableChance(person.Id, tag + ":father", 0.50);
            if (mother?.Tags.Has(tag) == true)
                inherited |= StableChance(person.Id, tag + ":mother", 0.50);

            if (inherited || (!hasSimulatedParent && StableChance(person.Id, tag + ":baseline", baseline)))
                person.Tags.Add(tag);
        }

        person.Tags.Add(InitializedTag);
        visiting.Remove(person.Id);
    }

    private static bool StableChance(Guid id, string salt, double chance)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(id.ToString("N") + ":" + salt));
        var value = BitConverter.ToUInt64(bytes, 0) / (double)ulong.MaxValue;
        return value < chance;
    }
}
