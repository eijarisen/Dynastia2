# Design Decisions

This is an append-only decision log. Durable implemented rules belong in the relevant domain document; this file keeps the reason/history and points to the final rule.

## Entry format

```text
ID: GD-YYYY-NNN
Status: Proposed | Approved | Implemented | Superseded
Date:
Area:
Decision:
Reasoning:
Affected mechanics:
Implementation notes:
Save compatibility:
Documentation to update:
Supersedes:
```

---

ID: **GD-2026-001**  
Status: **Implemented**  
Date: **2026-09-22**  
Area: **Documentation / Instructions**  
Decision: Maintain a compact seven-tab player Instructions guide separately from a modular developer-facing `docs/GameDesign/` reference.  
Reasoning: Player help should orient without exposing hidden formulas; future design/implementation chats need exact current rules, ownership, interactions, persistence and test pointers without reconstructing conversation history.  
Affected mechanics: all, documentation only.  
Implementation notes: player guide is `src/Dynastia.App/Views/InstructionsWindow.axaml`; developer index starts at `docs/GameDesign/00-Index.md`; technical ownership remains in `docs/DevelopmentMap.md`.  
Save compatibility: none.  
Documentation to update: the owning domain file for every future durable gameplay change; `08-Actions-and-UI-Surfaces.md` when actions/surfaces change.  
Supersedes: conversation-dependent documentation as the normal starting point for future work.

---

ID: **GD-2026-002**  
Status: **Implemented**  
Date: **2026-09-22**  
Area: **Community / Performance**  
Decision: Full annual Community policy resolution is only required for towns containing playable lineage households and towns with an outstanding player lobby; irrelevant towns are skipped.  
Reasoning: Simulating every historical town provided no player-facing benefit and made `community.policy_resolution` disproportionately slow.  
Affected mechanics: `dynastia.community`, Town Affairs Community.  
Implementation notes: `CommunityPolicyService` builds the set of relevant town IDs before proposal/policy resolution.  
Save compatibility: none.  
Documentation to update: `06-Towns-Institutions-and-Community.md`.  
Supersedes: implicit full-world policy iteration.

---

ID: **GD-2026-003**  
Status: **Implemented**  
Date: **2026-09-22**  
Area: **Community connections**  
Decision: Accepting a requested spare house/farmland establishes a Warm connection, and Warm/Close acquaintances are not automatically discarded merely because the donor became Poor after helping.  
Reasoning: A major accepted favor should strengthen, not terminate, the social connection.  
Affected mechanics: `dynastia.community`.  
Implementation notes: connection action/result and annual retention rules share the revised relationship behavior.  
Save compatibility: no schema migration.  
Documentation to update: `06-Towns-Institutions-and-Community.md`.  
Supersedes: prior post-gift relation penalties/poor-contact cleanup behavior.
