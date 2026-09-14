using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilySupport;

/// <summary>
/// Compatibility shell for deployments that still include the legacy
/// Family Support plugin. Cross-household support actions are owned by
/// Dynastia.Mechanics.FamilyRelations.
/// </summary>
public sealed class FamilySupportPlugin :
    IGamePlugin
{
    public void Initialize(
        IGamePluginContext context)
    {
        context.Log(
            "Legacy family-support actions are provided by Family Relations.");
    }
}
