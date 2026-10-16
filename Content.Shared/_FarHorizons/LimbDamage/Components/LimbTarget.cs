using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.LimbDamage.Components;

[Serializable, NetSerializable]
public enum LimbTarget : byte
{
    Random,
    Torso,
    Head,
    LeftArm,
    RightArm,
    LeftHand,
    RightHand,
    LeftLeg,
    RightLeg,
    LeftFoot,
    RightFoot,
}
