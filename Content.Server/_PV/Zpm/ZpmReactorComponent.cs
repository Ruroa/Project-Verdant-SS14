using Content.Shared._PV.Zpm;
using Content.Shared.Containers.ItemSlots;

namespace Content.Server._PV.Zpm;

[RegisterComponent]
public sealed partial class ZpmReactorComponent : Component
{
    [DataField]
    public bool Online;

    [DataField]
    public int OutputPercent;

    [DataField]
    public float Integrity = 100f;

    [DataField]
    public float ShieldingEfficiency = 100f;

    [DataField]
    public float ChamberPressure;

    [DataField]
    public float CoolantInletTemperature;

    [DataField]
    public float HeatOutletTemperature;

    /// <summary>
    /// Heat stored in the reactor core. The core continues heating while online even if the
    /// coolant loop is disconnected; coolant flow removes this stored heat.
    /// </summary>
    [DataField]
    public float StoredCoreHeat;

    [DataField]
    public float CoreTemperature = 293.15f;

    [DataField]
    public float PowerOutput;

    [DataField]
    public float MaxReturnTemperature = 2_000f;

    [DataField]
    // Oxygen is the common starting coolant. At 500 mol/s it can carry the reactor's
    // maximum 300 kW heat load while keeping the core in the blue/green range.
    public float CoolantFlowMolesPerSecond = 500f;

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
