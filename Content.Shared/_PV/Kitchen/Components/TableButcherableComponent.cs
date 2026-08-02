using Robust.Shared.GameStates;

namespace Content.Shared._PV.Kitchen.Components;

/// <summary>
/// Marks a carcass as valid for processing on a butchering table.
/// The table uses the entity's normal Butcherable drops and applies this yield multiplier.
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
