using Robust.Shared.GameStates;

namespace Content.Shared._PV.Kitchen.Components;

/// <summary>
/// Table butchering yield
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class TableButcherableComponent : Component
{
    /// <summary>
    /// Yield multiplier
    /// </summary>
    [DataField, AutoNetworkedField]
    public float YieldMultiplier = 1.5f;

    /// <summary>
    /// Fractional yield remainder
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BonusAccumulator;
}
