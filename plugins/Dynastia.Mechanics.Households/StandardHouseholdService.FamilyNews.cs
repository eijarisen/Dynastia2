using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

public sealed partial class StandardHouseholdService
{
    public bool ShouldShowFamilyNews(
        GameEvent gameEvent)
    {
        var eventIndex =
            IndexOfEvent(
                gameEvent);

        var ledger =
            GetNewsLedger(
                create:
                    false);

        if (eventIndex >= 0
            && ledger is not null
            && ledger.VisibilityByEventIndex
                .TryGetValue(
                    eventIndex,
                    out var stored))
        {
            return stored;
        }

        // Pre-rework saves have no historical visibility ledger. For those
        // events, use the best classification available from current state.
        return EvaluateFamilyNewsNow(
            gameEvent);
    }

    internal void RecordFamilyNewsVisibility(
        GameEvent gameEvent)
    {
        var index =
            IndexOfEvent(
                gameEvent);

        if (index < 0)
            return;

        var ledger =
            GetNewsLedger(
                create:
                    true);

        if (ledger is null)
            return;

        ledger.VisibilityByEventIndex[index] =
            EvaluateFamilyNewsNow(
                gameEvent);
    }

    private bool EvaluateFamilyNewsNow(
        GameEvent gameEvent)
    {
        if (gameEvent.Data.TryGetValue(
                "suppressChronicle",
                out var suppress)
            && suppress.Equals(
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var involved =
            new List<Guid>();

        if (gameEvent.SubjectId
            is Guid subjectId)
        {
            involved.Add(
                subjectId);
        }

        involved.AddRange(
            gameEvent.RelatedPersonIds);

        var involvedPeople =
            involved
                .Distinct()
                .Select(
                    FindPerson)
                .Where(
                    person =>
                        person is not null)
                .Cast<IPerson>()
                .ToList();

        if (involvedPeople.Any(
            person =>
                GetHouseholdInfo(
                    person)?.Class
                    == HouseholdClass.Lineage))
        {
            return true;
        }

        if (!involvedPeople.Any(
            person =>
                _family.IsBloodline(
                    person)
                || person.Tags.Has(
                    "simulation.peripheral_ex")
                || person.Tags.Has(
                    "simulation.peripheral_partner")
                || HasDirectBloodlineMarriage(
                    person)))
        {
            return false;
        }

        var type =
            gameEvent.Type;

        if (type.Equals(
                "personality.religious_study",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (type.Equals(
                "life.birth",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "peripheral.birth",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "life.death",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (type.Equals(
                "relationship.married",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.remarried",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.partnered",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.divorce",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.low_satisfaction_divorce",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.prison_divorce",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.affair",
                StringComparison.OrdinalIgnoreCase))
        {
            // The affair event is itself the divorce event in the current
            // relationship mechanic, so it belongs to the permitted
            // marriage/divorce family-news category.
            return true;
        }

        if (type.Equals(
                "health.serious_illness",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "health.natural_recovery",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "health.second_wind",
                StringComparison.OrdinalIgnoreCase))
        {
            return gameEvent.Data.TryGetValue(
                    "familyNews",
                    out var familyNews)
                && familyNews.Equals(
                    "true",
                    StringComparison.OrdinalIgnoreCase);
        }

        if (type.Equals(
                "justice.crime",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "justice.released",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return type.StartsWith(
            "rare.",
            StringComparison.OrdinalIgnoreCase);
    }

    private int IndexOfEvent(
        GameEvent gameEvent)
    {
        for (var index = 0;
            index < _events.AllEvents.Count;
            index++)
        {
            if (ReferenceEquals(
                _events.AllEvents[index],
                gameEvent))
            {
                return index;
            }
        }

        return -1;
    }

    private FamilyNewsLedgerComponent? GetNewsLedger(
        bool create)
    {
        var anchor =
            _gameState.People
                .Where(
                    person =>
                        _family.IsBloodline(
                            person))
                .OrderBy(
                    person =>
                        _family.GetGeneration(
                            person)
                        ?? int.MaxValue)
                .ThenBy(
                    BirthSortKey)
                .FirstOrDefault();

        if (anchor is null)
            return null;

        var ledger =
            anchor.Components.Get<
                FamilyNewsLedgerComponent>();

        if (ledger is not null
            || !create)
        {
            return ledger;
        }

        ledger =
            new FamilyNewsLedgerComponent();

        anchor.Components.Set(
            ledger);

        return ledger;
    }

    internal void UpdatePeripheralRelationshipState(
        GameEvent gameEvent)
    {
        var isBreakup =
            gameEvent.Type.Equals(
                "relationship.divorce",
                StringComparison.OrdinalIgnoreCase)
            || gameEvent.Type.Equals(
                "relationship.low_satisfaction_divorce",
                StringComparison.OrdinalIgnoreCase)
            || gameEvent.Type.Equals(
                "relationship.prison_divorce",
                StringComparison.OrdinalIgnoreCase)
            || gameEvent.Type.Equals(
                "relationship.affair",
                StringComparison.OrdinalIgnoreCase);

        if (isBreakup)
        {
            var involved =
                new List<IPerson>();

            if (gameEvent.SubjectId
                is Guid subjectId)
            {
                var subject =
                    FindPerson(
                        subjectId);

                if (subject is not null)
                {
                    involved.Add(
                        subject);
                }
            }

            involved.AddRange(
                gameEvent.RelatedPersonIds
                    .Select(
                        FindPerson)
                    .Where(
                        person =>
                            person is not null)
                    .Cast<IPerson>());

            var ex =
                involved.FirstOrDefault(
                    person =>
                        person.Tags.Has(
                            "simulation.peripheral_ex"));

            var laterPartner =
                involved.FirstOrDefault(
                    person =>
                        person.Tags.Has(
                            "simulation.peripheral_partner"));

            if (ex is not null
                && laterPartner is not null)
            {
                laterPartner.Tags.Remove(
                    "simulation.peripheral_partner");

                laterPartner.Tags.Add(
                    "simulation.peripheral_inactive");
            }

            foreach (var formerPartner in
                involved
                    .Where(
                        person =>
                            !_family.IsBloodline(
                                person)
                            && HasDirectBloodlineMarriage(
                                person))
                    .DistinctBy(
                        person =>
                            person.Id)
                    .ToList())
            {
                DetachFormerPartnerAfterBreakup(
                    formerPartner,
                    involved);
            }

            return;
        }

        if (!gameEvent.Type.Equals(
                "life.death",
                StringComparison.OrdinalIgnoreCase)
            || gameEvent.SubjectId
                is not Guid deceasedId)
        {
            return;
        }

        var deceased =
            FindPerson(
                deceasedId);

        if (deceased is null)
            return;

        // A living current/former partner of a Bloodline person remains part
        // of active simulation for life. When the Bloodline partner dies,
        // preserve the survivor as a direct former partner instead of
        // archiving them. Their later spouse is only a temporary peripheral
        // person and is handled separately below/reconciliation.
        if (_family.IsBloodline(deceased))
        {
            var directPartners =
                gameEvent.RelatedPersonIds
                    .Select(FindPerson)
                    .Where(person =>
                        person is not null
                        && person.Tags.Has("state.alive")
                        && !_family.IsBloodline(person)
                        && HasDirectBloodlineMarriage(person))
                    .Cast<IPerson>()
                    .DistinctBy(person => person.Id)
                    .ToList();

            var staleSpouse =
                _family.GetSpouse(deceased);

            if (staleSpouse is not null
                && staleSpouse.Tags.Has("state.alive")
                && !_family.IsBloodline(staleSpouse)
                && HasDirectBloodlineMarriage(staleSpouse)
                && !directPartners.Any(person => person.Id == staleSpouse.Id))
            {
                directPartners.Add(staleSpouse);
            }

            foreach (var partner in directPartners)
            {
                partner.Tags.Add(
                    "simulation.peripheral_ex");

                partner.Tags.Remove(
                    SimulationState.PeripheralInactiveTag);
            }
        }

        if (!deceased.Tags.Has(
                "simulation.peripheral_ex"))
        {
            return;
        }

        var laterPartners =
            gameEvent.RelatedPersonIds
                .Select(
                    FindPerson)
                .Where(
                    person =>
                        person is not null
                        && person.Tags.Has(
                            "simulation.peripheral_partner"))
                .Cast<IPerson>()
                .ToList();

        var currentPartner =
            _family.GetSpouse(
                deceased);

        if (currentPartner is not null
            && currentPartner.Tags.Has(
                "simulation.peripheral_partner")
            && !laterPartners.Any(
                partner =>
                    partner.Id
                    == currentPartner.Id))
        {
            laterPartners.Add(
                currentPartner);
        }

        foreach (var related in
            laterPartners)
        {
            related.Tags.Remove(
                "simulation.peripheral_partner");

            related.Tags.Add(
                "simulation.peripheral_inactive");
        }
    }

}
