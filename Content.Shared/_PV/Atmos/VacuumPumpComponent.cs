using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._PV.Atmos;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VacuumPumpComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;

    /// <summary>
    /// Maximum amount of gas removed from the inlet pipe network each second.
    /// </summary>
    [DataField]
    public float MolesPerSecond = 2000f;
}

[Serializable, NetSerializable]
public enum VacuumPumpUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class VacuumPumpToggleMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class VacuumPumpUiState(bool enabled, bool powered) : BoundUserInterfaceState
{
    public readonly bool Enabled = enabled;
    public readonly bool Powered = powered;
}

[Serializable, NetSerializable]
public enum VacuumPumpVisuals : byte
{
    Enabled,
}
