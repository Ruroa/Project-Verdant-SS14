using Content.Shared._PV.Zpm;
using Content.Shared.Containers.ItemSlots;

namespace Content.Server._PV.Zpm;

[RegisterComponent]
public sealed partial class ZpmReactorComponent : Component
{
    [DataField]
    public bool Online;

    [DataField]
    public ZpmPowerLevel PowerLevel = ZpmPowerLevel.Low;

    [DataField]
    public float Integrity = 100f;

    [DataField]
    public float CoolingEfficiency;

    [DataField]
    public float ShieldingEfficiency = 100f;

    [DataField]
    public float ChamberPressure;

    [DataField]
    public float CoolantInletTemperature;

    [DataField]
    public float HeatOutletTemperature;

    [DataField]
    public float PowerOutput;

    [DataField]
    public float SafeInletTemperature = 293.15f;

    [DataField]
    public float IdealInletTemperature = 73.15f;

    [DataField]
    public float MaxReturnTemperature = 600f;

    [DataField]
    public float RequiredPressureDifferential = 1_000f;

    [DataField]
    public float VacuumLimit = 5_000f;

    [DataField]
    public float IntegrityLossPerSecond = 0.2f;

    [DataField]
    public float IntegrityRecoveryPerSecond = 0.5f;

    [DataField]
    public float ShieldingIntegrityLossPerSecond = 0.5f;

    [DataField]
    public float VacuumIntegrityLossPerSecond = 1f;

    [DataField]
    public float StableHeatPerSecond = 100_000f;

    [DataField]
    public TimeSpan NextIntegrityUpdate;

    [DataField]
    public TimeSpan BreachTime;

    [DataField]
    public float NextWarningIntegrity = 50f;

    [DataField]
    public float NextCriticalIntegrity = 25f;

    [DataField]
    public float PreviousIntegrity = 100f;

    [DataField]
    public bool FinalBreachWarningSent;

    [DataField]
    public bool VacuumBreached;

    [DataField]
    public bool WasUnstable;

    [DataField]
    public bool BreachFinalized;

    [DataField]
    public bool CriticalAnnouncementSent;

    [DataField]
    public bool ShieldingWarningSent;

    [ViewVariables]
    public ZpmPulseVisual? CurrentPulseVisual;
}

[RegisterComponent]
public sealed partial class ZpmConsoleComponent : Component
{
    [ViewVariables]
    public EntityUid? LinkedReactor;

    [ViewVariables]
    public EntityUid? LinkedController;
}

[RegisterComponent]
public sealed partial class ZpmControllerComponent : Component
{
    [DataField]
    public ItemSlot CoreSlot = new();

    [ViewVariables]
    public bool CoreSlotInitialized;

    [ViewVariables]
    public EntityUid? Reactor;
}
