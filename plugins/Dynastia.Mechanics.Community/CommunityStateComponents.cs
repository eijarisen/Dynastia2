using Dynastia.Contracts;

namespace Dynastia.Mechanics.Community;

[PersistedComponentId("community.world_state")]
public sealed class CommunityWorldStateComponent
{
    public List<CommunityTownPolicyState> Towns { get; set; } = [];
    public List<CommunityLobbyState> Lobbies { get; set; } = [];
    public List<CommunityConnectionState> Connections { get; set; } = [];
    public List<CivicOfficeTownState> CivicOffices { get; set; } = [];
}

public sealed class CommunityTownPolicyState
{
    public string TownId { get; set; } = string.Empty;
    public int LastResolvedProposalYear { get; set; } = int.MinValue;
    public List<CommunityActivePolicyState> ActivePolicies { get; set; } = [];
}

public sealed class CommunityActivePolicyState
{
    public string PolicyId { get; set; } = string.Empty;
    public int EnactedYear { get; set; }
    public int ExpiresAfterYear { get; set; }
}

public sealed class CommunityLobbyState
{
    public string TownId { get; set; } = string.Empty;
    public int ProposalYear { get; set; }
    public string PolicyId { get; set; } = string.Empty;
    public Guid ActorId { get; set; }
    public Guid HouseholdId { get; set; }
    public double SupportBonus { get; set; }
}

public sealed class CommunityConnectionState
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Sex Sex { get; set; }
    public int BirthYear { get; set; }
    public int? DeathYear { get; set; }
    public string NationalityId { get; set; } = string.Empty;
    public string TownId { get; set; } = string.Empty;
    public string OccupationLabel { get; set; } = string.Empty;
    public string ArchetypeId { get; set; } = string.Empty;
    public string WealthBand { get; set; } = "Modest";
    public double Renown { get; set; }
    public double Reputation { get; set; }
    public int Familiarity { get; set; } = 20;
    public int Sympathy { get; set; } = 5;
    public string? SpouseName { get; set; }
    public List<string> Children { get; set; } = [];
    public bool HasSpareHouse { get; set; }
    public bool HasSpareFarmland { get; set; }
    public string OriginPolicyId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

[PersistedComponentId("community.participation")]
public sealed class CommunityParticipationComponent
{
    public int Count { get; set; }
}


public sealed class CivicOfficeTownState
{
    public string TownId { get; set; } = string.Empty;
    public Guid? HeadPersonId { get; set; }
    public string NpcName { get; set; } = string.Empty;
    public Sex NpcSex { get; set; }
    public int NpcBirthYear { get; set; }
    public string NpcNationalityId { get; set; } = string.Empty;
    public double NpcRenown { get; set; }
    public double NpcReputation { get; set; }
    public double Approval { get; set; }
    public int OfficeStartYear { get; set; }
    public int LastOfficeActionYear { get; set; } = int.MinValue;
    public double PendingApprovalAdjustment { get; set; }
    public int LastProcessedYear { get; set; } = int.MinValue;
}
