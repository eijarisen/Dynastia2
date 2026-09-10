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
            justice.CrimeName);
    }

    public bool IsImprisoned(
        IPerson person)
    {
        return GetRequired(
            person)
            .PrisonSentence > 0;
    }

    internal void Imprison(
        IPerson person,
        CrimeDefinition crime,
        int sentence)
    {
        var justice =
            GetRequired(
                person);

        justice.PrisonSentence =
            Math.Max(
                0,
                sentence);

        justice.CrimeId =
            crime.Id;

        justice.CrimeName =
            crime.Name;

        if (justice.PrisonSentence > 0)
        {
            person.Tags.Add(
                "state.imprisoned");
        }
    }

    internal bool AdvanceSentence(
        IPerson person)
    {
        var justice =
            GetRequired(
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

        return true;
    }

    private JusticeComponent GetRequired(
        IPerson person)
    {
        EnsureJustice(
            person);

        return person.Components.Get<
            JusticeComponent>()
            ?? throw new InvalidOperationException(
                "Justice component could not be created.");
    }
}
