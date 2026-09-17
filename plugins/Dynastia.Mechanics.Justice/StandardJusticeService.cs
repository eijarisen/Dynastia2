using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

public sealed class StandardJusticeService :
    IJusticeService
{
    public void EnsureJustice(
        IPerson person)
    {
        if (person.Components.Has<
            JusticeComponent>())
        {
            return;
        }

        person.Components.Set(
            new JusticeComponent());
    }

    public JusticeSnapshot GetStatus(
        IPerson person)
    {
        var justice =
            GetRequired(
                person);

        return new JusticeSnapshot(
            justice.PrisonSentence > 0,
            justice.PrisonSentence,
            justice.PrisonSentence >= 50,
            justice.CrimeId,
            justice.CrimeName,
            justice.CrimeDescription);
    }

    public bool IsImprisoned(
        IPerson person)
    {
        return GetRequired(
            person)
            .PrisonSentence > 0;
    }

    public void Imprison(
        IPerson person,
        int sentence,
        string reasonId,
        string reasonName,
        string? reasonDescription = null)
    {
        var justice =
            GetMutable(
                person);

        justice.PrisonSentence =
            Math.Max(
                0,
                sentence);

        justice.CrimeId =
            reasonId;

        justice.CrimeName =
            reasonName;

        justice.CrimeDescription =
            reasonDescription;

        if (justice.PrisonSentence > 0)
        {
            person.Tags.Add(
                "state.imprisoned");
        }
        else
        {
            person.Tags.Remove(
                "state.imprisoned");
        }
    }

    internal void Imprison(
        IPerson person,
        CrimeDefinition crime,
        int sentence)
    {
        Imprison(
            person,
            sentence,
            crime.Id,
            crime.Name,
            crime.Description);
    }

    internal bool AdvanceSentence(
        IPerson person)
    {
        var justice =
            GetMutable(
                person);

        if (justice.PrisonSentence <= 0)
            return false;

        justice.PrisonSentence--;

        if (justice.PrisonSentence > 0)
            return false;

        justice.PrisonSentence = 0;

        person.Tags.Remove(
            "state.imprisoned");

        justice.CrimeId = null;
        justice.CrimeName = null;
        justice.CrimeDescription = null;

        return true;
    }

    internal void ReconcileAll(IEnumerable<IPerson> people)
    {
        foreach (var person in people)
            EnsureJustice(person);
    }

    private static JusticeComponent GetRequired(
        IPerson person) =>
        person.Components.Get<JusticeComponent>()
        ?? throw new InvalidOperationException(
            "Justice state is missing. Run state reconciliation before reading it.");

    private JusticeComponent GetMutable(IPerson person)
    {
        EnsureJustice(person);
        return GetRequired(person);
    }
}
