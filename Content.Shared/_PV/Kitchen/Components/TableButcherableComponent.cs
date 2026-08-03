using Robust.Shared.GameStates;

namespace Content.Shared._PV.Kitchen.Components;

/// <summary>
/// Stores the additional yield state used while processing a carcass on a butchering table.
/// The table adds this component automatically to dead mobs that support normal knife butchering.
/// Normal floor butchering remains unchanged.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class TableButcherableComponent : Component
{
    /// <summary>
    /// Multiplier applied to the entity's normal butchering yield.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float YieldMultiplier = 1.5f;

    /// <summary>
    /// Runtime accumulator used to distribute fractional bonus products across repeated cuts.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BonusAccumulator;
}
