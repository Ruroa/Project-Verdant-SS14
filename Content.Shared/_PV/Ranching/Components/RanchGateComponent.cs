using Content.Shared.Whitelist;

namespace Content.Shared._PV.Ranching.Components;

/// <summary>
/// Livestock gate rules
/// </summary>
[RegisterComponent]
public sealed partial class RanchGateComponent : Component
{
    /// <summary>
    /// Blocked livestock
    /// </summary>
    [DataField(required: true)]
    public EntityWhitelist AnimalWhitelist = default!;

    /// <summary>
    /// Gate collision state
    /// </summary>
    [ViewVariables]
    public bool AnimalBarrierActive;
}
