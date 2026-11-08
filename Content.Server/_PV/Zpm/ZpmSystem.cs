using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.DeviceLinking.Systems;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Radio.EntitySystems;
using Content.Server.Power.Components;
using Content.Shared._PV.Zpm;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceLinking;
using Content.Shared.NodeContainer;
using Content.Shared.Radio;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;
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
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZpmReactorComponent, ComponentInit>(OnReactorInit);
        SubscribeLocalEvent<ZpmReactorComponent, ComponentRemove>(OnReactorRemove);
        SubscribeLocalEvent<ZpmReactorComponent, ItemSlotEjectAttemptEvent>(OnCoreEjectAttempt);
        SubscribeLocalEvent<ZpmReactorComponent, ItemSlotInsertAttemptEvent>(OnCoreInsertAttempt);
        SubscribeLocalEvent<ZpmReactorComponent, EntInsertedIntoContainerMessage>(OnCoreChanged);
        SubscribeLocalEvent<ZpmReactorComponent, EntRemovedFromContainerMessage>(OnCoreChanged);
        SubscribeLocalEvent<ZpmReactorComponent, AtmosDeviceUpdateEvent>(OnAtmosUpdate);
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
        _itemSlots.AddItemSlot(uid, CoreSlotId, component.CoreSlot);
        _itemSlots.SetLock(uid, CoreSlotId, component.Online);
        UpdatePower(uid, component);
    }

    private void OnReactorRemove(EntityUid uid, ZpmReactorComponent component, ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(uid, component.CoreSlot);
    }

    private void OnCoreChanged(EntityUid uid, ZpmReactorComponent component, ContainerModifiedMessage args)
    {
        if (args.Container.ID != component.CoreSlot.ID)
            return;

        RefreshReactor(uid);
    }

    private void OnCoreEjectAttempt(EntityUid uid, ZpmReactorComponent component, ref ItemSlotEjectAttemptEvent args)
    {
        if (component.Online)
            args.Cancelled = true;
    }

    private void OnCoreInsertAttempt(EntityUid uid, ZpmReactorComponent component, ref ItemSlotInsertAttemptEvent args)
    {
        if (component.Online)
            args.Cancelled = true;
    }

    private void OnAtmosUpdate(EntityUid uid, ZpmReactorComponent component, ref AtmosDeviceUpdateEvent args)
    {
        if (!component.Online || component.CoreSlot.Item == null || !TryGetPorts(uid, out var inlet, out var outlet))
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
        var flowFactor = Math.Clamp(transfer.TotalMoles / component.MinCoolantMoles, 0f, 1f);
        var availableCooling = heatCapacity * heatHeadroom * inletTemperatureFactor * pressureFactor * flowFactor;
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
        var hasCore = reactor.CoreSlot.Item != null;

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

        var coolingDeficit = MathF.Max(0f, 100f - reactor.CoolingEfficiency) / 100f;
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

        reactor.Integrity = Math.Clamp(reactor.Integrity, 0f, 100f);
        HandleIntegrityWarnings(uid, reactor);

        if (reactor.Integrity <= 0f && reactor.BreachTime == TimeSpan.Zero)
        {
            reactor.BreachTime = _timing.CurTime + TimeSpan.FromSeconds(BreachGraceSeconds);
            reactor.FinalBreachWarningSent = true;
            SendEngineeringMessage(uid, "ZPM INTEGRITY FIELD REACHED 0% — EVENT HORIZON COLLAPSE UNAVOIDABLE. Core breach in 10 seconds.");
        }

        if (reactor.Online && (coolingDeficit > 0f || vacuumLost))
        {
            reactor.WasUnstable = true;
        }
        else if (reactor.Online && reactor.WasUnstable)
        {
            SendEngineeringMessage(uid, $"ZPM conditions stabilized; integrity holding at {reactor.Integrity:0}%.");
            reactor.WasUnstable = false;
        }

        reactor.PreviousIntegrity = reactor.Integrity;
        _itemSlots.SetLock(uid, CoreSlotId, reactor.Online);
        UpdatePower(uid, reactor);
        PublishStatus(uid);
    }

    private void HandleIntegrityWarnings(EntityUid uid, ZpmReactorComponent reactor)
    {
        if (reactor.Integrity <= 25f)
        {
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
        if (reactor.CoreSlot.Item == null || air == null || air.Pressure > reactor.VacuumLimit || group is not { IsComplete: true })
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
        UpdateConsole(uid, component);
    }

    private void OnEjectCore(EntityUid uid, ZpmConsoleComponent component, ZpmEjectCoreMessage args)
    {
        if (!TryGetLinkedReactor(component, out var reactorUid, out var reactor) || reactor.Online)
            return;

        _itemSlots.TryEject(reactorUid, reactor.CoreSlot, null, out _);
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
        _itemSlots.SetLock(uid, CoreSlotId, value);
        if (!value)
            reactor.PowerOutput = 0f;
        UpdatePower(uid, reactor);
    }

    private void UpdatePower(EntityUid uid, ZpmReactorComponent reactor)
    {
        reactor.PowerOutput = reactor.Online && reactor.CoreSlot.Item != null ? GetPowerOutput(reactor.PowerLevel) : 0f;
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
            UpdatePower(uid, reactor);
            _itemSlots.SetLock(uid, CoreSlotId, reactor.Online);

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
        var hasCore = reactorUid is { } reactor &&
                      TryComp<ZpmReactorComponent>(reactor, out var reactorComp) &&
                      reactorComp.CoreSlot.Item != null;
        _appearance.SetData(controllerUid, ZpmControllerVisuals.CoreInserted, hasCore);
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

        if (!TryGetLinkedReactor(component, out _, out var reactor))
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
            reactor.CoreSlot.Item != null,
            reactor.Online,
            condition,
            reactor.PowerLevel,
            reactor.Integrity,
            reactor.CoolingEfficiency,
            reactor.ChamberPressure,
            reactor.CoolantInletTemperature,
            reactor.HeatOutletTemperature,
            reactor.PowerOutput);
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
