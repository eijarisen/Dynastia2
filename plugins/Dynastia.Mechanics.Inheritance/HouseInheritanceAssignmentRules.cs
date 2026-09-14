namespace Dynastia.Mechanics.Inheritance;

public static class HouseInheritanceAssignmentRules
{
    public static IReadOnlyList<Guid> ResolveRecipients(
        IReadOnlyList<Guid> livingHeirIds,
        IReadOnlyList<Guid?> assignedHeirIds)
    {
        ArgumentNullException.ThrowIfNull(livingHeirIds);
        ArgumentNullException.ThrowIfNull(assignedHeirIds);

        if (livingHeirIds.Count == 0)
            return [];

        var living = livingHeirIds.ToHashSet();
        var recipients = new List<Guid>(assignedHeirIds.Count);
        var standardIndex = 0;

        foreach (var assignedHeirId in assignedHeirIds)
        {
            if (assignedHeirId is Guid selected
                && living.Contains(selected))
            {
                recipients.Add(selected);
                continue;
            }

            recipients.Add(
                livingHeirIds[
                    standardIndex
                    % livingHeirIds.Count]);

            standardIndex++;
        }

        return recipients;
    }
}
