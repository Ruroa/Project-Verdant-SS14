using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Audio;
using Content.Server.Power.Components;
using Content.Shared._PV.Atmos;
using Content.Shared.Power;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;

namespace Content.Server._PV.Atmos;

public sealed partial class VacuumPumpSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private AmbientSoundSystem _ambient = default!;
    [Dependency] private TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VacuumPumpComponent, AtmosDeviceUpdateEvent>(OnAtmosUpdate);
        SubscribeLocalEvent<VacuumPumpComponent, VacuumPumpToggleMessage>(OnToggle);
        SubscribeLocalEvent<VacuumPumpComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<VacuumPumpComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnToggle(Entity<VacuumPumpComponent> ent, ref VacuumPumpToggleMessage args)
    {
        ent.Comp.Enabled = !ent.Comp.Enabled;
        Dirty(ent);
        UpdateState(ent);
    }

    private void OnUiOpened(Entity<VacuumPumpComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateState(ent);
    }

    private void OnPowerChanged(Entity<VacuumPumpComponent> ent, ref PowerChangedEvent args)
    {
        UpdateState(ent, args.Powered);
    }

    private void OnAtmosUpdate(Entity<VacuumPumpComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        if (!ent.Comp.Enabled ||
            !TryComp<ApcPowerReceiverComponent>(ent, out var receiver) ||
            !receiver.Powered ||
            args.Grid is not { } grid)
            return;

        var position = _transform.GetGridTilePositionOrDefault(ent.Owner);
        _atmosphere.GetTileMixture(grid, args.Map, position, true)?.Clear();

        // Clearing the pump tile and its four neighbours creates a very strong
        // atmospheric sink. A sealed room equalizes into it until it reaches 0 kPa.
        var adjacent = _atmosphere.GetAdjacentTileMixtures(grid, position, false, true);
        while (adjacent.MoveNext(out var mixture))
            mixture.Clear();
    }

    private void UpdateState(Entity<VacuumPumpComponent> ent, bool? poweredOverride = null)
    {
        var powered = poweredOverride ??
            (TryComp<ApcPowerReceiverComponent>(ent, out var receiver) && receiver.Powered);
        var running = ent.Comp.Enabled && powered;

        _appearance.SetData(ent, VacuumPumpVisuals.Enabled, running);
        _ambient.SetAmbience(ent, running);
        _ui.SetUiState(ent.Owner, VacuumPumpUiKey.Key, new VacuumPumpUiState(ent.Comp.Enabled, powered));
    }
}
