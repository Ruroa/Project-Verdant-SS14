using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.LimbDamage.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class LimbTargettingComponent : Component
{
    [DataField, AutoNetworkedField]
    public LimbTarget Target = LimbTarget.Random;
}

[Serializable, NetSerializable]
public sealed class ChangeLimbTargetMessage(LimbTarget target) : EntityEventArgs
{
    public LimbTarget Target = target;
}
