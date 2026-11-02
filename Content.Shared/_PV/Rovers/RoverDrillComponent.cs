using Content.Shared.DeviceLinking;
using Content.Shared.Damage;
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
    public DamageSpecifier Damage = new();

    /// <summary>Seconds between automatic hits while the drill is enabled.</summary>
    [DataField]
    public float AttackInterval = 0.5f;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextAttack;

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
