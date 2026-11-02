using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._PV.Rovers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RoverDrillComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField]
    public ProtoId<SinkPortPrototype> TogglePort = "Toggle";

    [DataField]
    public ProtoId<SinkPortPrototype> OnPort = "On";

    [DataField]
    public ProtoId<SinkPortPrototype> OffPort = "Off";
}

[Serializable, NetSerializable]
public enum RoverDrillVisuals : byte
{
    Enabled,
}

[Serializable, NetSerializable]
public enum RoverDrillLayers : byte
{
    Drilling,
}
