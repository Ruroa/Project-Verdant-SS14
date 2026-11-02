using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._PV.Rovers;

[RegisterComponent, NetworkedComponent]
public sealed partial class RoverWheelComponent : Component
{
    [DataField]
    public RoverWheelDriveDirection DriveDirection = RoverWheelDriveDirection.Any;
}

[Serializable, NetSerializable]
public enum RoverWheelDriveDirection : byte
{
    Any,
    North,
    East,
    South,
    West,
}

[Serializable, NetSerializable]
public enum RoverWheelVisuals : byte
{
    Moving,
    DriveActive,
}

[Serializable, NetSerializable]
public enum RoverWheelLayers : byte
{
    Wheels,
    Drive,
}
