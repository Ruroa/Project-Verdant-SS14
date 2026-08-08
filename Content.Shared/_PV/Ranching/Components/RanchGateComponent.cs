using Content.Shared.Whitelist;

namespace Content.Shared._PV.Ranching.Components;

/// <summary>
/// Keeps configured livestock from crossing an open ranch gate unless the animal is being pulled.
/// </summary>
[RegisterComponent]
public sealed partial class RanchGateComponent : Component
{
    /// <summary>
    /// Animals blocked by the gate while it is open.
    /// </summary>
    [DataField(required: true)]
    public EntityWhitelist AnimalWhitelist = default!;

    /// <summary>
    /// True while this component is maintaining selective livestock collision for an open door.
    /// </summary>
    [ViewVariables]
    public bool AnimalBarrierActive;
}
