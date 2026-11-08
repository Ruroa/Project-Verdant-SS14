using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Chat.Systems;
using Content.Server.DeviceLinking.Systems;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Server.Power.Components;
using Content.Shared._PV.Zpm;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceLinking;
using Content.Shared.NodeContainer;
using Content.Shared.Radio;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._PV.Zpm;

public sealed class ZpmSystem : EntitySystem
{
    private const string CoreSlotId = "coreSlot";
    private const string PipeNodeId = "pipe";
    private const string ZpmNodeId = "zpm";
    private static readonly TimeSpan UpdatePeriod = TimeSpan.FromSeconds(1);
    private const float BreachGraceSeconds = 10f;

    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly DeviceLinkSystem _deviceLink = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZpmReactorComponent, ComponentInit>(OnReactorInit);
        SubscribeLocalEvent<ZpmReactorComponent, AtmosDeviceUpdateEvent>(OnAtmosUpdate);
        SubscribeLocalEvent<ZpmControllerComponent, ComponentInit>(OnControllerInit);
        SubscribeLocalEvent<ZpmControllerComponent, ComponentRemove>(OnControllerRemove);
        SubscribeLocalEvent<ZpmControllerComponent, ItemSlotEjectAttemptEvent>(OnCoreEjectAttempt);
        SubscribeLocalEvent<ZpmControllerComponent, ItemSlotInsertAttemptEvent>(OnCoreInsertAttempt);
        SubscribeLocalEvent<ZpmControllerComponent, EntInsertedIntoContainerMessage>(OnCoreChanged);
        SubscribeLocalEvent<ZpmControllerComponent, EntRemovedFromContainerMessage>(OnCoreChanged);
        SubscribeLocalEvent<ZpmConsoleComponent, NewLinkEvent>(OnNewLink);
        SubscribeLocalEvent<ZpmConsoleComponent, PortDisconnectedEvent>(OnPortDisconnected);
        SubscribeLocalEvent<ZpmConsoleComponent, BoundUIOpenedEvent>(OnConsoleOpened);
        SubscribeLocalEvent<ZpmConsoleComponent, ZpmConsoleRefreshMessage>(OnConsoleRefresh);
        SubscribeLocalEvent<ZpmConsoleComponent, ZpmStartMessage>(OnStart);
        SubscribeLocalEvent<ZpmConsoleComponent, ZpmEjectCoreMessage>(OnEjectCore);
        SubscribeLocalEvent<ZpmConsoleComponent, ZpmSetPowerLevelMessage>(OnSetPower);
        SubscribeLocalEvent<ZpmConsoleComponent, ZpmShutdownMessage>(OnShutdown);
        SubscribeLocalEvent<ZpmConsoleComponent, SignalReceivedEvent>(OnConsoleSignal);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ZpmReactorComponent>();
        while (query.MoveNext(out var uid, out var reactor))
        {
            if (reactor.BreachFinalized)
                continue;

            if (reactor.Integrity <= 0f && reactor.BreachTime != TimeSpan.Zero && now >= reactor.BreachTime)
            {
                TriggerBreach(uid, reactor);
                continue;
            }

            if (now >= reactor.NextIntegrityUpdate)
            {
                reactor.NextIntegrityUpdate = now + UpdatePeriod;
                UpdateReactor(uid, reactor, 1f);
            }
        }

        var consoles = EntityQueryEnumerator<ZpmConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var console))
        {
            if (_ui.IsUiOpen(uid, ZpmConsoleUiKey.Key))
                UpdateConsole(uid, console);
        }
    }

    private void OnReactorInit(EntityUid uid, ZpmReactorComponent component, ComponentInit args)
    {
        UpdatePower(uid, component);
        UpdateReactorAppearance(uid, component);
    }

    private void OnControllerInit(EntityUid uid, ZpmControllerComponent component, ComponentInit args)
    {
        _itemSlots.AddItemSlot(uid, CoreSlotId, component.CoreSlot);
        component.CoreSlotInitialized = true;
        UpdateControllerAppearance(uid, component);
        if (component.Reactor is { } reactorUid)
            RefreshReactor(reactorUid);
    }

    private void OnControllerRemove(EntityUid uid, ZpmControllerComponent component, ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(uid, component.CoreSlot);
        component.CoreSlotInitialized = false;
    }

    private void OnCoreChanged(EntityUid uid, ZpmControllerComponent component, ContainerModifiedMessage args)
    {
        if (args.Container.ID != component.CoreSlot.ID)
            return;

        UpdateControllerAppearance(uid, component);
        if (component.Reactor is { } reactorUid)
            RefreshReactor(reactorUid);
    }

    private void OnCoreEjectAttempt(EntityUid uid, ZpmControllerComponent component, ref ItemSlotEjectAttemptEvent args)
    {
        if (component.Reactor is { } reactorUid && TryComp<ZpmReactorComponent>(reactorUid, out var reactor) && reactor.Online)
            args.Cancelled = true;
    }

    private void OnCoreInsertAttempt(EntityUid uid, ZpmControllerComponent component, ref ItemSlotInsertAttemptEvent args)
    {
        if (component.Reactor is { } reactorUid && TryComp<ZpmReactorComponent>(reactorUid, out var reactor) && reactor.Online)
            args.Cancelled = true;
    }

    private void OnAtmosUpdate(EntityUid uid, ZpmReactorComponent component, ref AtmosDeviceUpdateEvent args)
    {
        if (!component.Online || !TryGetCoreController(uid, out _, out var coreController) ||
            coreController.CoreSlot.Item == null || !TryGetPorts(uid, out var inlet, out var outlet))
            return;

        var input = inlet.Air;
        var output = outlet.Air;
        component.CoolantInletTemperature = input.Temperature;
        component.HeatOutletTemperature = output.Temperature;

        // The inlet pump points toward the reactor and the outlet pump points away.
        // Transfer is only possible when that loop has a real pressure gradient and gas flow.
        var transfer = GetPressureDrivenTransfer(input, output);
        var heatCapacity = _atmosphere.GetHeatCapacity(transfer, true);
        var heatHeadroom = MathF.Max(0f, component.MaxReturnTemperature - input.Temperature);
        var inletTemperatureFactor = Math.Clamp(
            (component.SafeInletTemperature - input.Temperature) /
            (component.SafeInletTemperature - component.IdealInletTemperature), 0f, 1f);
        var pressureFactor = Math.Clamp(
            (input.Pressure - output.Pressure) / component.RequiredPressureDifferential, 0f, 1f);
        // Heat capacity already scales with the amount of gas transferred. Applying a second
        // flow multiplier here counted low flow twice and made normal pump circulation appear
        // ineffective.
        var availableCooling = heatCapacity * heatHeadroom * inletTemperatureFactor * pressureFactor;
        var requestedHeat = GetHeatDemand(component.PowerLevel) * args.dt;
        component.CoolingEfficiency = requestedHeat <= 0f
            ? 100f
            : transfer.TotalMoles <= 0f ? 0f
            : Math.Clamp(availableCooling / requestedHeat * 100f, 0f, 100f);

        var appliedHeat = MathF.Min(availableCooling, requestedHeat);
        if (transfer.TotalMoles > 0f)
        {
            _atmosphere.AddHeat(transfer, appliedHeat);
            _atmosphere.Merge(output, transfer);
            component.HeatOutletTemperature = output.Temperature;
        }

    }

    private static GasMixture GetPressureDrivenTransfer(GasMixture inlet, GasMixture outlet)
    {
        var deltaPressure = inlet.Pressure - outlet.Pressure;
        var denominator = inlet.Temperature * outlet.Volume + outlet.Temperature * inlet.Volume;
        if (deltaPressure <= 0f || inlet.Pressure <= 0f || denominator <= 0f || inlet.TotalMoles <= 0f)
            return new GasMixture();

        var transferMoles = inlet.TotalMoles - (inlet.TotalMoles + outlet.TotalMoles) *
            outlet.Temperature * inlet.Volume / denominator;
        transferMoles = Math.Clamp(transferMoles, 0f, inlet.TotalMoles);
        return inlet.Remove(transferMoles);
    }

    private void UpdateReactor(EntityUid uid, ZpmReactorComponent reactor, float seconds)
    {
        var air = _atmosphere.GetContainingMixture(uid);
        reactor.ChamberPressure = air?.Pressure ?? 0f;
        var vacuumLost = reactor.ChamberPressure > reactor.VacuumLimit;
        var hasCore = TryGetCoreController(uid, out _, out var coreController) && coreController.CoreSlot.Item != null;

        if (reactor.Online && vacuumLost && !reactor.VacuumBreached)
        {
            reactor.VacuumBreached = true;
            SendEngineeringMessage(uid, "ZPM chamber vacuum breached. Integrity loss has begun.");
        }
        else if (!vacuumLost && reactor.VacuumBreached)
        {
            reactor.VacuumBreached = false;
            SendEngineeringMessage(uid, $"ZPM chamber vacuum restored; stabilizing at {reactor.Integrity:0}% integrity.");
        }

        if (reactor.Online && (!hasCore || reactor.BreachFinalized))
            SetOnline(uid, reactor, false);

        reactor.ShieldingEfficiency = GetShieldingEfficiency(uid);
        var coolingDeficit = MathF.Max(0f, 100f - reactor.CoolingEfficiency) / 100f;
        var shieldingDeficit = MathF.Max(0f, 100f - reactor.ShieldingEfficiency) / 100f;
        if (reactor.Online && reactor.ShieldingEfficiency <= 50f && !reactor.ShieldingWarningSent)
        {
            SendEngineeringMessage(uid, "ZPM shielding efficiency is at or below 50%. Repair the containment socket before the shielding fails.");
            reactor.ShieldingWarningSent = true;
        }
        else if (reactor.ShieldingEfficiency > 50f && reactor.ShieldingWarningSent)
        {
            SendEngineeringMessage(uid, $"ZPM shielding repaired to {reactor.ShieldingEfficiency:0}%; integrity recovery can resume when all other systems are stable.");
            reactor.ShieldingWarningSent = false;
        }
        if (reactor.Online && coolingDeficit > 0f)
        {
            var levelMultiplier = reactor.PowerLevel switch
            {
                ZpmPowerLevel.Low => 0.5f,
                ZpmPowerLevel.Medium => 0.75f,
                ZpmPowerLevel.Average => 1f,
                ZpmPowerLevel.High => 1.5f,
                _ => 1f,
            };
            reactor.Integrity -= reactor.IntegrityLossPerSecond * coolingDeficit * levelMultiplier * seconds;
        }

        if (reactor.Online && vacuumLost)
            reactor.Integrity -= reactor.VacuumIntegrityLossPerSecond * seconds;

        if (reactor.Online && shieldingDeficit > 0f)
            reactor.Integrity -= reactor.ShieldingIntegrityLossPerSecond * shieldingDeficit * seconds;

        if (!vacuumLost && shieldingDeficit <= 0f && (!reactor.Online || coolingDeficit <= 0f))
            reactor.Integrity += reactor.IntegrityRecoveryPerSecond * seconds;

        reactor.Integrity = Math.Clamp(reactor.Integrity, 0f, 100f);
        HandleIntegrityWarnings(uid, reactor);

        if (reactor.Integrity > 25f)
            reactor.CriticalAnnouncementSent = false;

        if (reactor.Integrity <= 0f && reactor.BreachTime == TimeSpan.Zero)
        {
            reactor.BreachTime = _timing.CurTime + TimeSpan.FromSeconds(BreachGraceSeconds);
            reactor.FinalBreachWarningSent = true;
            SendEngineeringMessage(uid, "ZPM INTEGRITY FIELD REACHED 0% — EVENT HORIZON COLLAPSE UNAVOIDABLE. Core breach in 10 seconds.");
            AnnounceStation(uid, "ZPM integrity has reached 0%. EVENT HORIZON COLLAPSE IS IMMINENT. Evacuate the reactor area immediately.");
        }
        else if (reactor.Integrity > 0f && reactor.BreachTime != TimeSpan.Zero)
        {
            reactor.BreachTime = TimeSpan.Zero;
            reactor.FinalBreachWarningSent = false;
        }

        UpdateReactorAppearance(uid, reactor);

        if (reactor.Online && (coolingDeficit > 0f || vacuumLost || shieldingDeficit > 0f))
        {
            reactor.WasUnstable = true;
        }
        else if (reactor.Online && reactor.WasUnstable)
        {
            SendEngineeringMessage(uid, $"ZPM conditions stabilized; integrity holding at {reactor.Integrity:0}%.");
            reactor.WasUnstable = false;
        }

        reactor.PreviousIntegrity = reactor.Integrity;
        if (TryGetCoreController(uid, out var controllerUid, out _))
            _itemSlots.SetLock(controllerUid, CoreSlotId, reactor.Online);
        UpdatePower(uid, reactor);
        PublishStatus(uid);
    }

    private void HandleIntegrityWarnings(EntityUid uid, ZpmReactorComponent reactor)
    {
        if (reactor.Integrity <= 25f)
        {
            if (!reactor.CriticalAnnouncementSent)
            {
                AnnounceStation(uid, "ZPM containment integrity has fallen to 25%. Things are FUCKED: engineering response is required immediately.");
                reactor.CriticalAnnouncementSent = true;
            }

            while (reactor.Integrity <= reactor.NextCriticalIntegrity && reactor.NextCriticalIntegrity > 0f)
            {
                SendEngineeringMessage(uid, $"ZPM CRITICAL: integrity at {reactor.NextCriticalIntegrity:0}%.");
                reactor.NextCriticalIntegrity -= 2f;
            }

            reactor.NextWarningIntegrity = 25f;
        }
        else if (reactor.Integrity <= 50f)
        {
            while (reactor.Integrity <= reactor.NextWarningIntegrity && reactor.NextWarningIntegrity > 25f)
            {
                SendEngineeringMessage(uid, $"ZPM warning: integrity at {reactor.NextWarningIntegrity:0}%.");
                reactor.NextWarningIntegrity -= 5f;
            }
        }
        else
        {
            reactor.NextWarningIntegrity = 50f;
            reactor.NextCriticalIntegrity = 25f;
        }
    }

    private void TriggerBreach(EntityUid uid, ZpmReactorComponent component)
    {
        component.BreachFinalized = true;
        SetOnline(uid, component, false);
        if (TryComp<TransformComponent>(uid, out var xform))
            Spawn("Singularity", xform.Coordinates);
        QueueDel(uid);
    }

    private void OnNewLink(EntityUid uid, ZpmConsoleComponent component, NewLinkEvent args)
    {
        if (args.Sink != uid || !TryComp<ZpmControllerComponent>(args.Source, out var controller) ||
            controller.Reactor is not { } reactorUid || !HasComp<ZpmReactorComponent>(reactorUid))
            return;

        component.LinkedController = args.Source;
        component.LinkedReactor = reactorUid;
        UpdateConsole(uid, component);
    }

    private void OnConsoleSignal(EntityUid uid, ZpmConsoleComponent component, ref SignalReceivedEvent args)
    {
        if (args.Port != "Toggle" || args.Trigger is not { } linked ||
            !TryComp<ZpmControllerComponent>(linked, out var controller) ||
            controller.Reactor is not { } reactorUid || !HasComp<ZpmReactorComponent>(reactorUid))
            return;

        component.LinkedController = linked;
        component.LinkedReactor = reactorUid;
        UpdateConsole(uid, component);
    }

    private void OnPortDisconnected(EntityUid uid, ZpmConsoleComponent component, PortDisconnectedEvent args)
    {
        if (args.Sink != uid || args.Source != component.LinkedController || args.Port != "Toggle")
            return;

        component.LinkedController = null;
        component.LinkedReactor = null;
        UpdateConsole(uid, component);
    }

    private void OnConsoleOpened(EntityUid uid, ZpmConsoleComponent component, BoundUIOpenedEvent args) => UpdateConsole(uid, component);
    private void OnConsoleRefresh(EntityUid uid, ZpmConsoleComponent component, ZpmConsoleRefreshMessage args) => UpdateConsole(uid, component);

    private void OnStart(EntityUid uid, ZpmConsoleComponent component, ZpmStartMessage args)
    {
        if (!TryGetLinkedReactor(component, out var reactorUid, out var reactor))
            return;

        var air = _atmosphere.GetContainingMixture(reactorUid);
        var group = GetNodeGroup(reactorUid);
        if (!TryGetCoreController(reactorUid, out _, out var coreController) || coreController.CoreSlot.Item == null ||
            air == null || air.Pressure > reactor.VacuumLimit || group is not { IsComplete: true })
            return;

        if (reactor.Integrity <= 25f)
            return;

        SetOnline(reactorUid, reactor, true);
        UpdateConsole(uid, component);
    }

    private void OnSetPower(EntityUid uid, ZpmConsoleComponent component, ZpmSetPowerLevelMessage args)
    {
        if (!TryGetLinkedReactor(component, out var reactorUid, out var reactor))
            return;

        reactor.PowerLevel = args.Level;
        UpdatePower(reactorUid, reactor);
        UpdateReactorAppearance(reactorUid, reactor);
        UpdateConsole(uid, component);
    }

    private void OnEjectCore(EntityUid uid, ZpmConsoleComponent component, ZpmEjectCoreMessage args)
    {
        if (!TryGetLinkedReactor(component, out var reactorUid, out var reactor) || reactor.Online ||
            !TryGetCoreController(reactorUid, out var controllerUid, out var coreController))
            return;

        _itemSlots.TryEject(controllerUid, coreController.CoreSlot, null, out _);
        UpdateConsole(uid, component);
    }

    private void OnShutdown(EntityUid uid, ZpmConsoleComponent component, ZpmShutdownMessage args)
    {
        if (!TryGetLinkedReactor(component, out var reactorUid, out var reactor))
            return;

        if (!args.Emergency && reactor.Integrity <= 50f)
            return;

        SetOnline(reactorUid, reactor, false);
        UpdateConsole(uid, component);
    }

    private void SetOnline(EntityUid uid, ZpmReactorComponent reactor, bool value)
    {
        reactor.Online = value;
        if (TryGetCoreController(uid, out var controllerUid, out _))
            _itemSlots.SetLock(controllerUid, CoreSlotId, value);
        if (!value)
            reactor.PowerOutput = 0f;
        UpdatePower(uid, reactor);
        UpdateReactorAppearance(uid, reactor);
    }

    private void UpdatePower(EntityUid uid, ZpmReactorComponent reactor)
    {
        reactor.PowerOutput = reactor.Online && TryGetCoreController(uid, out _, out var controller) &&
                              controller.CoreSlot.Item != null ? GetPowerOutput(reactor.PowerLevel) : 0f;
        if (TryComp<PowerSupplierComponent>(uid, out var supplier))
            supplier.MaxSupply = reactor.PowerOutput;
    }

    private static float GetPowerOutput(ZpmPowerLevel level) => level switch
    {
        ZpmPowerLevel.Low => 100_000f,
        ZpmPowerLevel.Medium => 250_000f,
        ZpmPowerLevel.Average => 500_000f,
        ZpmPowerLevel.High => 1_000_000f,
        _ => 100_000f,
    };

    private static float GetHeatDemand(ZpmPowerLevel level) => level switch
    {
        ZpmPowerLevel.Low => 25_000f,
        ZpmPowerLevel.Medium => 75_000f,
        ZpmPowerLevel.Average => 150_000f,
        ZpmPowerLevel.High => 300_000f,
        _ => 25_000f,
    };

    private bool TryGetPorts(EntityUid uid, out PipeNode inlet, out PipeNode outlet)
    {
        inlet = default!;
        outlet = default!;
        var group = GetNodeGroup(uid);
        if (group is not { IsComplete: true } ||
            !TryComp<NodeContainerComponent>(group.CoolingInlet!.Owner, out var inletContainer) ||
            !TryComp<NodeContainerComponent>(group.HeatOutlet!.Owner, out var outletContainer) ||
            !inletContainer.Nodes.TryGetValue(PipeNodeId, out var inletNode) || inletNode is not PipeNode inletPipe ||
            !outletContainer.Nodes.TryGetValue(PipeNodeId, out var outletNode) || outletNode is not PipeNode outletPipe)
            return false;

        inlet = inletPipe;
        outlet = outletPipe;
        return true;
    }

    private ZpmNodeGroup? GetNodeGroup(EntityUid uid)
    {
        if (!TryComp<NodeContainerComponent>(uid, out var container) ||
            !container.Nodes.TryGetValue(ZpmNodeId, out var node) ||
            node.NodeGroup is not ZpmNodeGroup group)
            return null;

        return group;
    }

    public void RefreshReactor(EntityUid uid)
    {
        if (TryComp<ZpmReactorComponent>(uid, out var reactor))
        {
            if (reactor.Online && (!TryGetCoreController(uid, out _, out var coreController) || coreController.CoreSlot.Item == null))
                SetOnline(uid, reactor, false);

            UpdatePower(uid, reactor);
            if (TryGetCoreController(uid, out var controllerUid, out _))
                _itemSlots.SetLock(controllerUid, CoreSlotId, reactor.Online);

            if (GetNodeGroup(uid) is { } group)
            {
                foreach (var controller in group.Controllers)
                    RefreshController(controller.Owner, uid);
            }
        }
    }

    public void RefreshController(EntityUid controllerUid, EntityUid? reactorUid)
    {
        if (!TryComp<ZpmControllerComponent>(controllerUid, out var controller))
            return;

        controller.Reactor = reactorUid;
        UpdateControllerAppearance(controllerUid, controller);
        if (controller.CoreSlotInitialized)
            _itemSlots.SetLock(controllerUid, CoreSlotId,
                reactorUid is { } reactor && TryComp<ZpmReactorComponent>(reactor, out var reactorComp) && reactorComp.Online);
    }

    private bool TryGetCoreController(EntityUid reactorUid, out EntityUid controllerUid, out ZpmControllerComponent controller)
    {
        controllerUid = default;
        controller = default!;
        if (GetNodeGroup(reactorUid) is not { } group)
            return false;

        foreach (var node in group.Controllers)
        {
            if (!TryComp<ZpmControllerComponent>(node.Owner, out var found) || found == null || !found.CoreSlotInitialized)
                continue;

            controllerUid = node.Owner;
            controller = found;
            return true;
        }

        return false;
    }

    private float GetShieldingEfficiency(EntityUid reactorUid)
    {
        if (GetNodeGroup(reactorUid) is not { } group)
            return 100f;

        const int expectedShieldingParts = 8;
        var efficiency = 0f;
        foreach (var part in group.ShieldingParts)
        {
            if (!TryComp<DamageableComponent>(part.Owner, out var damageable))
            {
                efficiency += 1f;
                continue;
            }

            efficiency += Math.Clamp(1f - damageable.TotalDamage.Float() / 100f, 0f, 1f);
        }

        return Math.Clamp(efficiency / expectedShieldingParts * 100f, 0f, 100f);
    }

    private void UpdateControllerAppearance(EntityUid uid, ZpmControllerComponent controller)
    {
        _appearance.SetData(uid, ZpmControllerVisuals.CoreInserted, controller.CoreSlot.Item != null);
    }

    private void UpdateReactorAppearance(EntityUid uid, ZpmReactorComponent reactor)
    {
        var low = reactor.PowerLevel is ZpmPowerLevel.Low or ZpmPowerLevel.Medium;
        var state = !reactor.Online ? ZpmPulseVisual.Offline
            : reactor.Integrity <= 25f
                ? low ? ZpmPulseVisual.CriticalLow : ZpmPulseVisual.CriticalHigh
            : reactor.Integrity <= 50f
                ? low ? ZpmPulseVisual.WarningLow : ZpmPulseVisual.WarningHigh
            : low ? ZpmPulseVisual.StableLow : ZpmPulseVisual.StableHigh;

        if (reactor.CurrentPulseVisual == state)
            return;

        reactor.CurrentPulseVisual = state;
        _appearance.SetData(uid, ZpmReactorVisuals.Pulse, state);
    }

    private void AnnounceStation(EntityUid source, string message)
    {
        var station = _station.GetOwningStation(source) ?? source;
        _chat.DispatchStationAnnouncement(station, message, "ZPM CONTAINMENT", false, null, Color.Red);
    }

    private bool TryGetLinkedReactor(ZpmConsoleComponent console, out EntityUid uid, out ZpmReactorComponent reactor)
    {
        uid = default;
        reactor = default!;
        if (console.LinkedReactor is not { } linked || !Exists(linked) ||
            !TryComp<ZpmReactorComponent>(linked, out var found) || found == null)
            return false;

        uid = linked;
        reactor = found;
        return true;
    }

    private void UpdateConsole(EntityUid uid, ZpmConsoleComponent component)
    {
        if (!_ui.HasUi(uid, ZpmConsoleUiKey.Key))
            return;

        ResolveLinkedReactor(uid, component);

        if (!TryGetLinkedReactor(component, out var reactorUid, out var reactor))
        {
            _ui.SetUiState(uid, ZpmConsoleUiKey.Key, new ZpmConsoleBoundInterfaceState(false));
            return;
        }

        var condition = !reactor.Online
            ? ZpmCondition.Offline
            : reactor.Integrity <= 25f ? ZpmCondition.Critical
            : reactor.Integrity <= 50f ? ZpmCondition.Warning
            : ZpmCondition.Stable;

        var state = new ZpmConsoleBoundInterfaceState(
            true,
            TryGetCoreController(reactorUid, out _, out var coreController) && coreController.CoreSlot.Item != null,
            reactor.Online,
            condition,
            reactor.PowerLevel,
            reactor.Integrity,
            reactor.CoolingEfficiency,
            reactor.ShieldingEfficiency,
            reactor.ChamberPressure,
            reactor.CoolantInletTemperature,
            reactor.HeatOutletTemperature,
            reactor.PowerOutput,
            TryComp<PowerSupplierComponent>(reactorUid, out var supplier) ? supplier.CurrentSupply : 0f);
        _ui.SetUiState(uid, ZpmConsoleUiKey.Key, state);
    }

    private void ResolveLinkedReactor(EntityUid consoleUid, ZpmConsoleComponent component)
    {
        if (component.LinkedController is { } linkedController && Exists(linkedController) &&
            TryComp<ZpmControllerComponent>(linkedController, out var linkedComp) &&
            linkedComp.Reactor is { } linkedReactor && Exists(linkedReactor))
        {
            component.LinkedReactor = linkedReactor;
            return;
        }

        component.LinkedController = null;
        component.LinkedReactor = null;
        if (!TryComp<DeviceLinkSinkComponent>(consoleUid, out var sink))
            return;

        foreach (var sourceUid in sink.LinkedSources)
        {
            if (!TryComp<ZpmControllerComponent>(sourceUid, out var controller) ||
                controller.Reactor is not { } reactorUid || !HasComp<ZpmReactorComponent>(reactorUid) ||
                !TryComp<DeviceLinkSourceComponent>(sourceUid, out var source))
                continue;

            foreach (var (sourcePort, sinkPort) in _deviceLink.GetLinks(sourceUid, consoleUid, source))
            {
                if (sourcePort != "Status" || sinkPort != "Toggle")
                    continue;

                component.LinkedController = sourceUid;
                component.LinkedReactor = reactorUid;
                return;
            }
        }
    }

    private void PublishStatus(EntityUid reactorUid)
    {
        if (GetNodeGroup(reactorUid) is not { } group)
            return;

        foreach (var controller in group.Controllers)
        {
            if (HasComp<DeviceLinkSourceComponent>(controller.Owner))
                _deviceLink.SendSignal(controller.Owner, "Status", true);
        }
    }

    private void SendEngineeringMessage(EntityUid source, string message)
    {
        var channel = _prototypes.Index<RadioChannelPrototype>("Engineering");
        _radio.SendRadioMessage(source, message, channel, source);
    }
}
