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
using Robust.Shared.Audio;
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
    // Lower capacity makes the core respond visibly to a working coolant loop instead of
    // needing several megajoules to move its displayed temperature by a useful amount.
    private const float CoreHeatCapacity = 10_000f;
    private const float BaseCoreTemperature = 293.15f;
    private const float MaximumCoreTemperature = 2_000f;
    // The console's 0-1500 K core gauge starts its red band at the final 1/8 of the scale.
    private const float CoreRedTemperature = 1_312.5f;
    private const float PassiveCoreCoolingPerSecond = 5_000f;
    private const float CoreYellowTemperature = 937.5f;
    private const float CoreGreenTemperature = 562.5f;

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
        SubscribeLocalEvent<ZpmConsoleComponent, ZpmSetOutputPercentMessage>(OnSetOutputPercent);
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
            coreController.CoreSlot.Item == null)
            return;

        // The core generates heat independently of the plumbing. This makes a disconnected
        // reactor visibly heat up instead of silently producing neither heat nor a failure.
        component.StoredCoreHeat += GetHeatDemand(component.OutputPercent) * args.dt;
        var removedHeat = 0f;
        component.CoolantInletTemperature = 0f;
        component.HeatOutletTemperature = 0f;

        if (TryGetPorts(uid, out var inlet, out var outlet))
        {
            var input = inlet.Air;
            var output = outlet.Air;
            component.CoolantInletTemperature = input.Temperature;
            component.HeatOutletTemperature = output.Temperature;

            // The pipe pumps move coolant around the loop. The reactor only exchanges
            // heat: it adds energy to the hot outlet gas and removes the same energy from
            // the core. Pressure is not used to decide whether thermal exchange happens.
            if (input.TotalMoles > 0f && output.TotalMoles > 0f && component.StoredCoreHeat > 0f)
            {
                var inletHeatCapacity = _atmosphere.GetHeatCapacity(input, true);
                var outletHeatCapacity = _atmosphere.GetHeatCapacity(output, true);
                var hottestAllowedReturn = MathF.Min(component.MaxReturnTemperature, component.CoreTemperature);
                var inletHeatHeadroom = MathF.Max(0f, hottestAllowedReturn - input.Temperature);
                var outletHeatHeadroom = MathF.Max(0f, hottestAllowedReturn - output.Temperature);
                var heatAvailableToCoolant = inletHeatCapacity * inletHeatHeadroom;
                var outletHeatCapacityHeadroom = outletHeatCapacity * outletHeatHeadroom;
                removedHeat = MathF.Min(component.StoredCoreHeat,
                    MathF.Min(heatAvailableToCoolant, outletHeatCapacityHeadroom));

                if (removedHeat > 0f)
                    _atmosphere.AddHeat(output, removedHeat);

                component.HeatOutletTemperature = output.Temperature;
            }
        }

        component.StoredCoreHeat -= removedHeat;
        component.CoreTemperature = Math.Clamp(
            BaseCoreTemperature + component.StoredCoreHeat / CoreHeatCapacity,
            1f, MaximumCoreTemperature);
        component.StoredCoreHeat = (component.CoreTemperature - BaseCoreTemperature) * CoreHeatCapacity;
        UpdatePower(uid, component);

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

        if (!reactor.Online && reactor.StoredCoreHeat > 0f)
        {
            reactor.StoredCoreHeat = MathF.Max(0f, reactor.StoredCoreHeat - PassiveCoreCoolingPerSecond * seconds);
            reactor.CoreTemperature = Math.Clamp(
                BaseCoreTemperature + reactor.StoredCoreHeat / CoreHeatCapacity,
                BaseCoreTemperature, MaximumCoreTemperature);
        }
        else if (!reactor.Online && reactor.StoredCoreHeat < 0f)
        {
            reactor.StoredCoreHeat = MathF.Min(0f, reactor.StoredCoreHeat + PassiveCoreCoolingPerSecond * seconds);
            reactor.CoreTemperature = Math.Clamp(
                BaseCoreTemperature + reactor.StoredCoreHeat / CoreHeatCapacity,
                1f, BaseCoreTemperature);
        }

        reactor.ShieldingEfficiency = GetShieldingEfficiency(uid);
        var thermalStress = Math.Clamp(
            (reactor.CoreTemperature - CoreRedTemperature) / (MaximumCoreTemperature - CoreRedTemperature), 0f, 1f);
        var yellowThermalStress = reactor.CoreTemperature >= CoreYellowTemperature;
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
        if (reactor.Online && thermalStress > 0f)
            reactor.Integrity -= reactor.IntegrityLossPerSecond * (0.125f + 0.875f * thermalStress) * seconds;
        else if (reactor.Online && yellowThermalStress)
            reactor.Integrity -= reactor.IntegrityLossPerSecond * 0.125f * seconds;

        if (reactor.Online && vacuumLost)
            reactor.Integrity -= reactor.VacuumIntegrityLossPerSecond * seconds;

        if (reactor.Online && shieldingDeficit > 0f)
            reactor.Integrity -= reactor.ShieldingIntegrityLossPerSecond * shieldingDeficit * seconds;

        if (!vacuumLost && shieldingDeficit <= 0f && !yellowThermalStress)
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

        if (reactor.Online && (vacuumLost || shieldingDeficit > 0f || yellowThermalStress))
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

    private void OnSetOutputPercent(EntityUid uid, ZpmConsoleComponent component, ZpmSetOutputPercentMessage args)
    {
        if (!TryGetLinkedReactor(component, out var reactorUid, out var reactor))
            return;

        reactor.OutputPercent = Math.Clamp((int) MathF.Round(args.Percent / 5f) * 5, 0, 100);
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
        {
            reactor.PowerOutput = 0f;
            reactor.CoolantInletTemperature = 0f;
            reactor.HeatOutletTemperature = 0f;
        }
        UpdatePower(uid, reactor);
        UpdateReactorAppearance(uid, reactor);
    }

    private void UpdatePower(EntityUid uid, ZpmReactorComponent reactor)
    {
        reactor.PowerOutput = reactor.Online && TryGetCoreController(uid, out _, out var controller) &&
                              controller.CoreSlot.Item != null
            ? MathF.Min(GetMaximumPowerOutput(), GetMaximumPowerOutput() * (reactor.OutputPercent / 100f) *
                GetPowerEfficiency(reactor.CoreTemperature))
            : 0f;
        if (TryComp<PowerSupplierComponent>(uid, out var supplier))
            supplier.MaxSupply = reactor.PowerOutput;
    }

    private static float GetMaximumPowerOutput() => 2_500_000f;

    private static float GetPowerEfficiency(float coreTemperature)
    {
        // Blue temperatures provide a modest output bonus; green gives normal
        // output, and hotter bands progressively reduce it.
        if (coreTemperature < CoreGreenTemperature)
            return 1f + 0.25f * Math.Clamp((CoreGreenTemperature - coreTemperature) / CoreGreenTemperature, 0f, 1f);

        if (coreTemperature < CoreYellowTemperature)
            return 1f;

        if (coreTemperature < CoreRedTemperature)
            return 0.75f + 0.25f * (CoreRedTemperature - coreTemperature) /
                (CoreRedTemperature - CoreYellowTemperature);

        return 0.25f + 0.5f * (1f - Math.Clamp(
            (coreTemperature - CoreRedTemperature) / (MaximumCoreTemperature - CoreRedTemperature), 0f, 1f));
    }

    private static float GetHeatDemand(int outputPercent) => 300_000f * (Math.Clamp(outputPercent, 0, 100) / 100f);

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
        var low = reactor.OutputPercent <= 50;
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
        _chat.DispatchStationAnnouncement(
            station,
            message,
            "ZPM CONTAINMENT",
            true,
            new SoundPathSpecifier("/Audio/Machines/alarm.ogg"),
            Color.Red);
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
            reactor.OutputPercent,
            reactor.Integrity,
            reactor.ShieldingEfficiency,
            reactor.ChamberPressure,
            reactor.CoolantInletTemperature,
            reactor.HeatOutletTemperature,
            reactor.CoreTemperature,
            reactor.PowerOutput,
            TryComp<PowerSupplierComponent>(reactorUid, out var supplier) ? supplier.CurrentSupply : 0f,
            GetPowerEfficiency(reactor.CoreTemperature) * 100f);
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
