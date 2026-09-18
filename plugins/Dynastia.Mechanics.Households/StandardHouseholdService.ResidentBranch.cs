using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

public sealed partial class StandardHouseholdService
{
    public MoveResidentBranchResult EstablishIndependentResidentBranch(
        IPerson sourceHead,
        IPerson newHead,
        Guid? propertyId = null)
    {
        ArgumentNullException.ThrowIfNull(sourceHead);
        ArgumentNullException.ThrowIfNull(newHead);

        var actualHead = ResolveHouseholdHead(sourceHead);
        if (actualHead?.Id != sourceHead.Id
            || !_economy.HasHousehold(sourceHead))
        {
            return Failed("The source household is no longer valid.");
        }

        var sourceHouseholdId = _economy.GetHouseholdId(sourceHead);
        if (sourceHouseholdId is null
            || newHead.Id == sourceHead.Id
            || _economy.GetHouseholdId(newHead) != sourceHouseholdId
            || _economy.HasHousehold(newHead))
        {
            return Failed("The selected resident can no longer establish a household from this home.");
        }

        var residentIds = _economy
            .GetHouseholdMemberIds(sourceHead)
            .ToHashSet();

        if (!residentIds.Contains(newHead.Id))
            return Failed("The selected resident no longer lives in this household.");

        var branch = CollectResidentBranch(
            newHead,
            residentIds,
            sourceHead.Id);
        if (branch.Count == 0)
            return Failed("No resident family branch could be moved.");

        HousePropertyInfo? selectedHouse = null;
        if (propertyId is Guid selectedPropertyId)
        {
            selectedHouse = _economy
                .GetHouses(sourceHead)
                .FirstOrDefault(house =>
                    house.Id == selectedPropertyId
                    && !house.IsResidence);

            if (selectedHouse is null)
            {
                return Failed("The selected spare house is no longer available.");
            }
        }

        var origin = _economy.GetResidenceTown(sourceHead);
        var destination = selectedHouse?.Town ?? origin;

        HousePropertyInfo? transferredHouse = null;
        if (selectedHouse is not null)
        {
            transferredHouse = _economy.TakeHouse(
                sourceHead,
                selectedHouse.Id);

            if (transferredHouse is null)
                return Failed("The selected spare house is no longer available.");
        }

        EndResidentCaregiverRoles(sourceHead, branch);

        foreach (var member in branch)
            _economy.RemoveHouseholdMember(member);

        newHead.Tags.Add("residence.independent");
        _economy.EnsureIndependentHousehold(newHead, newHead);

        foreach (var member in branch)
        {
            if (member.Id != newHead.Id)
                _economy.AddHouseholdMember(newHead, member);
        }

        if (transferredHouse is not null)
        {
            _economy.AddExistingHouse(
                newHead,
                transferredHouse with { AssignedHeirId = null });
        }

        _economy.SetResidenceTown(newHead, destination);

        // Read the property back after residence reconciliation so callers
        // receive its destination-household status rather than the old
        // source-household rental flags captured by TakeHouse().
        if (transferredHouse is not null)
        {
            var transferredHouseId = transferredHouse.Id;
            transferredHouse = _economy.GetHouses(newHead)
                .FirstOrDefault(house => house.Id == transferredHouseId)
                ?? transferredHouse with { AssignedHeirId = null };
        }

        var townChanged = !origin.Id.Equals(
            destination.Id,
            StringComparison.OrdinalIgnoreCase);

        if (townChanged)
        {
            RelocateResidentBranchEmployment(branch, destination);

            _events.Publish(new GameEvent
            {
                Type = "household.moved",
                Year = _gameState.Year,
                SubjectId = newHead.Id,
                RelatedPersonIds = branch
                    .Where(person => person.Id != newHead.Id)
                    .Select(person => person.Id)
                    .ToList(),
                Data = new Dictionary<string, string>
                {
                    ["fromTown"] = origin.Town,
                    ["toTown"] = destination.Town,
                    ["text"] =
                        $"The {newHead.Surname} household moved from {origin.Town} to {destination.Town}."
                }
            });
        }

        return new MoveResidentBranchResult(
            true,
            null,
            branch.Select(person => person.Id).ToList(),
            origin,
            destination,
            transferredHouse);
    }

    private IReadOnlyList<IPerson> CollectResidentBranch(
        IPerson root,
        IReadOnlySet<Guid> residentIds,
        Guid sourceHeadId)
    {
        var result = new List<IPerson>();
        var seen = new HashSet<Guid>();

        void Add(IPerson person)
        {
            if (person.Id == sourceHeadId
                || !residentIds.Contains(person.Id)
                || !seen.Add(person.Id))
            {
                return;
            }

            result.Add(person);
        }

        void AddDependentChildren(IPerson parent)
        {
            foreach (var child in _family.GetChildren(parent))
            {
                if (!child.Tags.Has("state.alive")
                    || child.Age >= 18
                    || child.Id == sourceHeadId
                    || !residentIds.Contains(child.Id))
                {
                    continue;
                }

                Add(child);
                AddDependentChildren(child);
            }
        }

        Add(root);

        var spouse = _family.GetSpouse(root);
        if (spouse is not null
            && spouse.Tags.Has("state.alive")
            && spouse.Id != sourceHeadId
            && residentIds.Contains(spouse.Id))
        {
            Add(spouse);
            AddDependentChildren(spouse);
        }

        AddDependentChildren(root);
        return result;
    }

    private void EndResidentCaregiverRoles(
        IPerson sourceHead,
        IReadOnlyList<IPerson> movingMembers)
    {
        var nannyId = _economy.GetHousehold(sourceHead)?.NannyId;

        foreach (var member in movingMembers.Where(person =>
                     person.Tags.Has(FamilyNannyTracker.FamilyNannyTag)))
        {
            member.Tags.Remove(FamilyNannyTracker.FamilyNannyTag);

            if (nannyId == member.Id)
            {
                _economy.SetNanny(sourceHead, null);
                nannyId = null;
            }

            _events.Publish(new GameEvent
            {
                Type = "household.family_nanny_ended",
                Year = _gameState.Year,
                SubjectId = sourceHead.Id,
                RelatedPersonIds = [member.Id],
                Data = new Dictionary<string, string>
                {
                    ["reason"] = "they left the household",
                    ["text"] =
                        $"{_family.GetDisplayName(member)}'s {_career.GetStatusLabel(FamilyNannyTracker.FamilyNannyTag)} role ended because they left the household."
                }
            });
        }
    }

    private void RelocateResidentBranchEmployment(
        IReadOnlyList<IPerson> movingMembers,
        TownInfo destination)
    {
        foreach (var person in movingMembers.Where(person =>
                     person.Tags.Has("state.alive")
                     && person.Age >= 18
                     && !person.Tags.Has("role.nanny")
                     && !person.Tags.Has(FamilyNannyTracker.FamilyNannyTag)))
        {
            var oldCareer = _career.GetCareer(person);
            if (oldCareer.IsRetired
                || !oldCareer.IsEmployed
                || oldCareer.IsSelfEmployed)
            {
                continue;
            }

            var foundWork = _career.RelocateEmployment(person);
            var newCareer = _career.GetCareer(person);

            _events.Publish(new GameEvent
            {
                Type = "career.relocated",
                Year = _gameState.Year,
                SubjectId = person.Id,
                Data = new Dictionary<string, string>
                {
                    ["suppressChronicle"] = "true",
                    ["oldCareerId"] = oldCareer.CareerId ?? string.Empty,
                    ["newCareerId"] = newCareer.CareerId ?? string.Empty,
                    ["text"] = foundWork
                        ? $"After moving to {destination.Town}, {_family.GetDisplayName(person)} established new work as {newCareer.JobTitle}."
                        : $"After moving to {destination.Town}, {_family.GetDisplayName(person)} was unable to find replacement employment."
                }
            });
        }
    }

    private static MoveResidentBranchResult Failed(string reason) =>
        new(
            false,
            reason,
            Array.Empty<Guid>(),
            null,
            null,
            null);
}
