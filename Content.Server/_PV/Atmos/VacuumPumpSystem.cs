using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.Audio;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Components;
using Content.Shared._PV.Atmos;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.Power;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._PV.Atmos;

public sealed partial class VacuumPumpSystem : EntitySystem
{
    [Dependency] private NodeContainerSystem _nodes = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private AmbientSoundSystem _ambient = default!;

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
            !_nodes.TryGetNode(ent.Owner, "inlet", out PipeNode? inlet))
            return;

        // The inlet is connected to a pipe network containing the chamber's
        // siphoning vent. Removed gas is discarded as if the outlet led directly
        // to space. The rate is limited so pressure falls visibly rather than the
        // complete network disappearing in a single atmos tick.
        var remaining = ent.Comp.MolesPerSecond * args.dt;
        var pipeAmount = MathF.Min(inlet.Air.TotalMoles, remaining);
        if (pipeAmount > 0f)
        {
            inlet.Air.Remove(pipeAmount);
            remaining -= pipeAmount;
        }

        if (remaining <= 0f || inlet.NodeGroup == null)
            return;

        // Ordinary siphoning vents are intentionally slow. Any siphoning vent
        // connected to this pump's inlet network instead shares the high-speed
        // vacuum budget, making the chamber depressurize like a hull breach.
        var vents = EntityQueryEnumerator<GasVentPumpComponent>();
        while (remaining > 0f && vents.MoveNext(out var ventUid, out var vent))
        {
            if (!vent.Enabled ||
                vent.PumpDirection != VentPumpDirection.Siphoning ||
                !_nodes.TryGetNode(ventUid, vent.Outlet, out PipeNode? ventNode) ||
                ventNode.NodeGroup != inlet.NodeGroup)
                continue;

            var environment = _atmosphere.GetTileMixture(ventUid, excite: true);
            if (environment == null)
                continue;

            if (environment.Pressure <= ent.Comp.FinishPressure && args.Grid is { } grid)
            {
                FinishVacuum(grid, args.Map, ventUid, ent.Comp.MaxFinishTiles);
                continue;
            }

            var ventAmount = MathF.Min(environment.TotalMoles, remaining);
            if (ventAmount <= 0f)
                continue;

            environment.Remove(ventAmount);
            remaining -= ventAmount;
        }
    }

    private void FinishVacuum(
        Entity<GridAtmosphereComponent?, GasTileOverlayComponent?> grid,
        Entity<MapAtmosphereComponent?>? map,
        EntityUid ventUid,
        int maxTiles)
    {
        if (!TryComp<MapGridComponent>(grid.Owner, out var mapGrid) || maxTiles <= 0)
            return;

        var atmosGrid = new Entity<GridAtmosphereComponent?>(grid.Owner, grid.Comp1);
        var start = _transform.GetGridTilePositionOrDefault(ventUid);
        var visited = new HashSet<Vector2i> { start };
        var queue = new Queue<Vector2i>();
        queue.Enqueue(start);

        AtmosDirection[] directions =
        {
            AtmosDirection.North,
            AtmosDirection.South,
            AtmosDirection.East,
            AtmosDirection.West,
        };

        while (queue.TryDequeue(out var tile))
        {
            _atmosphere.GetTileMixture(grid, map, tile, true)?.Clear();

            if (visited.Count >= maxTiles)
                continue;

            foreach (var direction in directions)
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
