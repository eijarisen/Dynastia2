using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Personality;

public sealed class StandardPersonalityService :
    IPersonalityService
{
    public const int AssignmentAge = 5;

    private static readonly string[] Temperaments =
    [
        "Melancholic",
        "Phlegmatic",
        "Sanguine",
        "Choleric"
    ];

    private static readonly string[] TemperamentTags =
    [
        "personality.melancholic",
        "personality.phlegmatic",
        "personality.sanguine",
        "personality.choleric"
    ];

    private static readonly string[] MoralsTags =
    [
        "morals.good",
        "morals.neutral",
        "morals.evil"
    ];

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;

    public StandardPersonalityService(
        IGameState gameState,
        IFamilyService family)
    {
        _gameState = gameState;
        _family = family;
    }

    public PersonalitySnapshot? GetPersonality(
        IPerson person)
    {
        ArgumentNullException.ThrowIfNull(
            person);

        if (person.Age < AssignmentAge)
            return null;

        var component =
            person.Components.Get<
                PersonalityComponent>();

        if (component is null
            || string.IsNullOrWhiteSpace(
                component.Temperament)
            || string.IsNullOrWhiteSpace(
                component.Morals))
        {
            throw new InvalidOperationException(
                "Personality state is incomplete. Run state reconciliation before reading it.");
        }

        return new PersonalitySnapshot(
            component.Temperament,
            component.Morals);
    }

    public PersonalitySnapshot GenerateCandidatePersonality(
        Guid candidateId)
    {
        var temperamentRoll = Roll(
            candidateId,
            "temperament-random-no-parents");

        var temperament = Temperaments[
            Math.Min(
                Temperaments.Length - 1,
                (int)(temperamentRoll * Temperaments.Length))];

        var moralsRoll = Roll(
            candidateId,
            "morals");

        var morals = moralsRoll < 0.35
            ? "Good"
            : moralsRoll < 0.75
                ? "Neutral"
                : "Evil";

        return new PersonalitySnapshot(
            temperament,
            morals);
    }

    public void SetPersonality(
        IPerson person,
        PersonalitySnapshot personality)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(personality);

        var component = new PersonalityComponent
        {
            Temperament = personality.Temperament,
            Morals = personality.Morals
        };

        person.Components.Set(component);
        ApplyTags(person, component);
    }

    public void ReconcileAll()
    {
        foreach (var person in
            _gameState.People)
        {
            if (person.Age < AssignmentAge)
            {
                person.Components.Remove<
                    PersonalityComponent>();

                ClearTags(
                    person);

                continue;
            }

            EnsurePersonality(
                person,
                []);
        }
    }

    internal void ReconcilePerson(
        IPerson person)
    {
        if (person.Age < AssignmentAge)
        {
            person.Components.Remove<
                PersonalityComponent>();

            ClearTags(
                person);

            return;
        }

        EnsurePersonality(
            person,
            []);
    }

    private void EnsurePersonality(
        IPerson person,
        HashSet<Guid> resolving)
    {
        var existing =
            person.Components.Get<
                PersonalityComponent>();

        if (IsComplete(
            existing))
        {
            ApplyTags(
                person,
                existing!);

            return;
        }

        if (!resolving.Add(
            person.Id))
        {
            AssignWithoutParents(
                person);

            return;
        }

        try
        {
            var father =
                _family.GetFather(
                    person);

            var mother =
                _family.GetMother(
                    person);

            EnsureParentIfEligible(
                father,
                resolving);

            EnsureParentIfEligible(
                mother,
                resolving);

            var fatherTemperament =
                GetTemperament(
                    father);

            var motherTemperament =
                GetTemperament(
                    mother);

            var component =
                existing
                ?? new PersonalityComponent();

            component.Temperament =
                SelectTemperament(
                    person,
                    fatherTemperament,
                    motherTemperament);

            component.Morals =
                SelectMorals(
                    person);

            person.Components.Set(
                component);

            ApplyTags(
                person,
                component);
        }
        finally
        {
            resolving.Remove(
                person.Id);
        }
    }

    private void EnsureParentIfEligible(
        IPerson? parent,
        HashSet<Guid> resolving)
    {
        if (parent is null
            || parent.Age < AssignmentAge)
        {
            return;
        }

        EnsurePersonality(
            parent,
            resolving);
    }

    private void AssignWithoutParents(
        IPerson person)
    {
        var component =
            person.Components.Get<
                PersonalityComponent>()
            ?? new PersonalityComponent();

        component.Temperament =
            RandomTemperament(
                person,
                "temperament-cycle-fallback");

        component.Morals =
            SelectMorals(
                person);

        person.Components.Set(
            component);

        ApplyTags(
            person,
            component);
    }

    private string SelectTemperament(
        IPerson person,
        string? fatherTemperament,
        string? motherTemperament)
    {
        var roll =
            Roll(
                person,
                "temperament-inheritance");

        if (fatherTemperament is not null
            && motherTemperament is not null)
        {
            if (roll < 0.40)
                return fatherTemperament;

            if (roll < 0.80)
                return motherTemperament;

            return RandomTemperament(
                person,
                "temperament-random");
        }

        var known =
            fatherTemperament
            ?? motherTemperament;

        if (known is not null)
        {
            if (roll < 0.80)
                return known;

            return RandomTemperament(
                person,
                "temperament-random-one-parent");
        }

        return RandomTemperament(
            person,
            "temperament-random-no-parents");
    }

    private string SelectMorals(
        IPerson person)
    {
        var roll =
            Roll(
                person,
                "morals");

        if (roll < 0.35)
            return "Good";

        if (roll < 0.75)
            return "Neutral";

        return "Evil";
    }

    private string RandomTemperament(
        IPerson person,
        string purpose)
    {
        var roll =
            Roll(
                person,
                purpose);

        var index =
            Math.Min(
                Temperaments.Length - 1,
                (int)(roll
                    * Temperaments.Length));

        return Temperaments[index];
    }

    private string? GetTemperament(
        IPerson? person)
    {
        if (person is null
            || person.Age < AssignmentAge)
        {
            return null;
        }

        var component =
            person.Components.Get<
                PersonalityComponent>();

        return IsComplete(
                component)
            ? component!.Temperament
            : null;
    }

    public bool ShiftMorals(
        IPerson person,
        int steps)
    {
        var component =
            person.Components.Get<PersonalityComponent>();

        if (!IsComplete(component)
            || steps == 0)
        {
            return false;
        }

        if (steps < 0
            && component!.LastMoralsDeclineYear == _gameState.Year)
        {
            return false;
        }

        if (steps < 0
            && component!.Morals == "Good"
            && component.HasMoralsProtection)
        {
            component.HasMoralsProtection = false;
            person.Components.Set(component);
            return false;
        }

        var index = component!.Morals switch
        {
            "Good" => 2,
            "Neutral" => 1,
            "Evil" => 0,
            _ => 1
        };

        var next = Math.Clamp(index + steps, 0, 2);
        if (next == index)
            return false;

        component.Morals = next switch
        {
            2 => "Good",
            1 => "Neutral",
            _ => "Evil"
        };

        if (steps < 0)
            component.LastMoralsDeclineYear = _gameState.Year;

        ApplyTags(person, component);
        return true;
    }

    public bool HasMoralsProtection(IPerson person)
    {
        var component = person.Components.Get<PersonalityComponent>();
        return IsComplete(component)
            && component!.HasMoralsProtection;
    }

    public void GrantMoralsProtection(IPerson person)
    {
        var component = person.Components.Get<PersonalityComponent>();
        if (!IsComplete(component))
            return;

        component!.HasMoralsProtection = true;
        person.Components.Set(component);
    }

    private double Roll(
        IPerson person,
        string purpose) =>
        Roll(person.Id, purpose);

    private double Roll(
        Guid personId,
        string purpose)
    {
        var input =
            $"{_gameState.DynastySurname}|" +
            $"{personId:N}|{purpose}";

        var hash =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    input));

        var value =
            BinaryPrimitives.ReadUInt64BigEndian(
                hash.AsSpan(
                    0,
                    sizeof(ulong)));

        return value
            / (double)ulong.MaxValue;
    }

    private static bool IsComplete(
        PersonalityComponent? component)
    {
        return component is not null
            && !string.IsNullOrWhiteSpace(
                component.Temperament)
            && !string.IsNullOrWhiteSpace(
                component.Morals);
    }

    private static void ApplyTags(
        IPerson person,
        PersonalityComponent component)
    {
        ClearTags(
            person);

        var temperament =
            component.Temperament!
                .Trim()
                .ToLowerInvariant();

        var morals =
            component.Morals!
                .Trim()
                .ToLowerInvariant();

        person.Tags.Add(
            $"personality.{temperament}");

        person.Tags.Add(
            $"morals.{morals}");
    }

    private static void ClearTags(
        IPerson person)
    {
        foreach (var tag in
            TemperamentTags)
        {
            person.Tags.Remove(
                tag);
        }

        foreach (var tag in
            MoralsTags)
        {
            person.Tags.Remove(
                tag);
        }
    }
}
