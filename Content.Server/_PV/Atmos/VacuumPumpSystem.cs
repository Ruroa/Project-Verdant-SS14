using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Audio;
using Content.Server.Power.Components;
using Content.Shared._PV.Atmos;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Power;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._PV.Atmos;

public sealed partial class VacuumPumpSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private AmbientSoundSystem _ambient = default!;

    private static readonly AtmosDirection[] Directions =
    {
        AtmosDirection.North,
        AtmosDirection.South,
        AtmosDirection.East,
        AtmosDirection.West,
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VacuumPumpComponent, AtmosDeviceUpdateEvent>(OnAtmosUpdate);
        SubscribeLocalEvent<VacuumPumpComponent, VacuumPumpToggleMessage>(OnToggle);
        SubscribeLocalEvent<VacuumPumpComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<VacuumPumpComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<VacuumPumpComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    private void OnToggle(Entity<VacuumPumpComponent> ent, ref VacuumPumpToggleMessage args)
    {
        SetEnabled(ent, !ent.Comp.Enabled);
    }

    private void OnUiOpened(Entity<VacuumPumpComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateState(ent);
    }

    private void OnPowerChanged(Entity<VacuumPumpComponent> ent, ref PowerChangedEvent args)
    {
        UpdateState(ent, args.Powered);
    }

    private void OnSignalReceived(Entity<VacuumPumpComponent> ent, ref SignalReceivedEvent args)
    {
        var state = SignalState.Momentary;
        args.Data?.TryGetValue(DeviceNetworkConstants.LogicState, out state);
        if (state is not (SignalState.High or SignalState.Momentary))
            return;

        switch (args.Port)
        {
            case "On":
                SetEnabled(ent, true);
                break;
            case "Off":
                SetEnabled(ent, false);
                break;
            case "Toggle":
                SetEnabled(ent, !ent.Comp.Enabled);
                break;
        }
    }

    private void OnAtmosUpdate(Entity<VacuumPumpComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        if (!ent.Comp.Enabled ||
            !TryComp<ApcPowerReceiverComponent>(ent, out var receiver) ||
            !receiver.Powered ||
            args.Grid is not { } grid ||
            !TryComp<MapGridComponent>(grid.Owner, out var mapGrid))
            return;

        var pumpTile = _transform.GetGridTilePositionOrDefault(ent.Owner);
        var facing = Transform(ent).LocalRotation.GetCardinalDir().ToIntVec();
        var start = pumpTile + facing;

        if (!_map.TryGetTileRef(grid.Owner, mapGrid, start, out var startTile) || startTile.Tile.IsEmpty)
            return;

        var mixtures = FindFacingRoom(grid, args.Map, mapGrid, start, ent.Comp.MaxFinishTiles);
        var totalMoles = 0f;
        foreach (var mixture in mixtures)
            totalMoles += mixture.TotalMoles;

        if (totalMoles <= 0f)
            return;

        var ratio = MathF.Min(1f, ent.Comp.MolesPerSecond * args.dt / totalMoles);
        foreach (var mixture in mixtures)
        {
            if (ratio >= 1f)
                mixture.Clear();
            else
                mixture.RemoveRatio(ratio);
        }
    }

    private HashSet<GasMixture> FindFacingRoom(
        Entity<GridAtmosphereComponent?, GasTileOverlayComponent?> grid,
        Entity<MapAtmosphereComponent?>? map,
        MapGridComponent mapGrid,
        Vector2i start,
        int maxTiles)
    {
        var mixtures = new HashSet<GasMixture>();
        if (maxTiles <= 0)
            return mixtures;

        var atmosGrid = new Entity<GridAtmosphereComponent?>(grid.Owner, grid.Comp1);
        var visited = new HashSet<Vector2i> { start };
        var queue = new Queue<Vector2i>();
        queue.Enqueue(start);

        while (queue.TryDequeue(out var tile))
        {
            var mixture = _atmosphere.GetTileMixture(grid, map, tile, true);
            if (mixture != null)
                mixtures.Add(mixture);

            if (visited.Count >= maxTiles)
                continue;

            foreach (var direction in Directions)
            {
                if (_atmosphere.IsTileAirBlockedCached(atmosGrid, tile, direction))
                    continue;

                var neighbor = tile.Offset(direction);
                if (visited.Contains(neighbor) ||
                    !_map.TryGetTileRef(grid.Owner, mapGrid, neighbor, out var tileRef) ||
                    tileRef.Tile.IsEmpty ||
                    _atmosphere.IsTileAirBlockedCached(atmosGrid, neighbor, direction.GetOpposite()))
                    continue;

                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return mixtures;
    }

    private void SetEnabled(Entity<VacuumPumpComponent> ent, bool enabled)
    {
        if (ent.Comp.Enabled == enabled)
            return;

        ent.Comp.Enabled = enabled;
        Dirty(ent);
        UpdateState(ent);
    }

    private void UpdateState(Entity<VacuumPumpComponent> ent, bool? poweredOverride = null)
    {
        var powered = poweredOverride ??
            (TryComp<ApcPowerReceiverComponent>(ent, out var receiver) && receiver.Powered);
        _ambient.SetAmbience(ent, ent.Comp.Enabled && powered);
        _ui.SetUiState(ent.Owner, VacuumPumpUiKey.Key, new VacuumPumpUiState(ent.Comp.Enabled, powered));
    }
}
