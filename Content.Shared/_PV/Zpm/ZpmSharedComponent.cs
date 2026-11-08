using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._PV.Zpm;

[RegisterComponent, NetworkedComponent]
public sealed partial class ZpmCoreComponent : Component
{
}

[Serializable, NetSerializable]
public enum ZpmConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum ZpmPowerLevel : byte
{
    Low,
    Medium,
    Average,
    High,
}

[Serializable, NetSerializable]
public enum ZpmCondition : byte
{
    Offline,
    Stable,
    Warning,
    Critical,
    Breached,
}

[Serializable, NetSerializable]
public enum ZpmControllerVisuals : byte
{
    CoreInserted,
}

[Serializable, NetSerializable]
public enum ZpmReactorVisuals : byte
{
    Pulse,
}

[Serializable, NetSerializable]
public enum ZpmPulseVisual : byte
{
    Offline,
    StableLow,
    StableHigh,
    WarningLow,
    WarningHigh,
    CriticalLow,
    CriticalHigh,
}

[Serializable, NetSerializable]
public sealed class ZpmConsoleRefreshMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class ZpmStartMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class ZpmEjectCoreMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class ZpmSetPowerLevelMessage(ZpmPowerLevel level) : BoundUserInterfaceMessage
{
    public readonly ZpmPowerLevel Level = level;
}

[Serializable, NetSerializable]
public sealed class ZpmShutdownMessage(bool emergency) : BoundUserInterfaceMessage
{
    public readonly bool Emergency = emergency;
}

[Serializable, NetSerializable]
public sealed class ZpmConsoleBoundInterfaceState : BoundUserInterfaceState
{
    public readonly bool Linked;
    public readonly bool HasCore;
    public readonly bool Online;
    public readonly ZpmCondition Condition;
    public readonly ZpmPowerLevel PowerLevel;
    public readonly float Integrity;
    public readonly float CoolingEfficiency;
    public readonly float ShieldingEfficiency;
    public readonly float ChamberPressure;
    public readonly float CoolantInletTemperature;
    public readonly float HeatOutletTemperature;
    public readonly float PowerOutput;
    public readonly float PowerSupplied;

    public ZpmConsoleBoundInterfaceState(
        bool linked,
        bool hasCore = false,
        bool online = false,
        ZpmCondition condition = ZpmCondition.Offline,
        ZpmPowerLevel powerLevel = ZpmPowerLevel.Low,
        float integrity = 100f,
        float coolingEfficiency = 0f,
        float shieldingEfficiency = 100f,
        float chamberPressure = 0f,
        float coolantInletTemperature = 0f,
        float heatOutletTemperature = 0f,
        float powerOutput = 0f,
        float powerSupplied = 0f)
    {
        Linked = linked;
        HasCore = hasCore;
        Online = online;
        Condition = condition;
        PowerLevel = powerLevel;
        Integrity = integrity;
        CoolingEfficiency = coolingEfficiency;
        ShieldingEfficiency = shieldingEfficiency;
        ChamberPressure = chamberPressure;
        CoolantInletTemperature = coolantInletTemperature;
        HeatOutletTemperature = heatOutletTemperature;
        PowerOutput = powerOutput;
        PowerSupplied = powerSupplied;
    }
}
