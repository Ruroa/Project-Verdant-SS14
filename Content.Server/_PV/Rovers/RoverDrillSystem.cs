using Content.Shared._PV.Rovers;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceNetwork;

namespace Content.Server._PV.Rovers;

public sealed class RoverDrillSystem : EntitySystem
{
    [Dependency] private SharedDeviceLinkSystem _deviceLink = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoverDrillComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<RoverDrillComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    private void OnInit(Entity<RoverDrillComponent> ent, ref ComponentInit args)
    {
        _deviceLink.EnsureSinkPorts(ent, ent.Comp.TogglePort, ent.Comp.OnPort, ent.Comp.OffPort);
        UpdateAppearance(ent);
    }

    private void OnSignalReceived(Entity<RoverDrillComponent> ent, ref SignalReceivedEvent args)
    {
        var state = SignalState.Momentary;
        args.Data?.TryGetValue(DeviceNetworkConstants.LogicState, out state);
        if (state is not (SignalState.High or SignalState.Momentary))
            return;

        if (args.Port == ent.Comp.TogglePort)
            ent.Comp.Enabled = !ent.Comp.Enabled;
        else if (args.Port == ent.Comp.OnPort)
            ent.Comp.Enabled = true;
        else if (args.Port == ent.Comp.OffPort)
            ent.Comp.Enabled = false;
        else
            return;

        Dirty(ent);
        UpdateAppearance(ent);
    }

    private void UpdateAppearance(Entity<RoverDrillComponent> ent)
    {
        _appearance.SetData(ent, RoverDrillVisuals.Enabled, ent.Comp.Enabled);
    }
}
